// Bundles the interactive ER viewer's JavaScript dependencies into <output>/vendor/.
//
// The viewer loads these from disk rather than a CDN so a generated diagram keeps working offline and
// without a server. They are genuinely bundled — an earlier attempt fetched esm.sh modules directly,
// which returned re-export stubs pointing at absolute CDN paths and so only appeared to work.
import { mkdir, copyFile, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";

const target = process.argv[2];

if (!target) {
	console.error("usage: bun scripts/erd-vendor.mts <output-directory>");
	process.exit(1);
}

const vendor = join(target, "vendor");
await mkdir(vendor, { recursive: true });

// A single entry re-exporting everything the viewer needs, so React is shared by React Flow rather
// than duplicated into a second copy (which would break hooks).
//
// Written inside the repository rather than the output directory: bun resolves bare imports against
// the nearest node_modules, and the output directory has none.
const entry = join(import.meta.dir, ".erd-entry.ts");

await writeFile(
	entry,
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
	"utf8",
);

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

const [output] = result.outputs;
await writeFile(join(vendor, "bundle.js"), await output.text(), "utf8");

await copyFile(
	join(import.meta.dir, "..", "node_modules", "@xyflow", "react", "dist", "style.css"),
	join(vendor, "reactflow.css"),
);

const kb = Math.round((await output.text()).length / 1024);
console.log(`Vendored bundle.js (${kb} KB) and reactflow.css into ${vendor}`);
