import { describe, expect, test } from "bun:test";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
	ASPIRE_SDK,
	applyAspireVersionFix,
	findAspireVersionMismatches,
	fixAspireVersion,
	GLOBAL_JSON,
	getAspireSdkVersion,
	getSampleAspireVersions,
	SAMPLE_CONFIG,
} from "./validate-aspire-version.mts";

const GLOBAL_JSON_CONTENT = {
	"msbuild-sdks": {
		[ASPIRE_SDK]: "13.6.5",
	},
};

function createSampleConfig(): Record<string, unknown> {
	return {
		appHost: {
			path: "apphost.mts",
			language: "typescript/nodejs",
		},
		sdk: {
			version: "13.6.5",
		},
		packages: {
			"Aspire.Hosting.Azure.PostgreSQL": "13.6.5",
			"Aspire.Hosting.Azure.Redis": "13.6.5",
			"Aspire.Hosting.JavaScript": "13.6.5",
			"AspireC4.Hosting": "../../src/src/AspireC4/AspireC4.csproj",
			"Unrelated.Package": "1.0.0",
		},
	};
}

async function withTemporaryRepository(
	sampleContent: string,
	callback: (root: string) => Promise<void>,
): Promise<void> {
	const root = await mkdtemp(join(tmpdir(), "aspirec4-aspire-version-"));
	const sampleDirectory = join(root, "samples", "typescript-app-host");

	try {
		await mkdir(sampleDirectory, { recursive: true });
		await writeFile(
			join(root, GLOBAL_JSON),
			`${JSON.stringify(GLOBAL_JSON_CONTENT, null, "\t")}\n`,
			"utf8",
		);
		await writeFile(join(root, SAMPLE_CONFIG), sampleContent, "utf8");
		await callback(root);
	} finally {
		await rm(root, { recursive: true, force: true });
	}
}

describe("getAspireSdkVersion", () => {
	test("returns the pinned Aspire AppHost SDK version", () => {
		expect(getAspireSdkVersion(GLOBAL_JSON_CONTENT)).toBe("13.6.5");
	});

	test("throws when the Aspire AppHost SDK is not pinned", () => {
		expect(() => getAspireSdkVersion({ "msbuild-sdks": {} })).toThrow(ASPIRE_SDK);
	});
});

describe("getSampleAspireVersions", () => {
	test("includes the SDK and every official Aspire hosting package", () => {
		const entries = getSampleAspireVersions(createSampleConfig());

		expect(entries).toEqual([
			{ path: "sdk.version", version: "13.6.5" },
			{ path: 'packages["Aspire.Hosting.Azure.PostgreSQL"]', version: "13.6.5" },
			{ path: 'packages["Aspire.Hosting.Azure.Redis"]', version: "13.6.5" },
			{ path: 'packages["Aspire.Hosting.JavaScript"]', version: "13.6.5" },
		]);
	});

	test("ignores the AspireC4 project reference and unrelated packages", () => {
		const entries = getSampleAspireVersions(createSampleConfig());

		expect(entries.some(entry => entry.path.includes("AspireC4"))).toBe(false);
		expect(entries.some(entry => entry.path.includes("Unrelated"))).toBe(false);
	});

	test("throws when sdk.version is missing", () => {
		expect(() => getSampleAspireVersions({ sdk: {} })).toThrow("sdk.version");
	});
});

describe("applyAspireVersionFix", () => {
	test("rewrites the SDK and Aspire hosting package versions", () => {
		const config = createSampleConfig();

		const changed = applyAspireVersionFix(config, "13.7.0");

		expect(changed).toBe(true);
		expect(getSampleAspireVersions(config)).toEqual([
			{ path: "sdk.version", version: "13.7.0" },
			{ path: 'packages["Aspire.Hosting.Azure.PostgreSQL"]', version: "13.7.0" },
			{ path: 'packages["Aspire.Hosting.Azure.Redis"]', version: "13.7.0" },
			{ path: 'packages["Aspire.Hosting.JavaScript"]', version: "13.7.0" },
		]);
	});

	test("leaves project references and unrelated packages alone", () => {
		const config = createSampleConfig();

		applyAspireVersionFix(config, "13.7.0");

		const packages = config["packages"] as Record<string, string>;
		expect(packages["AspireC4.Hosting"]).toBe("../../src/src/AspireC4/AspireC4.csproj");
		expect(packages["Unrelated.Package"]).toBe("1.0.0");
	});

	test("reports no change when every version already matches", () => {
		const config = createSampleConfig();

		expect(applyAspireVersionFix(config, "13.6.5")).toBe(false);
	});
});

describe("findAspireVersionMismatches", () => {
	test("reports each field that has drifted from global.json", async () => {
		const config = createSampleConfig();
		config["sdk"] = { version: "13.5.3" };
		(config["packages"] as Record<string, string>)["Aspire.Hosting.JavaScript"] = "13.5.3";
		const sampleContent = `${JSON.stringify(config, null, "  ")}\n`;

		await withTemporaryRepository(sampleContent, async root => {
			const mismatches = await findAspireVersionMismatches(root);

			expect(mismatches).toEqual([
				{ file: SAMPLE_CONFIG, path: "sdk.version", expected: "13.6.5", actual: "13.5.3" },
				{
					file: SAMPLE_CONFIG,
					path: 'packages["Aspire.Hosting.JavaScript"]',
					expected: "13.6.5",
					actual: "13.5.3",
				},
			]);
		});
	});

	test("reports nothing when the sample matches global.json", async () => {
		const sampleContent = `${JSON.stringify(createSampleConfig(), null, "  ")}\n`;

		await withTemporaryRepository(sampleContent, async root => {
			expect(await findAspireVersionMismatches(root)).toEqual([]);
		});
	});
});

describe("fixAspireVersion", () => {
	test("rewrites the sample without changing its indentation or trailing newline", async () => {
		const config = createSampleConfig();
		config["sdk"] = { version: "13.5.3" };
		(config["packages"] as Record<string, string>)["Aspire.Hosting.JavaScript"] = "13.5.3";
		const sampleContent = JSON.stringify(config, null, "  ");

		await withTemporaryRepository(sampleContent, async root => {
			const result = await fixAspireVersion(root);
			const written = await readFile(join(root, SAMPLE_CONFIG), "utf8");
			const parsed = JSON.parse(written) as Record<string, unknown>;

			expect(result).toEqual({ changed: true, version: "13.6.5" });
			expect(written.startsWith('{\n  "appHost"')).toBe(true);
			expect(written.endsWith("\n")).toBe(false);
			expect(getSampleAspireVersions(parsed)).toEqual([
				{ path: "sdk.version", version: "13.6.5" },
				{ path: 'packages["Aspire.Hosting.Azure.PostgreSQL"]', version: "13.6.5" },
				{ path: 'packages["Aspire.Hosting.Azure.Redis"]', version: "13.6.5" },
				{ path: 'packages["Aspire.Hosting.JavaScript"]', version: "13.6.5" },
			]);
		});
	});

	test("is idempotent", async () => {
		const config = createSampleConfig();
		config["sdk"] = { version: "13.5.3" };
		const sampleContent = `${JSON.stringify(config, null, "  ")}\n`;

		await withTemporaryRepository(sampleContent, async root => {
			await fixAspireVersion(root);

			expect(await fixAspireVersion(root)).toEqual({ changed: false, version: "13.6.5" });
		});
	});

	test("preserves CRLF line endings", async () => {
		const config = createSampleConfig();
		config["sdk"] = { version: "13.5.3" };
		const sampleContent = JSON.stringify(config, null, "  ").replaceAll("\n", "\r\n");

		await withTemporaryRepository(sampleContent, async root => {
			await fixAspireVersion(root);
			const written = await readFile(join(root, SAMPLE_CONFIG), "utf8");

			expect(written).toBe(JSON.stringify(createSampleConfig(), null, "  ").replaceAll("\n", "\r\n"));
		});
	});
});

describe("repository", () => {
	test("the TypeScript sample uses the Aspire version pinned in global.json", async () => {
		const repositoryRoot = join(import.meta.dir, "..");

		expect(await findAspireVersionMismatches(repositoryRoot)).toEqual([]);
	});
});
