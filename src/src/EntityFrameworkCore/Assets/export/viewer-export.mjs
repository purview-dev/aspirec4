// Renders the generated interactive ER viewer in a real browser and saves the image it produces.
//
// The viewer is a React Flow application, so the only way to get an image that matches what a reader
// sees is to let the viewer produce it: this calls the very function the toolbar's Save as SVG/PNG
// control calls. A d2 render, or a screenshot of the window, is a different picture.
//
// Driven over the Chrome DevTools Protocol using nothing but Node's own APIs. The Playwright image
// ships the browsers but not the `playwright` package, and installing one at export time would mean a
// network fetch on a path that is meant to be hermetic.
//
// Invoked as:  node viewer-export.mjs <formats-csv> <base-name>
// with the viewer mounted read-only at /data and the destination at /out.
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdtemp, readdir, readFile, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { extname, join, normalize } from "node:path";

const VIEWER = "/data";
const OUTPUT = "/out";
const BROWSERS = "/ms-playwright";

const formats = (process.argv[2] ?? "svg").split(",").filter(Boolean);
const baseName = process.argv[3] ?? "schema";

const TYPES = {
	".html": "text/html; charset=utf-8",
	".js": "text/javascript; charset=utf-8",
	".mjs": "text/javascript; charset=utf-8",
	".json": "application/json; charset=utf-8",
	".css": "text/css; charset=utf-8",
	".svg": "image/svg+xml",
};

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Locates the chromium the image shipped, whatever revision it happens to be. */
async function findChrome() {
	for (const entry of await readdir(BROWSERS)) {
		if (entry.startsWith("chromium-")) {
			return join(BROWSERS, entry, "chrome-linux", "chrome");
		}
	}

	throw new Error(`No chromium found under ${BROWSERS}.`);
}

/**
 * Serves the viewer over HTTP.
 *
 * Not opened from file://, because the viewer fetches its model JSON and that fetch is blocked under
 * the file:// origin. The server is local to this container, so nothing is exposed anywhere.
 */
async function serveViewer() {
	const server = createServer(async (request, response) => {
		try {
			const url = new URL(request.url ?? "/", "http://127.0.0.1");
			const relative =
				url.pathname === "/" ? "index.html" : decodeURIComponent(url.pathname).replace(/^\/+/, "");
			const path = join(VIEWER, normalize(relative));

			// normalize() collapses any "..", so a path that still escapes the root was trying to.
			if (!path.startsWith(VIEWER)) {
				response.writeHead(403).end();

				return;
			}

			const body = await readFile(path);

			response.writeHead(200, { "content-type": TYPES[extname(path)] ?? "application/octet-stream" }).end(body);
		} catch {
			response.writeHead(404).end();
		}
	});

	await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));

	return server;
}

/** Waits for the browser to publish its DevTools endpoint, then returns the page's socket URL. */
async function connect(port) {
	for (let attempt = 0; attempt < 100; attempt++) {
		try {
			const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
			const page = targets.find((t) => t.type === "page");

			if (page?.webSocketDebuggerUrl) {
				return page.webSocketDebuggerUrl;
			}
		} catch {
			// The browser is still starting.
		}

		await sleep(100);
	}

	throw new Error("The browser never published a DevTools endpoint.");
}

/** A minimal CDP client: send a command, await its reply. */
function client(socket) {
	let nextId = 0;
	const pending = new Map();

	socket.addEventListener("message", (event) => {
		const message = JSON.parse(event.data);
		const waiter = pending.get(message.id);

		if (!waiter) return;

		pending.delete(message.id);

		if (message.error) waiter.reject(new Error(message.error.message));
		else waiter.resolve(message.result);
	});

	return (method, params = {}) =>
		new Promise((resolve, reject) => {
			const id = ++nextId;

			pending.set(id, { resolve, reject });
			socket.send(JSON.stringify({ id, method, params }));
		});
}

/** Evaluates an expression in the page and returns its value, surfacing a page-side throw. */
async function evaluate(send, expression) {
	const result = await send("Runtime.evaluate", {
		expression,
		awaitPromise: true,
		returnByValue: true,
	});

	if (result.exceptionDetails) {
		throw new Error(result.exceptionDetails.exception?.description ?? "evaluation failed");
	}

	return result.result.value;
}

/**
 * Decodes the renderer's data URL into the bytes to write.
 *
 * The two formats arrive differently: a PNG comes back base64, while an SVG comes back as percent-
 * encoded text. Treating the SVG as base64 produces a file that is the right size and entirely
 * corrupt, so the declared encoding is what decides, not the format.
 */
function decodeDataUrl(dataUrl) {
	const comma = dataUrl.indexOf(",");
	const header = dataUrl.slice(0, comma);
	const payload = dataUrl.slice(comma + 1);

	return header.includes(";base64")
		? Buffer.from(payload, "base64")
		: Buffer.from(decodeURIComponent(payload), "utf8");
}

const server = await serveViewer();
const port = server.address().port;
const profile = await mkdtemp(join(tmpdir(), "aspirec4-chrome-"));

const chrome = spawn(
	await findChrome(),
	[
		"--headless=new",
		"--remote-debugging-port=0",
		// The container is not privileged, and the page is one this package generated from the
		// consumer's own model rather than anything fetched from the network.
		"--no-sandbox",
		"--disable-dev-shm-usage",
		"--hide-scrollbars",
		"--window-size=1600,1000",
		`--user-data-dir=${profile}`,
		`http://127.0.0.1:${port}/`,
	],
	{ stdio: ["ignore", "ignore", "pipe"] },
);

// The chosen debugging port is only discoverable from the browser's own startup banner.
const debugPort = await new Promise((resolve, reject) => {
	let banner = "";

	chrome.stderr.on("data", (chunk) => {
		banner += chunk;

		const match = banner.match(/DevTools listening on ws:\/\/127\.0\.0\.1:(\d+)/);

		if (match) resolve(Number(match[1]));
	});

	chrome.on("exit", (code) => reject(new Error(`chromium exited with code ${code}: ${banner}`)));
});

let failure = null;

try {
	const socket = new WebSocket(await connect(debugPort));

	await new Promise((resolve, reject) => {
		socket.addEventListener("open", resolve, { once: true });
		socket.addEventListener("error", () => reject(new Error("could not attach to the page")), { once: true });
	});

	const send = client(socket);

	await send("Runtime.enable");

	// The hook is published once the nodes are laid out, so its presence is the diagram being ready.
	// Polled rather than waited on with an event, because ELK lays out asynchronously after load.
	let ready = false;

	for (let attempt = 0; attempt < 600 && !ready; attempt++) {
		ready = await evaluate(send, "typeof globalThis.__aspirec4RenderImage === 'function'");

		if (!ready) await sleep(100);
	}

	if (!ready) {
		throw new Error("The viewer never finished laying out the diagram.");
	}

	for (const format of formats) {
		const dataUrl = await evaluate(send, `globalThis.__aspirec4RenderImage(${JSON.stringify(format)})`);

		if (typeof dataUrl !== "string" || !dataUrl.includes(",")) {
			throw new Error(`The viewer produced no ${format}.`);
		}

		await writeFile(join(OUTPUT, `${baseName}.${format}`), decodeDataUrl(dataUrl));
	}

	socket.close();
} catch (error) {
	failure = error;
} finally {
	chrome.kill();
	server.close();
}

if (failure) {
	console.error(failure.message);
	process.exit(1);
}
