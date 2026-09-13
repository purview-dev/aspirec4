#!/usr/bin/env bun
/**
 * Fetches the full LikeC4 icon list from the likec4/likec4 GitHub repository and writes
 * a JSON manifest to src/src/AspireC4/BuildResources/likec4-icons.json.
 *
 * Uses the Git Trees API (not the Contents API) so that directories with more than
 * 1,000 files are retrieved in full.
 *
 * Run with:
 *   bun scripts/generate-icon-manifest.mts
 *
 * Set GITHUB_TOKEN env var to increase the GitHub API rate limit (optional, recommended).
 */

import { writeFile, mkdir } from "node:fs/promises";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const OUTPUT_PATH = join(
	__dirname,
	"..",
	"src",
	"src",
	"AspireC4",
	"BuildResources",
	"likec4-icons.json"
);

const COLLECTIONS = ["aws", "azure", "gcp", "tech", "bootstrap"] as const;
type Collection = (typeof COLLECTIONS)[number];

const GITHUB_REPO = "likec4/likec4";
const ICONS_BASE_PATH = "packages/icons";
const GITHUB_REF = "main";

const headers: Record<string, string> = {
	"User-Agent": "aspirec4-icon-manifest-generator",
	Accept: "application/vnd.github.v3+json",
};
if (process.env.GITHUB_TOKEN) {
	headers["Authorization"] = `Bearer ${process.env.GITHUB_TOKEN}`;
}

interface GitHubCommit {
	commit: { tree: { sha: string } };
}

interface GitHubTreeEntry {
	type: string;
	path: string;
}

interface GitHubTree {
	tree: GitHubTreeEntry[];
	truncated: boolean;
}

interface IconManifest {
	generatedAt: string;
	source: string;
	icons: Record<Collection, string[]>;
}

async function githubGet<T>(url: string): Promise<T> {
	const res = await fetch(url, { headers });
	if (!res.ok) {
		const body = await res.text();
		throw new Error(
			`GitHub API error: ${res.status} ${res.statusText} — ${url}\n${body.slice(
				0,
				400
			)}`
		);
	}
	return res.json() as Promise<T>;
}

/**
 * Uses the Git Trees API to recursively fetch all files under packages/icons/
 * and returns the full icon registry, partitioned by collection.
 *
 * The Trees API returns up to 100,000 entries per request and does not have the
 * 1,000-item directory cap that the Contents API has.
 */
async function fetchAllIcons(): Promise<Record<Collection, string[]>> {
	// Step 1 — resolve HEAD commit to get the root tree SHA
	console.log(`Resolving HEAD of ${GITHUB_REPO}@${GITHUB_REF}...`);
	const commitUrl = `https://api.github.com/repos/${GITHUB_REPO}/commits/${GITHUB_REF}`;
	const commit = await githubGet<GitHubCommit>(commitUrl);
	const rootTreeSha = commit.commit.tree.sha;
	console.log(`  Root tree SHA: ${rootTreeSha}`);

	// Step 2 — fetch the full tree recursively
	const treeUrl = `https://api.github.com/repos/${GITHUB_REPO}/git/trees/${rootTreeSha}?recursive=1`;
	console.log(`Fetching recursive tree (this may take a moment)...`);
	const tree = await githubGet<GitHubTree>(treeUrl);

	if (tree.truncated) {
		console.warn(
			"⚠  Tree was truncated by GitHub API — some icons may be missing."
		);
	} else {
		console.log(`  Retrieved ${tree.tree.length} tree entries (not truncated)`);
	}

	// Step 3 — filter for packages/icons/{collection}/*.tsx and extract icon IDs
	const icons: Record<Collection, string[]> = Object.fromEntries(
		COLLECTIONS.map((c) => [c, [] as string[]])
	) as Record<Collection, string[]>;
	const collectionSet = new Set<string>(COLLECTIONS);
	const tsvRe = /^packages\/icons\/([^/]+)\/([^/]+)\.tsx$/;

	for (const entry of tree.tree) {
		if (entry.type !== "blob") continue;
		const m = entry.path.match(tsvRe);
		if (!m) continue;
		const [, collection, filename] = m;
		if (!collectionSet.has(collection)) continue;
		if (filename === "index") continue;
		icons[collection as Collection].push(filename);
	}

	for (const collection of COLLECTIONS) {
		icons[collection].sort();
	}

	return icons;
}

async function main(): Promise<void> {
	const icons = await fetchAllIcons();

	const total = Object.values(icons).reduce((sum, arr) => sum + arr.length, 0);
	for (const [c, arr] of Object.entries(icons)) {
		console.log(`  ${c}: ${arr.length} icons`);
	}
	console.log(`Total: ${total} icons across ${COLLECTIONS.length} collections`);

	const manifest: IconManifest = {
		generatedAt: new Date().toISOString(),
		source: `https://github.com/${GITHUB_REPO}/tree/${GITHUB_REF}/${ICONS_BASE_PATH}`,
		icons,
	};

	await mkdir(dirname(OUTPUT_PATH), { recursive: true });
	await writeFile(
		OUTPUT_PATH,
		JSON.stringify(manifest, null, 2) + "\n",
		"utf8"
	);
	console.log(`\nWrote: ${OUTPUT_PATH}`);
}

main().catch((err) => {
	console.error(err);
	process.exit(1);
});
