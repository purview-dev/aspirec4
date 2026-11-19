#!/usr/bin/env bun
/**
 * Keeps the Aspire version the polyglot samples ask for in step with the one the repository's own
 * AppHost builds against.
 *
 * `AspireAppHostVersion` in `Directory.Packages.props` is the single source of truth: it is the Aspire
 * line the C# sample AppHost and the integration tests run on. The TypeScript, Go and Python samples
 * repeat that version in their `aspire.config.json`, both as `sdk.version` and as the pins for the
 * official `Aspire.Hosting.*` packages, because the Aspire CLI reads `aspire.config.json` rather than
 * MSBuild. Nothing else keeps them in step, so a version bump that misses a sample silently runs it
 * against an older Aspire.
 *
 * Usage:
 *   bun scripts/validate-aspire-version.mts          check; exits 1 when a sample has drifted
 *   bun scripts/validate-aspire-version.mts --fix    rewrite the samples to match
 *   bun scripts/validate-aspire-version.mts --help   print this help
 */
import { readFile, writeFile } from "node:fs/promises";
import { join } from "node:path";

export const VERSION_SOURCE = "Directory.Packages.props";
export const VERSION_PROPERTY = "AspireAppHostVersion";

/** Every polyglot sample whose `aspire.config.json` repeats the Aspire version. */
export const SAMPLE_CONFIGS = [
	"samples/typescript-app-host/aspire.config.json",
	"samples/go-app-host/aspire.config.json",
	"samples/python-app-host/aspire.config.json",
] as const;

/**
 * The official Aspire hosting packages the samples pin. `AspireC4.Hosting` is deliberately not
 * matched: the samples point it at the local project, and a version string there would be wrong.
 */
const ASPIRE_HOSTING_PACKAGE = /^Aspire\.Hosting(?:\.|$)/;

export interface AspireVersionMismatch {
	file: string;
	path: string;
	expected: string;
	actual: string;
}

export interface AspireVersionFixResult {
	changed: boolean;
	version: string;
	/** The sample configs that were rewritten. */
	files: string[];
}

interface VersionEntry {
	path: string;
	version: string;
}

function isRecord(value: unknown): value is Record<string, unknown> {
	return typeof value === "object" && value !== null && !Array.isArray(value);
}

function parseJson(file: string, text: string): unknown {
	try {
		return JSON.parse(text);
	} catch (error) {
		throw new Error(`${file} is not valid JSON: ${(error as Error).message}`);
	}
}

/**
 * Reads `AspireAppHostVersion` from the text of `Directory.Packages.props`. XML comments are removed
 * first, because the file's own comments mention the property by name.
 */
export function getAspireAppHostVersion(propsText: string): string {
	const withoutComments = propsText.replace(/<!--[\s\S]*?-->/g, "");
	const match = withoutComments.match(
		new RegExp(`<${VERSION_PROPERTY}(?:\\s[^>]*)?>\\s*([^<\\s]+)\\s*</${VERSION_PROPERTY}>`),
	);
	const version = match?.[1];

	if (version === undefined || version.length === 0) {
		throw new Error(`${VERSION_SOURCE} must declare <${VERSION_PROPERTY}>`);
	}

	return version;
}

async function readExpectedVersion(root: string): Promise<string> {
	return getAspireAppHostVersion(await readFile(join(root, VERSION_SOURCE), "utf8"));
}

/** A project path, not a package version. `AspireC4.Hosting` in the samples is one. */
function isProjectPath(value: string): boolean {
	return value.includes("/") || value.includes("\\") || value.endsWith(".csproj");
}

/** Every `aspire.config.json` field that repeats the Aspire version. */
export function getSampleAspireVersions(sampleConfig: unknown, file: string = SAMPLE_CONFIGS[0]): VersionEntry[] {
	if (!isRecord(sampleConfig)) {
		throw new Error(`${file} must contain a JSON object`);
	}

	const sdk = sampleConfig["sdk"];
	if (!isRecord(sdk) || typeof sdk["version"] !== "string") {
		throw new Error(`${file} must declare sdk.version`);
	}

	const entries: VersionEntry[] = [{ path: "sdk.version", version: sdk["version"] }];
	const packages = sampleConfig["packages"];

	if (isRecord(packages)) {
		for (const [name, value] of Object.entries(packages)) {
			if (ASPIRE_HOSTING_PACKAGE.test(name) && typeof value === "string" && !isProjectPath(value)) {
				entries.push({ path: `packages["${name}"]`, version: value });
			}
		}
	}

	return entries;
}

/** Reports every field, in every sample, that does not match `AspireAppHostVersion`. */
export async function findAspireVersionMismatches(
	root: string,
	files: readonly string[] = SAMPLE_CONFIGS,
): Promise<AspireVersionMismatch[]> {
	const expected = await readExpectedVersion(root);
	const mismatches: AspireVersionMismatch[] = [];

	for (const file of files) {
		const sample = parseJson(file, await readFile(join(root, file), "utf8"));

		for (const entry of getSampleAspireVersions(sample, file)) {
			if (entry.version !== expected) {
				mismatches.push({ file, path: entry.path, expected, actual: entry.version });
			}
		}
	}

	return mismatches;
}

/**
 * Rewrites every version field in a parsed sample config to `version`. Returns true when anything
 * changed, so a caller can avoid writing an unchanged file.
 */
export function applyAspireVersionFix(
	sampleConfig: unknown,
	version: string,
	file: string = SAMPLE_CONFIGS[0],
): boolean {
	if (!isRecord(sampleConfig)) {
		throw new Error(`${file} must contain a JSON object`);
	}

	const sdk = sampleConfig["sdk"];
	if (!isRecord(sdk)) {
		throw new Error(`${file} must declare sdk.version`);
	}

	let changed = false;

	if (sdk["version"] !== version) {
		sdk["version"] = version;
		changed = true;
	}

	const packages = sampleConfig["packages"];
	if (isRecord(packages)) {
		for (const [name, value] of Object.entries(packages)) {
			if (
				ASPIRE_HOSTING_PACKAGE.test(name) &&
				typeof value === "string" &&
				!isProjectPath(value) &&
				value !== version
			) {
				packages[name] = version;
				changed = true;
			}
		}
	}

	return changed;
}

/**
 * Serialises a config the way the source file was written: its own indentation and line endings,
 * and a trailing newline only when it had one. Keeps `--fix` from producing a formatting-only diff.
 */
export function formatJson(value: unknown, source: string): string {
	const indentation = source.match(/\r?\n([ \t]+)"/)?.[1] ?? "  ";
	const lineEnding = source.includes("\r\n") ? "\r\n" : "\n";
	const trailingNewline = source.endsWith("\n") ? lineEnding : "";
	const json = JSON.stringify(value, null, indentation);

	return lineEnding === "\n"
		? json + trailingNewline
		: json.replaceAll("\n", lineEnding) + trailingNewline;
}

/** Applies the fix to every sample config on disk and reports what was written. */
export async function fixAspireVersion(
	root: string,
	files: readonly string[] = SAMPLE_CONFIGS,
): Promise<AspireVersionFixResult> {
	const version = await readExpectedVersion(root);
	const written: string[] = [];

	for (const file of files) {
		const path = join(root, file);
		const source = await readFile(path, "utf8");
		const config = parseJson(file, source);

		if (applyAspireVersionFix(config, version, file)) {
			await writeFile(path, formatJson(config, source), "utf8");
			written.push(file);
		}
	}

	return { changed: written.length > 0, version, files: written };
}

function printHelp(): void {
	console.log(
		`Usage:
  bun scripts/validate-aspire-version.mts [--fix]

Compares ${VERSION_PROPERTY} in ${VERSION_SOURCE} with the Aspire version in each sample's
aspire.config.json (sdk.version and the Aspire.Hosting.* package pins):

${SAMPLE_CONFIGS.map(file => `  ${file}`).join("\n")}

Options:
  --fix, -f   Rewrite the samples to match ${VERSION_PROPERTY}
  --help, -h  Print this help text`,
	);
}

async function main(args: string[]): Promise<number> {
	if (args.includes("--help") || args.includes("-h")) {
		printHelp();
		return 0;
	}

	const root = join(import.meta.dir, "..");
	const fix = args.includes("--fix") || args.includes("-f");

	if (fix) {
		const result = await fixAspireVersion(root);
		if (result.changed) {
			for (const file of result.files) {
				console.log(`Updated ${file} to Aspire ${result.version}.`);
			}
		} else {
			console.log(`Aspire ${result.version} is already in sync in every sample.`);
		}
		return 0;
	}

	const mismatches = await findAspireVersionMismatches(root);
	if (mismatches.length === 0) {
		const version = await readExpectedVersion(root);
		console.log(`Aspire ${version} is in sync with the TypeScript, Go and Python samples.`);
		return 0;
	}

	for (const mismatch of mismatches) {
		console.error(
			`${mismatch.file} ${mismatch.path}: expected ${mismatch.expected} (${VERSION_PROPERTY} in ${VERSION_SOURCE}), found ${mismatch.actual}`,
		);
	}
	console.error("Run `just samples-validate-fix` to update the samples.");
	return 1;
}

if (import.meta.main) {
	try {
		process.exitCode = await main(process.argv.slice(2));
	} catch (error) {
		console.error(`error: ${(error as Error).message}`);
		process.exitCode = 1;
	}
}
