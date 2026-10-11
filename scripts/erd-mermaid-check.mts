// Drives the generated Mermaid preview in a real browser and asserts it renders.
//
// The preview is a secondary path — Mermaid's job is publishing, where GitHub and most wikis render
// it themselves — so this checks the essentials only: the diagram renders, and pan/zoom responds.
import { serve } from "bun";
import { chromium } from "playwright";
import { join } from "node:path";
import { existsSync } from "node:fs";
import { mkdir, readFile, writeFile } from "node:fs/promises";

const assets = join(import.meta.dir, "..", "src", "src", "EntityFrameworkCore", "Assets", "mermaid");
const dir = join(import.meta.dir, "..", "artifacts", "erd-mermaid-check");

await mkdir(dir, { recursive: true });

if (!existsSync(join(dir, "vendor", "mermaid.js"))) {
	console.error(`vendor/mermaid.js missing — run: bun scripts/erd-vendor.mts ${dir}`);
	process.exit(1);
}

for (const name of ["index.html", "mermaid-app.js"]) {
	const text = (await readFile(join(assets, name), "utf8"))
		.replaceAll("__MODEL__", "schema.mmd")
		.replaceAll("__TITLE__", "Mermaid preview check");

	await writeFile(join(dir, name), text, "utf8");
}

// Mirrors what the emitter produces: subgraphs, aliases, key roles, a nullable type, a precision
// type with the comma rewritten, an attribute comment and a styled context.
const source =
	process.argv[2] && existsSync(process.argv[2])
		? await readFile(process.argv[2], "utf8")
		: `erDiagram
	classDef SalesDbContext fill:#dbeafe,stroke:#3b82f6
	subgraph SalesDbContext
		Orders["Order"] {
			int Id PK
			decimal(18_2) Total "Gross, including tax"
			datetimeoffset? ShippedAt
		}
		OrderLines["OrderLine"] {
			int OrderId PK, FK
			varchar(64) Sku UK
		}
	end
	Orders ||--|{ OrderLines : "Order"
	Orders:::SalesDbContext
`;

await writeFile(join(dir, "schema.mmd"), source, "utf8");

const server = serve({
	port: 0,
	async fetch(request) {
		const path = new URL(request.url).pathname;
		const file = Bun.file(join(dir, path === "/" ? "index.html" : decodeURIComponent(path)));

		return (await file.exists()) ? new Response(file) : new Response("not found", { status: 404 });
	},
});

const failures: string[] = [];
const check = (name: string, ok: boolean, detail = "") => {
	console.log(`${ok ? "ok   " : "FAIL "} ${name}${detail ? ` — ${detail}` : ""}`);
	if (!ok) failures.push(name);
};

const browser = await chromium.launch();

try {
	const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });

	const errors: string[] = [];
	page.on("pageerror", (e) => errors.push(e.message));
	page.on("console", (m) => m.type() === "error" && errors.push(m.text()));

	await page.goto(`http://localhost:${server.port}`, { waitUntil: "networkidle" });
	await page.waitForSelector("#canvas svg", { timeout: 20000 });

	check("diagram renders", (await page.locator("#canvas svg").count()) === 1);
	check("error panel hidden", await page.locator("#error").isHidden());

	const entities = await page.locator("#canvas svg .er.entityBox, #canvas svg [id*='entity']").count();
	check("entities present in the svg", entities > 0, `${entities} matched`);

	// textContent rather than innerText: the node is an SVGElement, not an HTMLElement.
	const text = await page.locator("#canvas svg").evaluate((el) => el.textContent ?? "");
	check("attribute comment rendered", text.includes("Gross"), "comment column");

	// Zoom via wheel, then confirm the transform changed.
	const before = await page.locator("#canvas").evaluate((el) => (el as HTMLElement).style.transform);
	await page.mouse.move(700, 450);
	await page.mouse.wheel(0, -300);
	await page.waitForTimeout(150);
	const after = await page.locator("#canvas").evaluate((el) => (el as HTMLElement).style.transform);

	check("zoom changes the transform", before !== after);
	check("no console errors", errors.length === 0, errors.slice(0, 2).join(" | "));

	await page.screenshot({ path: join(dir, "mermaid-check.png") });
} finally {
	await browser.close();
	server.stop(true);
}

console.log(failures.length === 0 ? "\nAll Mermaid preview checks passed." : `\n${failures.length} check(s) failed.`);
process.exit(failures.length === 0 ? 0 : 1);
