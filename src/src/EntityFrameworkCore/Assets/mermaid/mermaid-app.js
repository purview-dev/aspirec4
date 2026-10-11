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

const dark = globalThis.matchMedia?.("(prefers-color-scheme: dark)").matches ?? false;

mermaid.initialize({
	startOnLoad: false,
	// Mermaid 12 defaults ER diagrams to the neo look and redux-color theme; only the light/dark
	// choice is overridden, so the diagram matches the surrounding page.
	theme: dark ? "dark" : "default",
});

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

try {
	const response = await fetch(SOURCE_URL);

	if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);

	const { svg } = await mermaid.render("erd", await response.text());

	canvas.innerHTML = svg;
	fit();
} catch (error) {
	errorBox.hidden = false;
	errorBox.textContent =
		`Could not render ${SOURCE_URL}:\n${error.message}\n\n` +
		"Opening this file directly from disk may be blocked by the browser's file:// restrictions — " +
		"serve the folder over HTTP instead.";
}
