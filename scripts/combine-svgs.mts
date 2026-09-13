#!/usr/bin/env bun
/**
 * combine-svgs.mts
 *
 * Combines two SVG files (one for light theme, one for dark theme) into a single
 * SVG that uses CSS `@media (prefers-color-scheme: dark)` to display the correct
 * variant automatically.
 *
 * The light-theme SVG is shown by default; the dark-theme SVG is shown when the
 * user's OS/browser reports a dark colour-scheme preference.
 *
 * All `id` attributes in each source SVG are namespaced with a prefix (`light-` /
 * `dark-`) so that references inside gradients, masks, filters, clip-paths, etc.
 * never collide between the two embedded variants.
 *
 * Usage:
 *   bun scripts/combine-svgs.mts <light.svg> <dark.svg> [output.svg]
 *
 *   bun scripts/combine-svgs.mts --light icon-light.svg \
 *                                  --dark  icon-dark.svg  \
 *                                  --out   icon.svg
 *
 * If output.svg / --out is omitted the result is written to stdout.
 *
 * Flags:
 *   --light <path>   Path to the light-theme SVG
 *   --dark  <path>   Path to the dark-theme SVG
 *   --out   <path>   Output path (default: stdout)
 *   --help           Print this help text
 */

import { readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import process from "node:process";

// ---------------------------------------------------------------------------
// CLI argument parsing
// ---------------------------------------------------------------------------

interface CliOpts {
	light: string;
	dark: string;
	out: string | null;
}

function printHelp(): void {
	console.log(
		`
Usage:
  bun scripts/combine-svgs.mts <light.svg> <dark.svg> [output.svg]
  bun scripts/combine-svgs.mts --light <path> --dark <path> [--out <path>]

Options:
  --light  Path to the light-theme SVG (default: first positional arg)
  --dark   Path to the dark-theme SVG  (default: second positional arg)
  --out    Output path                 (default: third positional arg or stdout)
  --help   Print this help text
`.trim(),
	);
}

function parseArgs(argv: string[]): CliOpts {
	const args = argv.slice(2);
	const opts: Partial<CliOpts> & { out: string | null } = { out: null };
	const positional: string[] = [];

	for (let i = 0; i < args.length; i++) {
		const a = args[i];
		if (a === "--help" || a === "-h") {
			printHelp();
			process.exit(0);
		} else if (a === "--light" || a === "--dark" || a === "--out") {
			const key = a.slice(2) as "light" | "dark" | "out";
			if (i + 1 >= args.length) {
				console.error(`Error: ${a} requires a value`);
				process.exit(1);
			}
			opts[key] = args[++i];
		} else if (a.startsWith("--")) {
			console.error(`Error: Unknown option: ${a}`);
			process.exit(1);
		} else {
			positional.push(a);
		}
	}

	opts.light ??= positional[0];
	opts.dark ??= positional[1];
	opts.out ??= positional[2] ?? null;

	if (!opts.light || !opts.dark) {
		console.error("Error: Both --light and --dark SVG paths are required.");
		printHelp();
		process.exit(1);
	}

	return opts as CliOpts;
}

// ---------------------------------------------------------------------------
// SVG parsing helpers
// ---------------------------------------------------------------------------

interface SvgOpenTag {
	full: string;
	attrs: string;
}

/** Extract the raw attribute string from the opening <svg ...> tag. */
function extractSvgOpenTag(content: string): SvgOpenTag | null {
	const m = content.match(/<svg(\s[^>]*)?>/);
	return m ? { full: m[0], attrs: m[1] ?? "" } : null;
}

/** Return the content between <svg...> and </svg>. */
function extractSvgInner(content: string): string {
	const m = content.match(/<svg(?:\s[^>]*)?>(?<inner>[\s\S]*?)<\/svg\s*>/);
	return m?.groups?.inner?.trim() ?? "";
}

/**
 * Parse an SVG attribute string into a Map (preserving order).
 * Handles both double- and single-quoted attribute values.
 */
function parseAttrString(attrString: string): Map<string, string> {
	const map = new Map<string, string>();
	const re = /(?<name>[\w:.-]+)=(?:"(?<dq>[^"]*)"|'(?<sq>[^']*)')/g;
	let m: RegExpExecArray | null;
	while ((m = re.exec(attrString)) !== null) {
		map.set(m.groups!.name, m.groups!.dq ?? m.groups!.sq ?? "");
	}
	return map;
}

/** Serialise a Map back to an SVG attribute string. */
function serializeAttrs(map: Map<string, string>): string {
	return [...map.entries()].map(([k, v]) => `${k}="${v}"`).join(" ");
}

// ---------------------------------------------------------------------------
// ID namespacing
// ---------------------------------------------------------------------------

/** Collect every `id="..."` value from an SVG string. */
function collectIds(svgContent: string): Set<string> {
	const ids = new Set<string>();
	const re = /\bid="([^"]+)"/g;
	let m: RegExpExecArray | null;
	while ((m = re.exec(svgContent)) !== null) ids.add(m[1]);
	return ids;
}

/**
 * Prefix all id definitions and all references to those ids (href="#id",
 * xlink:href="#id", url(#id), begin="id.", end="id.") so that the two
 * embedded SVGs never share identifiers.
 *
 * Only IDs that actually exist in this SVG are rewritten — external
 * references are left untouched.
 */
function prefixIds(svgContent: string, prefix: string): string {
	const ids = collectIds(svgContent);
	if (ids.size === 0) return svgContent;

	let result = svgContent;

	// id="…" definitions
	result = result.replace(/\bid="([^"]+)"/g, (_, id: string) =>
		ids.has(id) ? `id="${prefix}${id}"` : `id="${id}"`,
	);

	// href="#…" (SVG 2 / HTML5)
	result = result.replace(/\bhref="#([^"]+)"/g, (full, id: string) =>
		ids.has(id) ? `href="#${prefix}${id}"` : full,
	);

	// xlink:href="#…" (SVG 1.1 legacy)
	result = result.replace(/\bxlink:href="#([^"]+)"/g, (full, id: string) =>
		ids.has(id) ? `xlink:href="#${prefix}${id}"` : full,
	);

	// url(#…) — used in fill, stroke, filter, clip-path, mask, etc.
	result = result.replace(/url\(#([^)]+)\)/g, (full, id: string) =>
		ids.has(id) ? `url(#${prefix}${id})` : full,
	);

	// SMIL begin="id.event" / end="id.event"
	result = result.replace(
		/\b(begin|end)="([^"]+)"/g,
		(full, attr: string, value: string) => {
			const rewritten = value
				.split(";")
				.map((spec) => {
					const dotIdx = spec.indexOf(".");
					if (dotIdx === -1) return spec;
					const ref = spec.slice(0, dotIdx);
					return ids.has(ref) ? `${prefix}${ref}${spec.slice(dotIdx)}` : spec;
				})
				.join(";");
			return `${attr}="${rewritten}"`;
		},
	);

	return result;
}

// ---------------------------------------------------------------------------
// Core combiner
// ---------------------------------------------------------------------------

function combineSvgs(lightRaw: string, darkRaw: string): string {
	// Strip XML declaration / DOCTYPE if present (not valid inside SVG as child)
	const strip = (s: string): string =>
		s.replace(/^<\?xml[^?]*\?>\s*/i, "").replace(/^<!DOCTYPE[^>]*>\s*/i, "");
	lightRaw = strip(lightRaw);
	darkRaw = strip(darkRaw);

	const lightTag = extractSvgOpenTag(lightRaw);
	const darkTag = extractSvgOpenTag(darkRaw);

	if (!lightTag)
		throw new Error("Could not locate <svg> element in the light SVG.");
	if (!darkTag)
		throw new Error("Could not locate <svg> element in the dark SVG.");

	// Build combined root attributes:
	//  - start with dark attrs as base so any extra namespaces are captured
	//  - then overlay light attrs (light takes precedence for viewBox / dimensions)
	const darkAttrs = parseAttrString(darkTag.attrs);
	const lightAttrs = parseAttrString(lightTag.attrs);
	const combined = new Map<string, string>([...darkAttrs, ...lightAttrs]);

	// Ensure the standard SVG namespace is always present
	if (!combined.has("xmlns"))
		combined.set("xmlns", "http://www.w3.org/2000/svg");

	// Namespace IDs in each variant to prevent collisions
	const lightInner = prefixIds(extractSvgInner(lightRaw), "light-");
	const darkInner = prefixIds(extractSvgInner(darkRaw), "dark-");

	const css = [
		".svg-dark { display: none; }",
		"@media (prefers-color-scheme: dark) {",
		"  .svg-light { display: none; }",
		"  .svg-dark  { display: inline; }",
		"}",
	].join("\n    ");

	return (
		`<svg ${serializeAttrs(combined)}>\n` +
		`  <style>\n    ${css}\n  </style>\n` +
		`  <g class="svg-light">\n    ${lightInner.replace(/\n/g, "\n    ")}\n  </g>\n` +
		`  <g class="svg-dark">\n    ${darkInner.replace(/\n/g, "\n    ")}\n  </g>\n` +
		`</svg>\n`
	);
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------

async function main(): Promise<void> {
	const opts = parseArgs(process.argv);

	const [lightRaw, darkRaw] = await Promise.all([
		readFile(resolve(opts.light), "utf8"),
		readFile(resolve(opts.dark), "utf8"),
	]);

	const output = combineSvgs(lightRaw, darkRaw);

	if (opts.out) {
		await writeFile(resolve(opts.out), output, "utf8");
		console.error(`✓ Written to ${opts.out}`);
	} else {
		process.stdout.write(output);
	}
}

main().catch((err) => {
	console.error(`Error: ${(err as Error).message}`);
	process.exit(1);
});
