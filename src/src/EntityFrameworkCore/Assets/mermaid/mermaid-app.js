// Local preview for the emitted Mermaid diagram.
//
// Deliberately minimal. Mermaid's role here is publishing — GitHub, Azure DevOps and most wikis
// render it natively — so this exists to eyeball the output before pasting it somewhere, not to be
// the primary experience. For that, use the interactive renderer, which reads the model directly and
// loses nothing.
//
// Mermaid is loaded from ./vendor/ rather than a CDN so the preview works offline. Populate that
// directory with `just erd-vendor`.
import mermaid from "./vendor/mermaid.js";

const SOURCE_URL = "__MODEL__";

const stage = document.getElementById("stage");
const canvas = document.getElementById("canvas");
const errorBox = document.getElementById("error");

/*
 * Theme. The same three choices and the same stored key as the interactive viewer, so a reader who
 * pinned one gets it in both. "auto" is the absence of a pin: the stylesheet leaves :root at
 * `color-scheme: light dark` and every light-dark() token follows the operating system.
 *
 * Mermaid bakes its colours into the SVG at render time rather than reading CSS, so unlike the
 * viewer's own chrome the diagram has to be rendered again when the theme changes.
 */
const THEMES = ["auto", "light", "dark"];
const THEME_KEY = "aspirec4-erd-theme";

const systemDark = globalThis.matchMedia?.("(prefers-color-scheme: dark)");

const stored = () => {
	try {
		const value = localStorage.getItem(THEME_KEY);

		return THEMES.includes(value) ? value : "auto";
	} catch {
		return "auto";
	}
};

let theme = stored();

const isDark = () => (theme === "auto" ? (systemDark?.matches ?? false) : theme === "dark");

const view = { x: 0, y: 0, scale: 1 };

const apply = () => {
	canvas.style.transform = `translate(${view.x}px, ${view.y}px) scale(${view.scale})`;
};

const fit = () => {
	const svg = canvas.querySelector("svg");
	if (!svg) return;

	const box = svg.getBoundingClientRect();
	if (box.width === 0 || box.height === 0) return;

	// The SVG is already transformed, so undo the current scale to get its natural size.
	const naturalWidth = box.width / view.scale;
	const naturalHeight = box.height / view.scale;

	view.scale = Math.min((stage.clientWidth - 80) / naturalWidth, (stage.clientHeight - 120) / naturalHeight, 1);
	view.x = (stage.clientWidth - naturalWidth * view.scale) / 2;
	view.y = 80;

	apply();
};

stage.addEventListener(
	"wheel",
	(event) => {
		event.preventDefault();

		const factor = event.deltaY < 0 ? 1.1 : 1 / 1.1;
		const next = Math.min(Math.max(view.scale * factor, 0.1), 8);

		// Keep the point under the cursor fixed while zooming.
		view.x = event.clientX - ((event.clientX - view.x) * next) / view.scale;
		view.y = event.clientY - ((event.clientY - view.y) * next) / view.scale;
		view.scale = next;

		apply();
	},
	{ passive: false },
);

let dragging = null;

stage.addEventListener("pointerdown", (event) => {
	dragging = { x: event.clientX - view.x, y: event.clientY - view.y };
	stage.classList.add("panning");
	stage.setPointerCapture(event.pointerId);
});

stage.addEventListener("pointermove", (event) => {
	if (!dragging) return;

	view.x = event.clientX - dragging.x;
	view.y = event.clientY - dragging.y;

	apply();
});

for (const type of ["pointerup", "pointercancel"]) {
	stage.addEventListener(type, () => {
		dragging = null;
		stage.classList.remove("panning");
	});
}

document.getElementById("fit").addEventListener("click", fit);

document.getElementById("reset").addEventListener("click", () => {
	view.scale = 1;
	view.x = 0;
	view.y = 80;
	apply();
});

// ── Image export ─────────────────────────────────────────────────────────────────────────────────
//
// Mermaid emits a self-contained SVG with its styles inlined, so the export is the SVG it already
// produced rather than a capture of the page. The PNG is that SVG rasterised at twice the size.

/** Hands the browser a file without going near the page's own navigation. */
function save(blob, extension) {
	const href = URL.createObjectURL(blob);
	const link = document.createElement("a");

	link.download = `${(document.title || "erd").replaceAll(/[^\w.-]+/g, "-").toLowerCase()}.${extension}`;
	link.href = href;
	link.click();

	setTimeout(() => URL.revokeObjectURL(href), 30_000);
}

/** The rendered diagram, as standalone SVG markup sized in absolute units. */
function exportSvg() {
	const svg = canvas.querySelector("svg")?.cloneNode(true);
	if (!svg) return null;

	const box = canvas.querySelector("svg").getBBox();

	// Mermaid sizes its SVG to the container with a percentage width, which a rasteriser cannot
	// resolve and an editor opens at the wrong size.
	svg.setAttribute("width", Math.ceil(box.width));
	svg.setAttribute("height", Math.ceil(box.height));
	svg.setAttribute("viewBox", `${box.x} ${box.y} ${box.width} ${box.height}`);
	svg.style.removeProperty("max-width");

	return { markup: new XMLSerializer().serializeToString(svg), width: box.width, height: box.height };
}

async function download(format) {
	const exported = exportSvg();
	if (!exported) return;

	if (format === "svg") {
		save(new Blob([exported.markup], { type: "image/svg+xml" }), "svg");

		return;
	}

	/*
	 * Decoded from a data URL through an <img>.
	 *
	 * Not createImageBitmap, which Chromium declines to use on SVG and fails by never settling, and
	 * not a blob URL, which it treats as foreign enough to taint the canvas — leaving toBlob to
	 * throw a SecurityError after everything else has apparently worked.
	 */
	const image = new Image();

	image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(exported.markup)}`;

	await image.decode();

	const scale = 2;
	const target = document.createElement("canvas");

	target.width = Math.ceil(exported.width * scale);
	target.height = Math.ceil(exported.height * scale);

	const context = target.getContext("2d");

	// SVG has no background of its own, and a transparent PNG reads as unreadable wherever it is
	// pasted onto a surface of the opposite theme.
	context.fillStyle = getComputedStyle(document.body).backgroundColor;
	context.fillRect(0, 0, target.width, target.height);
	context.drawImage(image, 0, 0, target.width, target.height);

	save(await new Promise((resolve) => target.toBlob(resolve, "image/png")), "png");
}

for (const button of document.querySelectorAll("[data-export]")) {
	button.addEventListener("click", () => download(button.dataset.export));
}

// ── Rendering ────────────────────────────────────────────────────────────────────────────────────

let source = null;
let generation = 0;

async function render() {
	mermaid.initialize({
		startOnLoad: false,
		// Mermaid 12 defaults ER diagrams to the neo look and redux-color theme; only the light/dark
		// choice is overridden, so the diagram matches the surrounding page.
		theme: isDark() ? "dark" : "default",
	});

	try {
		source ??= await (async () => {
			const response = await fetch(SOURCE_URL);

			if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);

			return await response.text();
		})();

		// Mermaid refuses to reuse an id, so each pass gets its own.
		const { svg } = await mermaid.render(`erd-${generation++}`, source);

		canvas.innerHTML = svg;
		errorBox.hidden = true;
		fit();
	} catch (error) {
		errorBox.hidden = false;
		errorBox.textContent =
			`Could not render ${SOURCE_URL}:\n${error.message}\n\n` +
			"Opening this file directly from disk may be blocked by the browser's file:// restrictions — " +
			"serve the folder over HTTP instead.";
	}
}

function applyTheme(next) {
	theme = next;

	if (theme === "auto") {
		delete document.documentElement.dataset.theme;
	} else {
		document.documentElement.dataset.theme = theme;
	}

	try {
		localStorage.setItem(THEME_KEY, theme);
	} catch {
		/* the choice still applies to this session */
	}

	for (const button of document.querySelectorAll("[data-theme-option]")) {
		button.setAttribute("aria-pressed", String(button.dataset.themeOption === theme));
	}

	return render();
}

for (const button of document.querySelectorAll("[data-theme-option]")) {
	button.addEventListener("click", () => applyTheme(button.dataset.themeOption));
}

// While following the system, a change to it has to reach the already-rendered diagram.
systemDark?.addEventListener("change", () => theme === "auto" && render());

await applyTheme(theme);
