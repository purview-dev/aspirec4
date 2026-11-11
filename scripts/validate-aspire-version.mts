#!/usr/bin/env bun
/**
 * Keeps the Aspire version the TypeScript sample asks for in step with the one the repository
 * builds against.
 *
 * `global.json` is the single source of truth for the Aspire AppHost SDK version. The TypeScript
 * sample repeats that version in `samples/typescript-app-host/aspire.config.json`, both as
 * `sdk.version` and as the pins for the official `Aspire.Hosting.*` packages, because the Aspire
 * CLI reads `aspire.config.json` rather than `global.json`. Nothing else keeps the two files in
 * step, so a version bump that misses the sample silently runs it against an older Aspire SDK.
 *
 * Usage:
 *   bun scripts/validate-aspire-version.mts          check; exits 1 when the sample has drifted
 *   bun scripts/validate-aspire-version.mts --fix    rewrite the sample to match global.json
 *   bun scripts/validate-aspire-version.mts --help   print this help
 */
import { readFile, writeFile } from "node:fs/promises";
import { join } from "node:path";

export const GLOBAL_JSON = "global.json";
export const SAMPLE_CONFIG = "samples/typescript-app-host/aspire.config.json";
export const ASPIRE_SDK = "Aspire.AppHost.Sdk";

/**
 * The official Aspire hosting packages the sample pins. `AspireC4.Hosting` is deliberately not
 * matched: the sample points it at the local project, and a version string there would be wrong.
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

async function readJson(file: string): Promise<unknown> {
	return parseJson(file, await readFile(file, "utf8"));
}

/** Reads the Aspire AppHost SDK version from a parsed `global.json`. */
export function getAspireSdkVersion(globalJson: unknown): string {
	const sdks = isRecord(globalJson) ? globalJson["msbuild-sdks"] : undefined;
	const version = isRecord(sdks) ? sdks[ASPIRE_SDK] : undefined;

	if (typeof version !== "string" || version.length === 0) {
		throw new Error(`${GLOBAL_JSON} must declare msbuild-sdks["${ASPIRE_SDK}"]`);
	}

	return version;
}

/** A project path, not a package version. `AspireC4.Hosting` in the sample is one. */
function isProjectPath(value: string): boolean {
	return value.includes("/") || value.includes("\\") || value.endsWith(".csproj");
}

/** Every `aspire.config.json` field that repeats the Aspire SDK version. */
export function getSampleAspireVersions(sampleConfig: unknown): VersionEntry[] {
	if (!isRecord(sampleConfig)) {
		throw new Error(`${SAMPLE_CONFIG} must contain a JSON object`);
	}

	const sdk = sampleConfig["sdk"];
	if (!isRecord(sdk) || typeof sdk["version"] !== "string") {
		throw new Error(`${SAMPLE_CONFIG} must declare sdk.version`);
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

/** Reports every field in the sample that does not match `global.json`. */
export async function findAspireVersionMismatches(root: string): Promise<AspireVersionMismatch[]> {
	const expected = getAspireSdkVersion(await readJson(join(root, GLOBAL_JSON)));
	const sample = await readJson(join(root, SAMPLE_CONFIG));

	return getSampleAspireVersions(sample)
		.filter(entry => entry.version !== expected)
		.map(entry => ({
			file: SAMPLE_CONFIG,
			path: entry.path,
			expected,
			actual: entry.version,
		}));
}

/**
 * Rewrites every version field in a parsed sample config to `version`. Returns true when anything
 * changed, so a caller can avoid writing an unchanged file.
 */
export function applyAspireVersionFix(sampleConfig: unknown, version: string): boolean {
	if (!isRecord(sampleConfig)) {
		throw new Error(`${SAMPLE_CONFIG} must contain a JSON object`);
	}

	const sdk = sampleConfig["sdk"];
	if (!isRecord(sdk)) {
		throw new Error(`${SAMPLE_CONFIG} must declare sdk.version`);
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

/** Applies the fix to the sample config on disk and reports what was written. */
export async function fixAspireVersion(root: string): Promise<AspireVersionFixResult> {
	const version = getAspireSdkVersion(await readJson(join(root, GLOBAL_JSON)));
	const file = join(root, SAMPLE_CONFIG);
	const source = await readFile(file, "utf8");
	const config = parseJson(SAMPLE_CONFIG, source);
	const changed = applyAspireVersionFix(config, version);

	if (changed) {
		await writeFile(file, formatJson(config, source), "utf8");
	}

	return { changed, version };
}

function printHelp(): void {
	console.log(
		`Usage:
  bun scripts/validate-aspire-version.mts [--fix]

Compares the Aspire SDK version in ${GLOBAL_JSON} (msbuild-sdks["${ASPIRE_SDK}"]) with the
version in ${SAMPLE_CONFIG} (sdk.version and the Aspire.Hosting.* package pins).

Options:
  --fix, -f   Rewrite the sample to match ${GLOBAL_JSON}
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
			console.log(`Updated ${SAMPLE_CONFIG} to Aspire ${result.version}.`);
		} else {
			console.log(`Aspire ${result.version} is already in sync (${SAMPLE_CONFIG}).`);
		}
		return 0;
	}

	const mismatches = await findAspireVersionMismatches(root);
	if (mismatches.length === 0) {
		const version = getAspireSdkVersion(await readJson(join(root, GLOBAL_JSON)));
		console.log(`Aspire ${version} is in sync with ${SAMPLE_CONFIG}.`);
		return 0;
	}

	for (const mismatch of mismatches) {
		console.error(
			`${mismatch.file} ${mismatch.path}: expected ${mismatch.expected} (from ${GLOBAL_JSON}), found ${mismatch.actual}`,
		);
	}
	console.error("Run `bun scripts/validate-aspire-version.mts --fix` to update the sample.");
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
