// AspireC4 interactive ER diagram viewer.
//
// Consumes the model descriptor directly — the same JSON the AppHost writes — rather than parsing a
// text diagram, so nothing is lost: column-level edges, key roles and context grouping all survive.
//
// Dependencies are loaded from ./vendor/ rather than a CDN, so the viewer works offline. Populate
// that directory with `just erd-vendor`.
import {
	Background,
	Controls,
	ELK,
	Handle,
	MiniMap,
	Position,
	React,
	ReactFlow,
	ReactFlowProvider,
	createRoot,
	toPng,
	toSvg,
	useCallback,
	useEdgesState,
	useEffect,
	useMemo,
	useNodesState,
	useReactFlow,
	useRef,
	useState,
} from "./vendor/bundle.js";

const DATA_URL = document.currentScript?.dataset?.model ?? "__MODEL__";

// Substituted by the generator from AspireC4EfDiagramOptions.DomainTypeDisplay.
const DOMAIN_TYPE_DISPLAY = "__DOMAIN_TYPE_DISPLAY__";

const elk = new ELK();

const h = React.createElement;

/** Flattens the descriptor into tables keyed by name, remembering which context each came from. */
function flatten(model) {
	const tables = [];
	for (const context of model.contexts ?? []) {
		for (const table of context.tables ?? []) {
			tables.push({ ...table, context: context.name, schema: table.schema ?? context.defaultSchema ?? null });
		}
	}
	return tables;
}

/**
 * Picks the crow's-foot marker for the dependent end.
 *
 * The same two flags the Mermaid emitter uses for its cardinality, so both renderers say the same
 * thing about a relationship: unique means at most one dependent row, required means at least one.
 */
function dependentMarker(fk) {
	if (fk.isUnique) return fk.isRequired ? "erd-one" : "erd-one-optional";

	return fk.isRequired ? "erd-many" : "erd-many-optional";
}

/**
 * Builds edges from foreign keys, dropping any whose principal is outside the model.
 *
 * Each edge is drawn in its principal's context colour, so a relationship is traceable to where it
 * starts without following the line back.
 */
function buildEdges(tables, colourOf) {
	const known = new Set(tables.map((t) => t.name.toLowerCase()));
	const contextOf = new Map(tables.map((t) => [t.name, t.context]));
	const edges = [];

	for (const table of tables) {
		for (const fk of table.foreignKeys ?? []) {
			if (!known.has(String(fk.principalTable).toLowerCase())) continue;

			edges.push({
				id: `${table.name}.${fk.columns.join("_")}->${fk.principalTable}`,
				// Edges render below nodes, which would clip a self-loop's arc and label against the
				// neighbouring card it has to reach around.
				...(fk.principalTable === table.name ? { type: "selfLoop", zIndex: 5 } : {}),
				// Drawn principal -> dependent so the dash animation, which runs along the path
				// direction, flows toward the many end. The dependent is the many side unless the
				// foreign key is unique.
				source: fk.principalTable,
				target: table.name,
				label: fk.navigationName ?? fk.columns.join(", "),
				className: "flowing",
				style: {
					stroke: colourOf(contextOf.get(fk.principalTable)),
					strokeWidth: 2.5,
					// The principal end is always exactly one: a foreign key references a single row.
					markerStart: "url(#erd-one)",
					markerEnd: `url(#${dependentMarker(fk)})`,
				},
				// Labels sit on top of whatever the edge crosses, so they carry their own background
				// rather than being read against a table underneath them.
				labelShowBg: true,
				labelBgStyle: { fill: "var(--panel)", stroke: "var(--line)" },
				labelBgPadding: [8, 4],
				labelBgBorderRadius: 10,
				labelStyle: { fill: "var(--fg)", fontSize: 11 },
				data: { unique: !!fk.isUnique, required: !!fk.isRequired },
			});
		}
	}

	return edges;
}

// Matches the palette the Mermaid emitter uses for classDef, so a diagram looks the same whichever
// renderer produced it. Only the stroke is defined; the fill is derived from it so it stays legible
// against both the light and dark themes.
const CONTEXT_COLOURS = ["#3b82f6", "#22c55e", "#f59e0b", "#a855f7", "#f43f5e", "#14b8a6"];

const tint = (hex, alpha) => {
	const n = Number.parseInt(hex.slice(1), 16);
	return `rgba(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}, ${alpha})`;
};

/** A labelled container holding every table from one DbContext. */
function ContextNode({ data }) {
	return h(
		"div",
		{
			className: "context",
			style: { borderColor: tint(data.colour, 0.45), background: tint(data.colour, 0.04) },
		},
		h("span", { className: "context-label", style: { color: data.colour } }, data.label),
	);
}

function EntityNode({ data }) {
	const badges = (col) => {
		const keys = [];
		if (col.isPrimaryKey) keys.push("pk");
		if (col.isForeignKey) keys.push("fk");
		if (col.isUnique && !col.isPrimaryKey) keys.push("uk");
		return keys.map((k) => h("span", { className: `badge ${k}`, key: k }, k.toUpperCase()));
	};

	const isEnum = (col) => col.domainTypeKind === "Enum";

	const bothShown = () =>
		DOMAIN_TYPE_DISPLAY === "DomainTypeFirst" || DOMAIN_TYPE_DISPLAY === "DatabaseFirst";

	// The label kept alongside the headline; also the tooltip when it is not shown inline.
	const annotation = (col) => {
		if (!col.domainTypeName) return null;
		const marker = isEnum(col);
		let db = col.storeType ?? "";
		if (col.maxLength > 0 && !db.includes("(")) db += `(${col.maxLength})`;

		if (DOMAIN_TYPE_DISPLAY === "DomainTypeFirst") return marker ? `${db}, enum` : db;
		if (DOMAIN_TYPE_DISPLAY === "DatabaseFirst") return marker ? `${col.domainTypeName}, enum` : col.domainTypeName;
		if (DOMAIN_TYPE_DISPLAY === "DomainTypeOnly") return db;
		return null;
	};

	const type = (col) => {
		let db = col.storeType ?? "";
		if (col.maxLength > 0 && !db.includes("(")) db += `(${col.maxLength})`;

		const domainFirst =
			!!col.domainTypeName &&
			DOMAIN_TYPE_DISPLAY !== "DatabaseFirst" &&
			DOMAIN_TYPE_DISPLAY !== "DatabaseOnly";
		const primary = domainFirst ? col.domainTypeName : db;

		const parts = [];
		if (bothShown() && col.domainTypeName) {
			parts.push(DOMAIN_TYPE_DISPLAY === "DatabaseFirst" ? col.domainTypeName : db);
		}
		if (isEnum(col)) parts.push("enum");

		let t = parts.length === 0 ? primary : `${primary} (${parts.join(", ")})`;
		if (col.isNullable && !col.isPrimaryKey) t += "?";
		return t;
	};

	return h(
		"div",
		{ className: `entity ${data.match}`, style: { borderColor: tint(data.colour, 0.5) } },
		h(Handle, { type: "target", position: Position.Top }),
		h(
			"header",
			{ style: { borderBottomColor: data.colour, background: tint(data.colour, 0.14) } },
			h("span", null, data.table.name),
			// The context is the container's label now, so only the schema is worth repeating here.
			data.table.schema ? h("span", { className: "schema" }, data.table.schema) : null,
		),
		(data.table.columns ?? []).map((col) =>
			h(
				"div",
				{
					// A table can match on a column the reader cannot see the reason for, so the row
					// that caused the hit is marked rather than only the card around it.
					className: `col${data.needle && col.name.toLowerCase().includes(data.needle) ? " hit" : ""}`,
					key: col.name,
				},
				h("span", { className: "name" }, h("span", { className: "col-name" }, col.name), ...badges(col)),
				h(
					"span",
					{
						className: "type",
						// The database type stays reachable even when the domain type is the headline.
						title: !bothShown() && annotation(col) ? annotation(col) : undefined,
					},
					type(col),
				),
			),
		),
		h(Handle, { type: "source", position: Position.Bottom }),
	);
}

/*
 * Self-loop geometry, shared between the edge and the layout.
 *
 * A self-loop arcs out to the right of its own card and hangs its label out there too, so the
 * layout has to reserve that space or the arc lands across whatever card ELK packed alongside.
 * Both numbers derive from SELF_LOOP_REACH so the drawing and the reservation cannot drift apart.
 */
/*
 * Card metrics, mirroring --entity-width / --entity-header-height / --entity-row-height in
 * index.html. ELK lays out boxes of this size and React Flow drops the real card into each one,
 * so an estimate that runs small is silently paid for out of the gaps between cards and the
 * container's own padding — and the error compounds with every row.
 */
const ENTITY_WIDTH = 248;
const ENTITY_HEADER_HEIGHT = 36;
const ENTITY_ROW_HEIGHT = 28;
const ENTITY_BORDER = 2;

const SELF_LOOP_REACH = 280;

// Measured, not derived: the bezier's rightmost point lands at about this fraction of the control
// offset, and the label rides on it. The space needed beside the card is that overhang plus room
// for the widest label worth reading.
const SELF_LOOP_PEAK = 0.72;
const SELF_LOOP_LABEL_ALLOWANCE = 70;
const SELF_LOOP_GUTTER = SELF_LOOP_REACH * SELF_LOOP_PEAK - ENTITY_WIDTH / 2 + SELF_LOOP_LABEL_ALLOWANCE;

/**
 * A relationship from a table to itself, such as a parent category.
 *
 * The default edge runs from the node's bottom handle to its own top handle, which passes straight
 * behind the card and leaves only a stub visible. This arcs clear of the right-hand edge instead.
 */
function SelfLoopEdge({ sourceX, sourceY, targetX, targetY, style, label }) {
	const reach = SELF_LOOP_REACH;
	const d = `M ${sourceX},${sourceY} C ${sourceX + reach},${sourceY + 24} ${targetX + reach},${targetY - 24} ${targetX},${targetY}`;

	return h(
		React.Fragment,
		null,
		h("path", { className: "react-flow__edge-path", d, style, fill: "none" }),
		label
			? h(
					"text",
					{
						className: "self-loop-label",
						x: Math.max(sourceX, targetX) + reach * SELF_LOOP_PEAK,
						y: (sourceY + targetY) / 2,
						dominantBaseline: "middle",
						textAnchor: "middle",
					},
					label,
				)
			: null,
	);
}

const nodeTypes = { entity: EntityNode, context: ContextNode };
const edgeTypes = { selfLoop: SelfLoopEdge };

/**
 * The box ELK should lay the table out in.
 *
 * Wider than the rendered card when the table references itself: React Flow draws the card at its
 * own width against the left of this box, which leaves the remainder as clear gutter for the loop.
 */
const entitySize = (table, selfReferencing = false) => ({
	width: ENTITY_WIDTH + (selfReferencing ? SELF_LOOP_GUTTER : 0),
	height: ENTITY_BORDER + ENTITY_HEADER_HEIGHT + (table.columns?.length ?? 0) * ENTITY_ROW_HEIGHT,
});

/**
 * Lays the graph out with one container per DbContext.
 *
 * ELK is given a nested graph so it sizes each container around its own tables and routes edges
 * between them; React Flow then expects child positions relative to their parent, which is exactly
 * what ELK returns.
 */
/** Assigns each context a stable colour, so nodes and edges agree on it. */
function contextColours(tables) {
	const contexts = [...new Set(tables.map((t) => t.context))];
	const byContext = new Map(contexts.map((c, i) => [c, CONTEXT_COLOURS[i % CONTEXT_COLOURS.length]]));

	return (context) => byContext.get(context) ?? CONTEXT_COLOURS[0];
}

async function layout(tables, edges, colourOf) {
	const contexts = [...new Set(tables.map((t) => t.context))];

	const contextOf = new Map(tables.map((t) => [t.name, t.context]));
	const selfReferencing = new Set(edges.filter((e) => e.source === e.target).map((e) => e.source));

	const graph = {
		id: "root",
		// Two algorithms: rectpacking arranges the containers towards a landscape ratio, and layered
		// arranges the tables inside each. Using layered at both levels put every container in a
		// single row — it has no edges between them to work with, and elk.aspectRatio does not reach
		// its component packer — which was far wider than tall and forced the view to zoom out.
		layoutOptions: {
			"elk.algorithm": "rectpacking",
			// Tuned by measuring the rendered result: 1.7 left every container in a single row, since
			// one row was already closer to that ratio than wrapping would be, and 1.4 produced a
			// ragged three-plus-one. A target squarer than the viewport is what actually wraps.
			"elk.aspectRatio": "1.1",
			"elk.spacing.nodeNode": "60",
		},
		children: contexts.map((context) => ({
			id: `ctx:${context}`,
			layoutOptions: {
				"elk.algorithm": "layered",
				"elk.direction": "DOWN",
				// Top carries the label that sits on the border line; the other three are even, so the
				// container reads as a frame around its tables rather than a box they are pushed into.
				"elk.padding": "[top=52,left=28,bottom=28,right=28]",
				"elk.spacing.nodeNode": "48",
				"elk.layered.spacing.nodeNodeBetweenLayers": "76",
			},
			children: tables
				.filter((t) => t.context === context)
				.map((table) => ({ id: table.name, ...entitySize(table, selfReferencing.has(table.name)) })),
			// Only relationships within this context: they order the tables sensibly. Cross-context
			// edges are left out because ELK's routing is unused — React Flow draws every edge itself
			// from the resulting node positions. Self-loops go too: they say nothing about ordering,
			// and the space they need is already reserved in the node's own box.
			edges: edges
				.filter((e) => e.source !== e.target)
				.filter((e) => contextOf.get(e.source) === context && contextOf.get(e.target) === context)
				.map((e) => ({ id: e.id, sources: [e.source], targets: [e.target] })),
		})),
	};

	const result = await elk.layout(graph);
	const nodes = [];

	for (const container of result.children ?? []) {
		const context = container.id.slice("ctx:".length);
		const colour = colourOf(context);

		// Containers must precede their children in the array or React Flow cannot resolve parentId.
		nodes.push({
			id: container.id,
			type: "context",
			position: { x: container.x ?? 0, y: container.y ?? 0 },
			style: { width: container.width, height: container.height },
			data: { label: context, colour },
			selectable: false,
			draggable: false,
		});

		for (const child of container.children ?? []) {
			const table = tables.find((t) => t.name === child.id);

			nodes.push({
				id: child.id,
				type: "entity",
				parentId: container.id,
				extent: "parent",
				position: { x: child.x ?? 0, y: child.y ?? 0 },
				data: { table, colour, match: "", needle: "" },
			});
		}
	}

	return nodes;
}

/*
 * Theme.
 *
 * "auto" is the absence of a pin: the stylesheet leaves :root at `color-scheme: light dark` and every
 * light-dark() token follows the operating system. Light and dark pin color-scheme instead, so the
 * same token block serves all three and there is no second palette to keep in step.
 */
const THEMES = ["auto", "light", "dark"];
const THEME_KEY = "aspirec4-erd-theme";

/** Reads and writes the stored choice, tolerating an origin where storage is unavailable. */
const storedTheme = () => {
	try {
		const value = localStorage.getItem(THEME_KEY);

		return THEMES.includes(value) ? value : "auto";
	} catch {
		return "auto";
	}
};

function useTheme() {
	const [theme, setTheme] = useState(storedTheme);

	useEffect(() => {
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
	}, [theme]);

	// React Flow's own chrome has a light and a dark stylesheet of its own, selected by this prop
	// rather than by our tokens, so it has to be told the resolved theme and not "auto".
	const [systemDark, setSystemDark] = useState(
		() => globalThis.matchMedia?.("(prefers-color-scheme: dark)").matches ?? false,
	);

	useEffect(() => {
		const query = globalThis.matchMedia?.("(prefers-color-scheme: dark)");
		if (!query) return;

		const onChange = (event) => setSystemDark(event.matches);

		query.addEventListener("change", onChange);

		return () => query.removeEventListener("change", onChange);
	}, []);

	const resolved = theme === "auto" ? (systemDark ? "dark" : "light") : theme;

	return [theme, setTheme, resolved];
}

/*
 * Image export.
 *
 * Exported at native size rather than at whatever zoom the view happens to sit at, so the result is
 * the diagram rather than a screenshot of a window onto it.
 */
const EXPORT_PADDING = 60;

// A very large schema would otherwise ask the browser for a canvas it will refuse to allocate, and
// the failure is a silently blank image rather than an error.
const EXPORT_MAX_SIDE = 8000;

/**
 * The bounds of the whole drawing, in flow coordinates.
 *
 * Taken from the containers: every table is laid out inside one and clamped there by `extent`, so
 * their union covers the graph. The padding absorbs the bow of a bezier running between two of them.
 */
function drawingBounds(nodes) {
	const containers = nodes.filter((n) => n.type === "context");

	if (containers.length === 0) return null;

	const left = Math.min(...containers.map((n) => n.position.x));
	const top = Math.min(...containers.map((n) => n.position.y));
	const right = Math.max(...containers.map((n) => n.position.x + (n.style?.width ?? 0)));
	const bottom = Math.max(...containers.map((n) => n.position.y + (n.style?.height ?? 0)));

	return { x: left, y: top, width: right - left, height: bottom - top };
}

/**
 * Hands the browser a file without going near the page's own navigation.
 *
 * Downloaded from a blob rather than from the data URL the renderer returns: a full-size PNG runs to
 * several megabytes, past the length a browser will accept in a URL, and the failure is silent.
 */
async function save(dataUrl, fileName) {
	const href = URL.createObjectURL(await (await fetch(dataUrl)).blob());
	const link = document.createElement("a");

	link.download = fileName;
	link.href = href;
	link.click();

	// Revoking straight away can outrun the download; the object only has to outlive the handover.
	setTimeout(() => URL.revokeObjectURL(href), 30_000);
}

/**
 * Renders the current diagram to an image and downloads it.
 *
 * The crow's-foot markers are defined in a stand-alone &lt;svg&gt; outside the viewport, so a capture
 * of the viewport alone resolves every `url(#erd-…)` to nothing and loses the cardinality. A copy
 * rides along for the duration of the capture.
 */
async function exportImage(format, nodes, title) {
	const viewport = document.querySelector(".react-flow__viewport");
	const bounds = drawingBounds(nodes);

	if (!viewport || !bounds) return false;

	const natural = Math.max(bounds.width, bounds.height) + EXPORT_PADDING * 2;
	const scale = Math.min(1, EXPORT_MAX_SIDE / natural);

	const width = Math.ceil((bounds.width + EXPORT_PADDING * 2) * scale);
	const height = Math.ceil((bounds.height + EXPORT_PADDING * 2) * scale);

	const markers = document.querySelector("svg.erd-markers")?.cloneNode(true);

	if (markers) viewport.append(markers);

	try {
		const render = format === "png" ? toPng : toSvg;
		const dataUrl = await render(viewport, {
			backgroundColor: getComputedStyle(document.body).backgroundColor,
			width,
			height,
			// PNG is a raster, so it is taken at twice the size to stay sharp when zoomed or printed.
			...(format === "png" ? { pixelRatio: 2 } : {}),
			style: {
				width: `${width}px`,
				height: `${height}px`,
				transform: `scale(${scale}) translate(${EXPORT_PADDING - bounds.x}px, ${EXPORT_PADDING - bounds.y}px)`,
				transformOrigin: "top left",
			},
		});

		await save(dataUrl, `${(title || "erd").replaceAll(/[^\w.-]+/g, "-").toLowerCase()}.${format}`);

		return true;
	} finally {
		markers?.remove();
	}
}

function Diagram({ model }) {
	const tables = useMemo(() => flatten(model), [model]);
	const [nodes, setNodes, onNodesChange] = useNodesState([]);
	const [edges, setEdges, onEdgesChange] = useEdgesState([]);
	const [query, setQuery] = useState("");
	const [cursor, setCursor] = useState(0);
	const [exporting, setExporting] = useState("");
	const [theme, setTheme, resolvedTheme] = useTheme();

	const { fitView } = useReactFlow();
	const input = useRef(null);
	// Whether Enter has already moved to the match the counter is pointing at. The first press
	// travels to it; later presses advance. Without this the first press would skip match one.
	const visited = useRef(false);

	useEffect(() => {
		const colourOf = contextColours(tables);
		const built = buildEdges(tables, colourOf);

		layout(tables, built, colourOf).then((positioned) => {
			setNodes(positioned);
			setEdges(built);
		});
	}, [tables, setNodes, setEdges]);

	const needle = query.trim().toLowerCase();

	/** Tables matching the query, in layout order, so Enter walks the diagram rather than the model. */
	const matches = useMemo(() => {
		if (!needle) return [];

		return tables
			.filter(
				(t) =>
					t.name.toLowerCase().includes(needle) ||
					(t.columns ?? []).some((c) => c.name.toLowerCase().includes(needle)),
			)
			.map((t) => t.name);
	}, [tables, needle]);

	const current = matches.length > 0 ? matches[Math.min(cursor, matches.length - 1)] : null;

	// Marking is derived from the query rather than applied by the change handler, so the state of
	// the drawing cannot drift from the state of the field.
	useEffect(() => {
		const hits = new Set(matches);

		setNodes((nodes) =>
			nodes.map((n) => {
				// Containers are never dimmed; doing so would wash out the tables inside them.
				if (n.type !== "entity") return n;

				const match = !needle ? "" : n.id === current ? "hit current" : hits.has(n.id) ? "hit" : "dimmed";

				if (n.data.match === match && n.data.needle === needle) return n;

				return { ...n, data: { ...n.data, match, needle } };
			}),
		);

		// A relationship is only fully in play when both of its ends are, so an edge recedes with
		// whichever end was dimmed.
		setEdges((edges) =>
			edges.map((e) => {
				const className = `flowing${needle && !(hits.has(e.source) && hits.has(e.target)) ? " dimmed" : ""}`;

				return e.className === className ? e : { ...e, className };
			}),
		);
	}, [matches, needle, current, setNodes, setEdges]);

	const search = useCallback((value) => {
		setQuery(value);
		setCursor(0);
		visited.current = false;
	}, []);

	/** Moves to the next or previous match and brings it into view. */
	const step = useCallback(
		(delta) => {
			if (matches.length === 0) return;

			const next = visited.current ? (cursor + delta + matches.length) % matches.length : cursor;

			visited.current = true;

			setCursor(next);
			fitView({ nodes: [{ id: matches[next] }], duration: 420, maxZoom: 1.1, padding: 0.6 });
		},
		[matches, cursor, fitView],
	);

	// Search is the one control worth reaching without the mouse, so it takes the shortcut every
	// other search field on the web has trained the reader to try.
	useEffect(() => {
		const onKeyDown = (event) => {
			const typing = document.activeElement === input.current;

			if (event.key === "Escape" && typing) {
				search("");

				return;
			}

			if (typing || event.altKey || event.shiftKey) return;

			if (event.key === "/" || ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k")) {
				event.preventDefault();
				input.current?.focus();
				input.current?.select();
			}
		};

		globalThis.addEventListener("keydown", onKeyDown);

		return () => globalThis.removeEventListener("keydown", onKeyDown);
	}, [search]);

	const download = useCallback(
		async (format) => {
			setExporting(format);

			try {
				// The page title carries the configured diagram title, so the file is named after the
				// diagram rather than after the renderer.
				await exportImage(format, nodes, document.title);
			} finally {
				setExporting("");
			}
		},
		[nodes],
	);

	const themeButton = (option) =>
		h(
			"button",
			{
				key: option,
				type: "button",
				"data-theme-option": option,
				"aria-pressed": theme === option,
				title: option === "auto" ? "Follow the system theme" : `Always ${option}`,
				onClick: () => setTheme(option),
			},
			option[0].toUpperCase() + option.slice(1),
		);

	return h(
		React.Fragment,
		null,
		h(
			"div",
			{ className: "toolbar" },
			h(
				"div",
				{ className: "search" },
				h("input", {
					ref: input,
					placeholder: "Search tables and columns…",
					"aria-label": "Search tables and columns",
					value: query,
					onChange: (e) => search(e.target.value),
					onKeyDown: (e) => {
						if (e.key !== "Enter") return;

						e.preventDefault();
						step(e.shiftKey ? -1 : 1);
					},
				}),
				needle
					? h(
							"span",
							{ className: `matches${matches.length === 0 ? " none" : ""}` },
							matches.length === 0 ? "no matches" : `${cursor + 1}/${matches.length}`,
						)
					: null,
				query
					? h(
							"button",
							{
								type: "button",
								className: "clear",
								title: "Clear the search (Esc)",
								"aria-label": "Clear the search",
								onClick: () => {
									search("");
									input.current?.focus();
								},
							},
							"×",
						)
					: null,
			),
			h("span", { className: "count" }, `${tables.length} tables · ${edges.length} relationships`),
			h("span", { className: "sep" }),
			h("span", { className: "count" }, "Export"),
			...["png", "svg"].map((format) =>
				h(
					"button",
					{
						key: format,
						type: "button",
						"data-export": format,
						disabled: exporting !== "",
						title: `Download the whole diagram as ${format.toUpperCase()}`,
						onClick: () => download(format),
					},
					exporting === format ? "…" : format.toUpperCase(),
				),
			),
			h("span", { className: "sep" }),
			h("div", { className: "segmented", role: "group", "aria-label": "Theme" }, ...THEMES.map(themeButton)),
		),
		h(ReactFlow, {
			nodes,
			edges,
			onNodesChange,
			onEdgesChange,
			nodeTypes,
			edgeTypes,
			colorMode: resolvedTheme,
			fitView: true,
			// Asymmetric, because the toolbar floats over the top-left corner: a uniform ratio either
			// has to be large enough for the toolbar — wasting the other three sides — or it leaves
			// the first container's label underneath it. The top figure is the toolbar's own height
			// plus a margin; the rest is breathing room.
			fitViewOptions: { padding: { top: "96px", right: "24px", bottom: "24px", left: "24px" } },
			proOptions: { hideAttribution: true },
			children: [
				h(Background, { key: "bg", variant: "lines", gap: 28, color: "var(--grid)" }),
				h(Controls, { key: "controls" }),
				h(MiniMap, { key: "minimap", pannable: true, zoomable: true }),
			],
		}),
	);
}

function App() {
	const [model, setModel] = useState(null);
	const [error, setError] = useState(null);

	useEffect(() => {
		fetch(DATA_URL)
			.then((r) => {
				if (!r.ok) throw new Error(`${r.status} ${r.statusText}`);
				return r.json();
			})
			.then(setModel)
			.catch((e) =>
				setError(
					`Could not load ${DATA_URL}: ${e.message}. Opening this file directly from disk may be blocked by the browser's file:// restrictions — serve the folder over HTTP instead.`,
				),
			);
	}, []);

	if (error) return h("div", { style: { padding: 24 } }, error);
	if (!model) return h("div", { style: { padding: 24 } }, "Loading…");

	return h(ReactFlowProvider, null, h(Diagram, { model }));
}

createRoot(document.getElementById("root")).render(h(App));
