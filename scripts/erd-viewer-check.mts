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

/** Opens the viewer's settings popover, if it is not already open. */
const openSettings = async (page: any) => {
	if ((await page.locator(".settings-panel").count()) === 0) {
		await page.click(".settings > button");
	}
};

/** Picks one option from a segmented control, e.g. the toolbar's View → Names. */
const chooseIn = async (page: any, group: string, option: string) => {
	await page.locator(`${group} button`, { hasText: option }).first().click();
};

/** Picks one option from a settings row, e.g. Theme → Dark, opening the popover if needed. */
const choose = async (page: any, group: string, option: string) => {
	await openSettings(page);
	await chooseIn(page, `.settings-panel [aria-label="${group}"]`, option);
};

/** The toolbar's View control, which is not behind the settings popover. */
const VIEW = '[aria-label="View"]';

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

			// The gap between stacked tables is where the relationship labels sit, so it has to clear a
			// label rather than merely separate two borders. Tables no relationship connects are laid
			// out as separate components and packed, a seam that needs its own spacing: at ELK's default
			// of 20 those groups all but touch.
			check(
				"stacked tables stay visually separate",
				spacing.closest.gap >= 100,
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

			// ── Auto-sizing and remembered arrangement ────────────────────────────────────────

			// A card is measured from its content, so nothing is truncated: a long column name or
			// store type widens its own card instead of being cut off.
			const truncated = await page.locator(".entity .col-name").evaluateAll((els) =>
				els
					.filter((e) => (e as HTMLElement).scrollWidth > (e as HTMLElement).clientWidth + 1)
					.map((e) => e.textContent ?? "?"),
			);

			check("no column name is truncated", truncated.length === 0, truncated.slice(0, 3).join(", "));

			// A table's offset within its context box, which is what a remembered arrangement has to
			// preserve — and what a reload must reproduce.
			//
			// Divided back out by the viewport scale, so the result is in layout pixels: the view is
			// zoomed to fit, so a screen-pixel offset shrinks as the schema grows and any threshold
			// against it would hold only for diagrams small enough to render at 1:1.
			const offsetInContext = (target: any, id: string) =>
				target.evaluate((wanted: string) => {
					const node = document.querySelector(`.react-flow__node-entity[data-id="${wanted}"]`);
					if (!node) return null;

					const n = (node as HTMLElement).getBoundingClientRect();
					const context = [...document.querySelectorAll(".react-flow__node-context")].find((c) => {
						const box = (c as HTMLElement).getBoundingClientRect();

						return (
							n.left >= box.left - 1 &&
							n.right <= box.right + 1 &&
							n.top >= box.top - 1 &&
							n.bottom <= box.bottom + 1
						);
					});

					if (!context) return null;

					const viewport = document.querySelector<HTMLElement>(".react-flow__viewport")!;
					const scale = viewport.getBoundingClientRect().width / viewport.offsetWidth;
					const box = (context as HTMLElement).getBoundingClientRect();

					return {
						x: Math.round((n.left - box.left) / scale),
						y: Math.round((n.top - box.top) / scale),
					};
				}, id);

			// Deliberately not the topmost table in its container: fitContexts pins a container's top to
			// whichever table is highest, so dragging that one carries the whole container with it and
			// its offset never changes — which would leave the reload check below passing whether or not
			// anything was actually remembered.
			const draggedId = (await page.evaluate(() => {
				const cards = [...document.querySelectorAll<HTMLElement>(".react-flow__node-entity")].map((n) => ({
					id: n.dataset.id ?? "?",
					box: n.getBoundingClientRect(),
				}));

				for (const container of document.querySelectorAll<HTMLElement>(".react-flow__node-context")) {
					const rect = container.getBoundingClientRect();
					const own = cards.filter(
						(n) =>
							n.box.left >= rect.left &&
							n.box.right <= rect.right &&
							n.box.top >= rect.top &&
							n.box.bottom <= rect.bottom,
					);

					if (own.length < 2) continue;

					return own.reduce((lowest, n) => (n.box.top > lowest.box.top ? n : lowest)).id;
				}

				return null;
			}))!;

			check("a container holds more than one table to drag", draggedId !== null, draggedId ?? "none found");

			const card = page.locator(`.react-flow__node-entity[data-id="${draggedId}"]`);
			const origin = await offsetInContext(page, draggedId);
			const start = (await card.boundingBox())!;

			await page.mouse.move(start.x + 40, start.y + 12);
			await page.mouse.down();
			await page.mouse.move(start.x + 40, start.y + 170, { steps: 12 });
			await page.mouse.up();
			await page.waitForTimeout(250);

			const before = await offsetInContext(page, draggedId);

			check(
				"a table can be dragged",
				origin !== null && before !== null && before.y - origin.y > 40,
				`offset ${origin?.y ?? "?"}px → ${before?.y ?? "?"}px in its context`,
			);

			check(
				"the context box follows the table it holds",
				before !== null,
				before === null ? `table ${draggedId} left its context` : "still inside",
			);

			await page.reload({ waitUntil: "networkidle" });
			await page.waitForSelector(".entity", { timeout: 20000 });

			const after = await offsetInContext(page, draggedId);

			check(
				"the arrangement survives a reload",
				before !== null && after !== null && Math.abs(after.y - before.y) < 6,
				`${before?.y ?? "?"}px → ${after?.y ?? "?"}px`,
			);

			// ── Table display ─────────────────────────────────────────────────────────────────

			const columnRows = await page.locator(".entity .col").count();

			check("columns mode shows every column", columnRows > 0, `${columnRows} rows`);

			await chooseIn(page, VIEW, "Names");
			await page.waitForTimeout(500);

			const collapsed = await page.locator(".entity .col").count();
			const headers = await page.locator(".entity header").count();

			check(
				"names mode collapses a table to its header",
				collapsed === 0 && headers > 0,
				`${headers} headers, ${collapsed} rows`,
			);

			await page.reload({ waitUntil: "networkidle" });
			await page.waitForSelector(".entity", { timeout: 20000 });

			check(
				"the table display is remembered",
				(await page.locator(".entity .col").count()) === 0,
				"still collapsed after reload",
			);

			await chooseIn(page, VIEW, "Columns");
			await page.waitForTimeout(500);

			check("columns mode restores the columns", (await page.locator(".entity .col").count()) > 0);

			// ── Complex value objects ─────────────────────────────────────────────────────────
			//
			// EF flattens a complex value object into one column per member, named {Property}_{Member},
			// so a card otherwise shows a run of prefixed rows that says nothing about what they are.
			// They are folded under the property that owns them, which only means anything if the fold
			// opens, closes, survives a reload, and does not disturb the layout around it.

			const groups = await page.locator(".entity .col.group").count();

			if (groups === 0) {
				check("the model flattens a complex value object", false, "no group rows — nothing to check");
			} else {
				const group = page.locator(".entity .col.group").first();
				const groupCard = page.locator(".react-flow__node-entity", { has: page.locator(".col.group") }).first();
				const groupCardId = (await groupCard.getAttribute("data-id"))!;

				check("complex value objects are folded under their property", groups > 0, `${groups} group rows`);

				check(
					"a folded group starts closed",
					(await page.locator(".entity .col.member").count()) === 0 &&
						(await page.locator(".entity .col.group.open").count()) === 0,
				);

				// The count is what a closed group says about what it is holding: without it the row
				// claims a value object and gives no reason to open it.
				const closedLabel = await group.locator(".type").innerText();

				check("a closed group reports how many members it holds", /\(\d+\)$/.test(closedLabel), closedLabel);

				const widthBefore = (await groupCard.boundingBox())!.width;
				const heightBefore = (await groupCard.boundingBox())!.height;

				await group.locator(".chevron").click();
				await page.waitForTimeout(350);

				const members = await page.locator(".entity .col.member").count();

				check("the chevron opens one group", members > 0, `${members} member rows`);

				const openLabel = await group.locator(".type").innerText();

				check(
					"an open group drops the count",
					!/\(\d+\)$/.test(openLabel) && openLabel.length > 0,
					openLabel,
				);

				// A member of an open group is shown as its database type alone: the group row above it
				// already names the value object, and repeating it on every member crowds out the one
				// thing those rows exist to say.
				const repeated = await page
					.locator(".entity .col.member .type")
					.evaluateAll((els, name) => els.map((e) => e.textContent ?? "").filter((t) => t.includes(name)), openLabel);

				check(
					"a member does not repeat its group's value object",
					repeated.length === 0,
					repeated.slice(0, 2).join(", ") || `members read as plain types beside ${openLabel}`,
				);

				// Members are indented so the card still reads as a list of the table's own columns.
				const indented = await page.evaluate(() => {
					const member = document.querySelector<HTMLElement>(".entity .col.member .col-name");
					const column = document.querySelector<HTMLElement>(".entity .col:not(.member):not(.group) .col-name");
					if (!member || !column) return null;

					return Math.round(member.getBoundingClientRect().left - column.getBoundingClientRect().left);
				});

				check("members are indented beneath their property", (indented ?? 0) > 0, `${indented ?? "?"}px`);

				const opened = (await groupCard.boundingBox())!;

				// The card is measured as though every group were open, so opening one only ever changes
				// its height. A width that moved would mean the arrangement around it has to shift too.
				check(
					"opening a group does not re-widen the card",
					Math.abs(opened.width - widthBefore) < 2,
					`${Math.round(widthBefore)}px → ${Math.round(opened.width)}px`,
				);

				check(
					"opening a group makes room for the members",
					opened.height > heightBefore + 2,
					`${Math.round(heightBefore)}px → ${Math.round(opened.height)}px`,
				);

				// The open set rides with the saved arrangement, so a reload reopens what the reader left
				// open rather than folding the card back up under them.
				await page.reload({ waitUntil: "networkidle" });
				await page.waitForSelector(".entity", { timeout: 20000 });

				check(
					"an opened group survives a reload",
					(await page.locator(".entity .col.member").count()) > 0,
					`${await page.locator(".entity .col.member").count()} member rows`,
				);

				// ── Expand all / Collapse all ─────────────────────────────────────────────────

				const COMPLEX = '[aria-label="Complex value objects"]';

				await chooseIn(page, COMPLEX, "Collapse all");
				await page.waitForTimeout(350);

				check(
					"Collapse all folds every group",
					(await page.locator(".entity .col.member").count()) === 0,
					"no member rows",
				);

				await chooseIn(page, COMPLEX, "Expand all");
				await page.waitForTimeout(350);

				const allOpen = await page.locator(".entity .col.group.open").count();

				check("Expand all opens every group", allOpen === groups, `${allOpen} of ${groups} open`);

				await chooseIn(page, COMPLEX, "Collapse all");
				await page.waitForTimeout(350);

				// ── Search reaches a folded member ────────────────────────────────────────────
				//
				// A hit inside a closed group would otherwise be counted and then not shown. The groups
				// opened this way are held apart from the reader's own, so clearing the search folds
				// them back rather than leaving the card as the search left it.

				const memberColumn = await page.evaluate((id) => {
					const card = document.querySelector(`.react-flow__node-entity[data-id="${id}"]`);
					const name = card?.querySelector(".col.group .col-name")?.textContent ?? "";

					return name ? `${name}_` : "";
				}, groupCardId);

				if (memberColumn) {
					// Searching the flattened column's own prefix can only be satisfied by a folded member.
					await page.fill(".toolbar input", memberColumn);
					await page.waitForTimeout(400);

					check(
						"a search match inside a folded group opens it",
						(await page.locator(".entity .col.member").count()) > 0,
						`searching "${memberColumn}"`,
					);

					check(
						"the revealed member is marked as the hit",
						(await page.locator(".entity .col.member.hit").count()) > 0,
					);

					await page.locator(".toolbar input").press("Escape");
					await page.waitForTimeout(400);

					check(
						"clearing the search folds it back",
						(await page.locator(".entity .col.member").count()) === 0,
						"back to closed",
					);
				}
			}

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

			await choose(page, "Theme", "Dark");
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
			await openSettings(page);

			check(
				"the dark button is still the pressed one",
				(
					await page.locator('.settings-panel [aria-label="Theme"] button[aria-pressed="true"]').innerText()
				) === "Dark",
			);

			await choose(page, "Theme", "Auto");
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

			await choose(darkPage, "Theme", "Light");
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
