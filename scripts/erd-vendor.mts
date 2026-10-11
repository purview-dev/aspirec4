// Bundles the ER viewer's JavaScript dependencies.
//
// Two modes:
//   bun scripts/erd-vendor.mts <output-directory>   write plain files into <dir>/vendor
//   bun scripts/erd-vendor.mts --embed              write gzipped files into the library's assets,
//                                                   where they are embedded into the package
//
// The embedded copies are what make a generated diagram work with no setup step. Re-run --embed to
// pick up a dependency bump; the result is committed alongside the code that loads it.
//
// They are genuinely bundled rather than fetched: requesting ESM modules from a CDN returns
// re-export stubs pointing at absolute CDN paths, which only appear to work.
import { gzipSync } from "node:zlib";
import { mkdir, copyFile, readFile, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";

const embed = process.argv[2] === "--embed";
const target = embed
	? join(import.meta.dir, "..", "src", "src", "EntityFrameworkCore", "Assets", "viewer", "vendor")
	: process.argv[2]
		? join(process.argv[2], "vendor")
		: null;

if (!target) {
	console.error("usage: bun scripts/erd-vendor.mts <output-directory>|--embed");
	process.exit(1);
}

await mkdir(target, { recursive: true });

/** Bundles an entry module, sharing React so React Flow does not get a second copy of it. */
async function bundle(name: string, source: string): Promise<Uint8Array> {
	const entry = join(import.meta.dir, `.erd-${name}-entry.ts`);

	await writeFile(entry, source, "utf8");

	const result = await Bun.build({
		entrypoints: [entry],
		target: "browser",
		format: "esm",
		minify: true,
		define: { "process.env.NODE_ENV": '"production"' },
	});

	await rm(entry, { force: true });

	if (!result.success) {
		for (const log of result.logs) console.error(log);
		process.exit(1);
	}

	return new Uint8Array(await result.outputs[0].arrayBuffer());
}

const viewer = await bundle(
	"viewer",
	`export { default as React, useCallback, useEffect, useMemo, useState } from "react";
export { createRoot } from "react-dom/client";
export {
	Background,
	Controls,
	Handle,
	MiniMap,
	Position,
	ReactFlow,
	ReactFlowProvider,
	useEdgesState,
	useNodesState,
} from "@xyflow/react";
export { default as ELK } from "elkjs/lib/elk.bundled.js";
`,
);

// Mermaid is bundled separately: only the Mermaid preview needs it, and it is roughly three times
// the size of the viewer bundle.
const mermaid = await bundle("mermaid", `export { default } from "mermaid";\n`);

const css = await readFile(join(import.meta.dir, "..", "node_modules", "@xyflow", "react", "dist", "style.css"));

const files: [string, Uint8Array][] = [
	["bundle.js", viewer],
	["mermaid.js", mermaid],
	["reactflow.css", new Uint8Array(css)],
];

for (const [name, content] of files) {
	const payload = embed ? gzipSync(content, { level: 9 }) : content;

	await writeFile(join(target, embed ? `${name}.gz` : name), payload);

	console.log(`${name}${embed ? ".gz" : ""}  ${Math.round(payload.length / 1024)} KB`);
}

console.log(`\nWrote ${files.length} files into ${target}`);
