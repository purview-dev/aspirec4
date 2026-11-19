import { describe, expect, test } from "bun:test";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import {
	applyAspireVersionFix,
	findAspireVersionMismatches,
	fixAspireVersion,
	getAspireAppHostVersion,
	getSampleAspireVersions,
	SAMPLE_CONFIGS,
	VERSION_PROPERTY,
	VERSION_SOURCE,
} from "./validate-aspire-version.mts";

const TYPESCRIPT_SAMPLE = SAMPLE_CONFIGS[0];
const GO_SAMPLE = SAMPLE_CONFIGS[1];

// The comment names the property on purpose: the real file's comments do, and must not be read.
const PROPS_CONTENT = `<Project>
	<PropertyGroup>
		<!-- ${VERSION_PROPERTY} is what the repository's own AppHost runs on: <${VERSION_PROPERTY}>0.0.0</${VERSION_PROPERTY}> -->
		<AspireVersion>13.6.5</AspireVersion>
		<${VERSION_PROPERTY}>17.0.0</${VERSION_PROPERTY}>
	</PropertyGroup>
</Project>
`;

function createSampleConfig(): Record<string, unknown> {
	return {
		appHost: {
			path: "apphost.mts",
			language: "typescript/nodejs",
		},
		sdk: {
			version: "17.0.0",
		},
		packages: {
			"Aspire.Hosting.Azure.PostgreSQL": "17.0.0",
			"Aspire.Hosting.Azure.Redis": "17.0.0",
			"Aspire.Hosting.JavaScript": "17.0.0",
			"AspireC4.Hosting": "../../src/src/AspireC4/AspireC4.csproj",
			"Unrelated.Package": "1.0.0",
		},
	};
}

async function withTemporaryRepository(
	samples: Record<string, string>,
	callback: (root: string) => Promise<void>,
): Promise<void> {
	const root = await mkdtemp(join(tmpdir(), "aspirec4-aspire-version-"));

	try {
		await writeFile(join(root, VERSION_SOURCE), PROPS_CONTENT, "utf8");
		for (const [file, content] of Object.entries(samples)) {
			await mkdir(dirname(join(root, file)), { recursive: true });
			await writeFile(join(root, file), content, "utf8");
		}
		await callback(root);
	} finally {
		await rm(root, { recursive: true, force: true });
	}
}

describe("getAspireAppHostVersion", () => {
	test("returns the version the repository's AppHost runs on", () => {
		expect(getAspireAppHostVersion(PROPS_CONTENT)).toBe("17.0.0");
	});

	test("ignores the property inside XML comments", () => {
		const commentedOnly = `<Project><!-- <${VERSION_PROPERTY}>9.9.9</${VERSION_PROPERTY}> --></Project>`;

		expect(() => getAspireAppHostVersion(commentedOnly)).toThrow(VERSION_PROPERTY);
	});

	test("throws when the property is not declared", () => {
		expect(() => getAspireAppHostVersion("<Project />")).toThrow(VERSION_PROPERTY);
	});
});

describe("getSampleAspireVersions", () => {
	test("includes the SDK and every official Aspire hosting package", () => {
		const entries = getSampleAspireVersions(createSampleConfig());

		expect(entries).toEqual([
			{ path: "sdk.version", version: "17.0.0" },
			{ path: 'packages["Aspire.Hosting.Azure.PostgreSQL"]', version: "17.0.0" },
			{ path: 'packages["Aspire.Hosting.Azure.Redis"]', version: "17.0.0" },
			{ path: 'packages["Aspire.Hosting.JavaScript"]', version: "17.0.0" },
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

		const changed = applyAspireVersionFix(config, "17.1.0");

		expect(changed).toBe(true);
		expect(getSampleAspireVersions(config)).toEqual([
			{ path: "sdk.version", version: "17.1.0" },
			{ path: 'packages["Aspire.Hosting.Azure.PostgreSQL"]', version: "17.1.0" },
			{ path: 'packages["Aspire.Hosting.Azure.Redis"]', version: "17.1.0" },
			{ path: 'packages["Aspire.Hosting.JavaScript"]', version: "17.1.0" },
		]);
	});

	test("leaves project references and unrelated packages alone", () => {
		const config = createSampleConfig();

		applyAspireVersionFix(config, "17.1.0");

		const packages = config["packages"] as Record<string, string>;
		expect(packages["AspireC4.Hosting"]).toBe("../../src/src/AspireC4/AspireC4.csproj");
		expect(packages["Unrelated.Package"]).toBe("1.0.0");
	});

	test("reports no change when every version already matches", () => {
		const config = createSampleConfig();

		expect(applyAspireVersionFix(config, "17.0.0")).toBe(false);
	});
});

describe("findAspireVersionMismatches", () => {
	test("reports each drifted field, in every sample", async () => {
		const typescript = createSampleConfig();
		typescript["sdk"] = { version: "13.6.5" };
		const go = createSampleConfig();
		(go["packages"] as Record<string, string>)["Aspire.Hosting.JavaScript"] = "13.6.5";

		await withTemporaryRepository(
			{
				[TYPESCRIPT_SAMPLE]: `${JSON.stringify(typescript, null, "  ")}\n`,
				[GO_SAMPLE]: `${JSON.stringify(go, null, "  ")}\n`,
			},
			async root => {
				const mismatches = await findAspireVersionMismatches(root, [TYPESCRIPT_SAMPLE, GO_SAMPLE]);

				expect(mismatches).toEqual([
					{ file: TYPESCRIPT_SAMPLE, path: "sdk.version", expected: "17.0.0", actual: "13.6.5" },
					{
						file: GO_SAMPLE,
						path: 'packages["Aspire.Hosting.JavaScript"]',
						expected: "17.0.0",
						actual: "13.6.5",
					},
				]);
			},
		);
	});

	test("reports nothing when the samples match", async () => {
		const sampleContent = `${JSON.stringify(createSampleConfig(), null, "  ")}\n`;

		await withTemporaryRepository({ [TYPESCRIPT_SAMPLE]: sampleContent }, async root => {
			expect(await findAspireVersionMismatches(root, [TYPESCRIPT_SAMPLE])).toEqual([]);
		});
	});
});

describe("fixAspireVersion", () => {
	test("rewrites a sample without changing its indentation or trailing newline", async () => {
		const config = createSampleConfig();
		config["sdk"] = { version: "13.6.5" };
		(config["packages"] as Record<string, string>)["Aspire.Hosting.JavaScript"] = "13.6.5";
		const sampleContent = JSON.stringify(config, null, "  ");

		await withTemporaryRepository({ [TYPESCRIPT_SAMPLE]: sampleContent }, async root => {
			const result = await fixAspireVersion(root, [TYPESCRIPT_SAMPLE]);
			const written = await readFile(join(root, TYPESCRIPT_SAMPLE), "utf8");
			const parsed = JSON.parse(written) as Record<string, unknown>;

			expect(result).toEqual({ changed: true, version: "17.0.0", files: [TYPESCRIPT_SAMPLE] });
			expect(written.startsWith('{\n  "appHost"')).toBe(true);
			expect(written.endsWith("\n")).toBe(false);
			expect(getSampleAspireVersions(parsed)).toEqual([
				{ path: "sdk.version", version: "17.0.0" },
				{ path: 'packages["Aspire.Hosting.Azure.PostgreSQL"]', version: "17.0.0" },
				{ path: 'packages["Aspire.Hosting.Azure.Redis"]', version: "17.0.0" },
				{ path: 'packages["Aspire.Hosting.JavaScript"]', version: "17.0.0" },
			]);
		});
	});

	test("rewrites only the samples that drifted", async () => {
		const drifted = createSampleConfig();
		drifted["sdk"] = { version: "13.6.5" };

		await withTemporaryRepository(
			{
				[TYPESCRIPT_SAMPLE]: `${JSON.stringify(createSampleConfig(), null, "  ")}\n`,
				[GO_SAMPLE]: `${JSON.stringify(drifted, null, "  ")}\n`,
			},
			async root => {
				const result = await fixAspireVersion(root, [TYPESCRIPT_SAMPLE, GO_SAMPLE]);

				expect(result.files).toEqual([GO_SAMPLE]);
			},
		);
	});

	test("is idempotent", async () => {
		const config = createSampleConfig();
		config["sdk"] = { version: "13.6.5" };
		const sampleContent = `${JSON.stringify(config, null, "  ")}\n`;

		await withTemporaryRepository({ [TYPESCRIPT_SAMPLE]: sampleContent }, async root => {
			await fixAspireVersion(root, [TYPESCRIPT_SAMPLE]);

			expect(await fixAspireVersion(root, [TYPESCRIPT_SAMPLE])).toEqual({
				changed: false,
				version: "17.0.0",
				files: [],
			});
		});
	});

	test("preserves CRLF line endings", async () => {
		const config = createSampleConfig();
		config["sdk"] = { version: "13.6.5" };
		const sampleContent = JSON.stringify(config, null, "  ").replaceAll("\n", "\r\n");

		await withTemporaryRepository({ [TYPESCRIPT_SAMPLE]: sampleContent }, async root => {
			await fixAspireVersion(root, [TYPESCRIPT_SAMPLE]);
			const written = await readFile(join(root, TYPESCRIPT_SAMPLE), "utf8");

			expect(written).toBe(JSON.stringify(createSampleConfig(), null, "  ").replaceAll("\n", "\r\n"));
		});
	});
});

describe("repository", () => {
	test("every polyglot sample uses the Aspire version the C# AppHost runs on", async () => {
		const repositoryRoot = join(import.meta.dir, "..");

		expect(await findAspireVersionMismatches(repositoryRoot)).toEqual([]);
	});
});
