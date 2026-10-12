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

const descriptor = await readFile(model, "utf8");

await writeFile(join(dir, "model.json"), descriptor, "utf8");

// A column name no table name contains, so searching for it can only match through a column. Taken
// from the model rather than hard-coded, so the check travels with whatever schema it is given.
const modelTables = (JSON.parse(descriptor).contexts ?? []).flatMap((c: any) => c.tables ?? []);
const columnOnly: string =
	modelTables
		.flatMap((t: any) => (t.columns ?? []).map((c: any) => c.name as string))
		.find(
			(name: string) =>
				name.length > 4 &&
				!modelTables.some((t: any) => t.name.toLowerCase().includes(name.toLowerCase())),
		) ?? "";

if (!columnOnly) {
	console.error("no column name is distinct from every table name in this model — cannot check column search");
	process.exit(1);
}

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

			// ELK sizes a container around boxes it was told the size of, so a card that renders
			// bigger than its box is paid for out of the gaps and the container's own padding. These
			// two assert the result rather than the estimate, so any future drift shows up here.
			const spacing = await page.evaluate(() => {
				// The view is zoomed to fit, so screen pixels are not layout pixels. Everything below
				// is divided back out by the viewport scale to compare against the configured spacing.
				const viewport = document.querySelector<HTMLElement>(".react-flow__viewport")!;
				const scale = viewport.getBoundingClientRect().width / viewport.offsetWidth;

				// React Flow renders child nodes as DOM siblings of their container, not inside it, so
				// parentage has to come from the geometry.
				const cards = [...document.querySelectorAll<HTMLElement>(".react-flow__node-entity")].map((n) => ({
					id: n.dataset.id ?? "?",
					box: n.getBoundingClientRect(),
					parent: "",
				}));

				const containers = [...document.querySelectorAll<HTMLElement>(".react-flow__node-context")].map(
					(c) => {
						const id = c.dataset.id ?? "?";
						const rect = c.getBoundingClientRect();
						const own = cards.filter(
							(n) =>
								n.box.left >= rect.left &&
								n.box.right <= rect.right &&
								n.box.top >= rect.top &&
								n.box.bottom <= rect.bottom,
						);

						for (const n of own) n.parent = id;

						return {
							id,
							count: own.length,
							pad: Math.round(Math.min(...own.map((n) => rect.bottom - n.box.bottom)) / scale),
						};
					},
				);

				// Only cards that share horizontal space can crowd each other vertically.
				let closest = { pair: "none", gap: Number.POSITIVE_INFINITY };
				for (const a of cards)
					for (const b of cards) {
						if (a === b || a.parent !== b.parent || b.box.top < a.box.bottom) continue;
						if (a.box.left >= b.box.right || a.box.right <= b.box.left) continue;

						const gap = Math.round((b.box.top - a.box.bottom) / scale);
						if (gap < closest.gap) closest = { pair: `${a.id} → ${b.id}`, gap };
					}

				return { containers, closest };
			});

			const tightest = spacing.containers.reduce((a, b) => (b.pad < a.pad ? b : a));
			const empty = spacing.containers.filter((c) => c.count === 0);

			check(
				"every context has padding below its last table",
				empty.length === 0 && tightest.pad >= 20,
				empty.length > 0
					? `no tables matched ${empty.map((c) => c.id).join(", ")}`
					: `${tightest.id} has ${tightest.pad}px`,
			);

			check(
				"stacked tables stay visually separate",
				spacing.closest.gap >= 40,
				`${spacing.closest.pair} — ${spacing.closest.gap}px`,
			);

			// ELK ran if the nodes are not all stacked at the origin.
			const xs = await page.locator(".react-flow__node").evaluateAll((els) =>
				els.map((e) => (e as HTMLElement).style.transform),
			);
			check("ELK positioned the nodes", new Set(xs).size > 1, `${new Set(xs).size} distinct positions`);

			// The toolbar floats over the canvas, so the initial fit has to leave room for it. It is
			// also the thing most likely to grow, and it hides a container's label when it does.
			const covered = await page.evaluate(() => {
				const bar = document.querySelector(".toolbar")!.getBoundingClientRect();

				return [...document.querySelectorAll(".react-flow__node")]
					.map((n) => ({ id: (n as HTMLElement).dataset.id ?? "?", box: n.getBoundingClientRect() }))
					// Container labels ride above the top border of their node.
					.filter(
						({ box }) =>
							box.left < bar.right &&
							box.right > bar.left &&
							box.top - 18 < bar.bottom &&
							box.bottom > bar.top,
					)
					.map(({ id }) => id);
			});

			check("the toolbar covers nothing on the initial fit", covered.length === 0, covered.join(", "));

			// ── Search ────────────────────────────────────────────────────────────────────────
			//
			// Search is the only way into a schema too large to read at a glance, so it is checked as
			// a working tool — marks, counter, navigation and the ways out of it — not just as a
			// class that lands on an element.

			const first = (await page.locator(".entity header").first().innerText()).split("\n")[0];
			const term = first.slice(0, 4);

			await page.fill(".toolbar input", term);
			await page.waitForTimeout(300);

			check("search highlights matches", (await page.locator(".entity.hit").count()) > 0, `matching "${term}"`);
			check("search dims non-matches", (await page.locator(".entity.dimmed").count()) > 0);
			check(
				"search dims relationships to dimmed tables",
				(await page.locator(".react-flow__edge.dimmed").count()) > 0,
			);

			const counter = await page.locator(".search .matches").innerText();
			check("search reports its position in the matches", /^\d+\/\d+$/.test(counter), counter);

			// A query that only a column can satisfy: the table has to match on it, and the row that
			// caused the match has to say so, or the reader is left hunting for it by eye.
			await page.fill(".toolbar input", columnOnly);
			await page.waitForTimeout(300);

			check(
				"a column-only match marks the column",
				(await page.locator(".entity .col.hit").count()) > 0,
				`matching "${columnOnly}"`,
			);

			// Enter travels: first to the match the counter points at, then onward through the rest.
			await page.fill(".toolbar input", term);
			await page.waitForTimeout(300);

			const total = Number(((await page.locator(".search .matches").innerText()).split("/")[1] ?? "0").trim());
			const viewportBefore = await page.locator(".react-flow__viewport").getAttribute("style");

			await page.locator(".toolbar input").press("Enter");
			await page.waitForTimeout(700);

			check(
				"Enter brings a match into view",
				(await page.locator(".react-flow__viewport").getAttribute("style")) !== viewportBefore,
			);
			check("exactly one match is current", (await page.locator(".entity.current").count()) === 1);

			if (total > 1) {
				await page.locator(".toolbar input").press("Enter");
				await page.waitForTimeout(500);

				const advanced = await page.locator(".search .matches").innerText();
				check("Enter advances through the matches", advanced.startsWith("2/"), advanced);
			}

			await page.fill(".toolbar input", "zzzzqqqq");
			await page.waitForTimeout(250);
			check("an empty result says so", (await page.locator(".search .matches.none").count()) === 1);

			await page.locator(".toolbar input").press("Escape");
			await page.waitForTimeout(250);
			check(
				"Escape clears the search",
				(await page.inputValue(".toolbar input")) === "" &&
					(await page.locator(".entity.dimmed").count()) === 0,
			);

			// The field is reachable without the mouse, from wherever the reader happens to be.
			await page.locator("body").click({ position: { x: 900, y: 900 } });
			await page.keyboard.press("/");
			await page.waitForTimeout(150);
			check(
				"'/' focuses the search field",
				await page.evaluate(() => document.activeElement?.tagName === "INPUT"),
			);

			await page.screenshot({ path: join(dir, "viewer-check.png"), fullPage: false });

			// ── Image export ──────────────────────────────────────────────────────────────────
			//
			// The export is the diagram at native size, not a capture of the window, so both the
			// file arriving and it being bigger than the viewport are part of the contract.

			for (const format of ["svg", "png"] as const) {
				const [download] = await Promise.all([
					page.waitForEvent("download", { timeout: 30000 }),
					page.click(`[data-export="${format}"]`),
				]);

				const file = await download.path();
				const bytes = file ? (await readFile(file)).length : 0;

				check(
					`${format.toUpperCase()} export downloads a file`,
					bytes > 10_000 && download.suggestedFilename().endsWith(`.${format}`),
					`${download.suggestedFilename()}, ${Math.round(bytes / 1024)} KB`,
				);

				await writeFile(join(dir, `viewer-export.${format}`), await readFile(file!));

				if (format === "svg") {
					const svg = await readFile(file!, "utf8");

					// The crow's-foot markers are defined outside the captured subtree, so without a
					// copy riding along every url(#erd-…) in the export resolves to nothing.
					check("SVG export carries the cardinality markers", svg.includes('id="erd-many"'));
					check("SVG export carries the tables", svg.includes(first));
				}
			}

			// ── Theme ─────────────────────────────────────────────────────────────────────────

			check(
				"no theme is pinned by default",
				(await page.evaluate(() => document.documentElement.dataset.theme)) === undefined,
			);

			const background = () =>
				page.evaluate(() => getComputedStyle(document.body).backgroundColor);

			// Luminance, so the assertion is "this is the dark theme" rather than an exact hex that
			// any palette change would have to be chased through.
			const luminance = async () => {
				const [r, g, b] = (await background()).match(/\d+/g)!.map(Number);

				return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
			};

			await page.click('[data-theme-option="dark"]');
			await page.waitForTimeout(200);

			check(
				"dark is applied on demand",
				(await page.evaluate(() => document.documentElement.dataset.theme)) === "dark" &&
					(await luminance()) < 0.3,
				await background(),
			);
			check(
				"React Flow's own chrome follows the theme",
				(await page.locator(".react-flow.dark").count()) === 1,
			);

			await page.screenshot({ path: join(dir, "viewer-check-dark.png") });

			// Reloading is where an unpersisted choice shows up, and where a choice applied only by
			// the module shows up as a flash of the wrong theme before it loads.
			await page.reload({ waitUntil: "networkidle" });
			await page.waitForSelector(".entity", { timeout: 20000 });

			check(
				"the choice survives a reload",
				(await page.evaluate(() => document.documentElement.dataset.theme)) === "dark" &&
					(await luminance()) < 0.3,
			);
			check(
				"the dark button is still the pressed one",
				(await page.locator('[data-theme-option="dark"][aria-pressed="true"]').count()) === 1,
			);

			await page.click('[data-theme-option="auto"]');
			await page.waitForTimeout(200);

			check(
				"auto returns to the system theme",
				(await page.evaluate(() => document.documentElement.dataset.theme)) === undefined &&
					(await luminance()) > 0.7,
				await background(),
			);

			// The pin has to beat the operating system in both directions, so the opposite case is
			// checked against a context that reports a dark desktop.
			const darkDesktop = await browser.newContext({
				colorScheme: "dark",
				viewport: { width: 1600, height: 1000 },
			});
			const darkPage = await darkDesktop.newPage();

			await darkPage.goto(base, { waitUntil: "networkidle" });
			await darkPage.waitForSelector(".entity", { timeout: 20000 });

			const darkDesktopLuminance = async () => {
				const [r, g, b] = (
					await darkPage.evaluate(() => getComputedStyle(document.body).backgroundColor)
				).match(/\d+/g)!.map(Number);

				return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
			};

			check("a dark desktop gets the dark theme unasked", (await darkDesktopLuminance()) < 0.3);

			await darkPage.click('[data-theme-option="light"]');
			await darkPage.waitForTimeout(200);

			check("light overrides a dark desktop", (await darkDesktopLuminance()) > 0.7);

			await darkDesktop.close();
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
