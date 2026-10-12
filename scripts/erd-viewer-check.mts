// Drives the generated interactive ER viewer in a real browser and asserts it works.
//
// The viewer is the default renderer, so "it compiles" is not enough: ELK layout, node rendering,
// edge animation and search all run only in a browser. Served over HTTP rather than opened from
// file://, because fetch() of the model JSON is blocked under the file:// origin.
import { serve } from "bun";
import { chromium } from "playwright";
import { join } from "node:path";
import { existsSync } from "node:fs";
import { mkdir, readFile, writeFile } from "node:fs/promises";

const model = process.argv[2];

if (!model || !existsSync(model)) {
	console.error("usage: bun scripts/erd-viewer-check.mts <model.json>");
	process.exit(1);
}

// Assembled from the asset sources rather than a previously generated directory, so the check always
// exercises the current viewer rather than whatever the last AppHost run happened to leave behind.
const assets = join(import.meta.dir, "..", "src", "src", "EntityFrameworkCore", "Assets", "viewer");
const dir = join(import.meta.dir, "..", "artifacts", "erd-viewer-check");

await mkdir(dir, { recursive: true });

for (const name of ["index.html", "app.js"]) {
	const text = (await readFile(join(assets, name), "utf8"))
		.replaceAll("__MODEL__", "model.json")
		.replaceAll("__TITLE__", "Viewer check");

	await writeFile(join(dir, name), text, "utf8");
}

await writeFile(join(dir, "model.json"), await readFile(model, "utf8"), "utf8");

if (!existsSync(join(dir, "vendor", "bundle.js"))) {
	console.error(`vendor/bundle.js missing — run: just erd-vendor ${dir}`);
	process.exit(1);
}

const server = serve({
	port: 0,
	async fetch(request) {
		const path = new URL(request.url).pathname;
		const file = Bun.file(join(dir, path === "/" ? "index.html" : decodeURIComponent(path)));

		return (await file.exists()) ? new Response(file) : new Response("not found", { status: 404 });
	},
});

const base = `http://localhost:${server.port}`;
const failures: string[] = [];
const check = (name: string, ok: boolean, detail = "") => {
	console.log(`${ok ? "ok   " : "FAIL "} ${name}${detail ? ` — ${detail}` : ""}`);
	if (!ok) failures.push(name);
};

const browser = await chromium.launch();

try {
	for (const reducedMotion of ["no-preference", "reduce"] as const) {
		const context = await browser.newContext({ reducedMotion, viewport: { width: 1600, height: 1000 } });
		const page = await context.newPage();

		const errors: string[] = [];
		page.on("pageerror", (e) => errors.push(e.message));
		page.on("console", (m) => m.type() === "error" && errors.push(m.text()));

		await page.goto(base, { waitUntil: "networkidle" });

		if (reducedMotion === "no-preference") {
			await page.waitForSelector(".entity", { timeout: 20000 });

			const nodes = await page.locator(".entity").count();
			check("entity nodes render", nodes > 0, `${nodes} nodes`);

			const edges = await page.locator(".react-flow__edge-path").count();
			check("relationship edges render", edges > 0, `${edges} edges`);

			const badges = await page.locator(".entity .badge").count();
			check("key badges render", badges > 0, `${badges} badges`);

			// Cardinality is derived data, so it has to reach the drawing or the richest renderer
			// shows less about a relationship than the Mermaid output does.
			const markers = await page.locator(".react-flow__edge-path").evaluateAll((els) => ({
				starts: els.filter((e) => (e as SVGElement).style.markerStart?.includes("erd-one")).length,
				feet: els.filter((e) => (e as SVGElement).style.markerEnd?.includes("erd-many")).length,
			}));

			check("principal ends marked as one", markers.starts > 0, `${markers.starts} edges`);
			check("dependent ends marked with a crow's foot", markers.feet > 0, `${markers.feet} edges`);

			const defined = await page.locator("svg.erd-markers marker").count();
			check("every marker is defined", defined === 4, `${defined} markers`);

			// A self-reference drawn with the default edge runs behind its own card, leaving only a
			// stub visible. It has to arc clear of the node to read as a relationship at all.
			const selfLoop = await page.evaluate(() => {
				const edge = document.querySelector('[data-id*="categories.ParentCategoryId"]');
				const node = document.querySelector('[data-id="categories"]');
				if (!edge || !node) return null;

				const path = (edge.querySelector("path") as SVGPathElement).getBoundingClientRect();
				return { overhang: Math.round(path.right - node.getBoundingClientRect().right) };
			});

			check(
				"self-referencing edge clears its own card",
				(selfLoop?.overhang ?? 0) > 20,
				`${selfLoop?.overhang ?? "not found"}px beyond the node`,
			);

			// The arc and its label hang into the space beside the card, so the layout has to reserve
			// it. Without that, the label lands on whichever table ELK happened to pack alongside.
			const collisions = await page.evaluate(() => {
				const label = document.querySelector(".self-loop-label")?.getBoundingClientRect();
				if (!label) return null;

				return [...document.querySelectorAll(".react-flow__node .entity")]
					.map((el) => ({ el, box: el.getBoundingClientRect() }))
					.filter(
						({ box }) =>
							box.left < label.right &&
							box.right > label.left &&
							box.top < label.bottom &&
							box.bottom > label.top,
					)
					.map(({ el }) => el.querySelector("header")?.textContent?.split("\n")[0] ?? "?");
			});

			check(
				"self-referencing label overlaps no card",
				collisions !== null && collisions.length === 0,
				collisions === null ? "label not found" : (collisions.join(", ") || "clear of every card"),
			);

			// ELK ran if the nodes are not all stacked at the origin.
			const xs = await page.locator(".react-flow__node").evaluateAll((els) =>
				els.map((e) => (e as HTMLElement).style.transform),
			);
			check("ELK positioned the nodes", new Set(xs).size > 1, `${new Set(xs).size} distinct positions`);

			// Search dims non-matching entities.
			const first = (await page.locator(".entity header").first().innerText()).split("\n")[0];
			await page.fill(".toolbar input", first.slice(0, 4));
			await page.waitForTimeout(300);
			check(
				"search highlights matches",
				(await page.locator(".entity.hit").count()) > 0,
				`matching "${first.slice(0, 4)}"`,
			);
			check("search dims non-matches", (await page.locator(".entity.dimmed").count()) > 0);

			await page.fill(".toolbar input", "");
			await page.waitForTimeout(200);
			await page.screenshot({ path: join(dir, "viewer-check.png"), fullPage: false });

			// A dark capture too, so a styling regression in either theme is visible without a rerun.
			const dark = await browser.newContext({ colorScheme: "dark", viewport: { width: 1600, height: 1000 } });
			const darkPage = await dark.newPage();

			await darkPage.goto(base, { waitUntil: "networkidle" });
			await darkPage.waitForSelector(".entity", { timeout: 20000 });
			await darkPage.waitForTimeout(600);
			await darkPage.screenshot({ path: join(dir, "viewer-check-dark.png") });
			await dark.close();
		}

		// Animation is CSS-driven, so the computed style is the honest check.
		const animation = await page
			.locator(".react-flow__edge.flowing .react-flow__edge-path")
			.first()
			.evaluate((el) => getComputedStyle(el).animationName);

		check(
			reducedMotion === "reduce" ? "animation disabled under reduced motion" : "edges animate",
			reducedMotion === "reduce" ? animation === "none" : animation === "dash",
			`animation-name: ${animation}`,
		);

		check(`no console errors (${reducedMotion})`, errors.length === 0, errors.slice(0, 2).join(" | "));

		await context.close();
	}
} finally {
	await browser.close();
	server.stop(true);
}

console.log(failures.length === 0 ? "\nAll viewer checks passed." : `\n${failures.length} check(s) failed.`);
process.exit(failures.length === 0 ? 0 : 1);
