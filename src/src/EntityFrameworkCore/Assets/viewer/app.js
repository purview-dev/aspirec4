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
	useCallback,
	useEdgesState,
	useEffect,
	useMemo,
	useNodesState,
	useState,
} from "./vendor/bundle.js";

const DATA_URL = document.currentScript?.dataset?.model ?? "__MODEL__";

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

/** Builds edges from foreign keys, dropping any whose principal is outside the model. */
function buildEdges(tables) {
	const known = new Set(tables.map((t) => t.name.toLowerCase()));
	const edges = [];

	for (const table of tables) {
		for (const fk of table.foreignKeys ?? []) {
			if (!known.has(String(fk.principalTable).toLowerCase())) continue;

			edges.push({
				id: `${table.name}.${fk.columns.join("_")}->${fk.principalTable}`,
				// Drawn principal -> dependent so the dash animation, which runs along the path
				// direction, flows toward the many end. The dependent is the many side unless the
				// foreign key is unique.
				source: fk.principalTable,
				target: table.name,
				label: fk.navigationName ?? fk.columns.join(", "),
				className: "flowing",
				// Labels sit on top of whatever the edge crosses, so they carry their own background
				// rather than being read against a table underneath them.
				labelShowBg: true,
				labelBgStyle: { fill: "var(--panel)", fillOpacity: 0.92 },
				labelBgPadding: [6, 3],
				labelBgBorderRadius: 4,
				labelStyle: { fill: "var(--muted)", fontSize: 11 },
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

	const type = (col) => {
		let t = col.storeType ?? "";
		if (col.maxLength > 0 && !t.includes("(")) t += `(${col.maxLength})`;
		if (col.isNullable && !col.isPrimaryKey) t += "?";
		return t;
	};

	return h(
		"div",
		{ className: `entity ${data.match}` },
		h(Handle, { type: "target", position: Position.Top }),
		h(
			"header",
			{ style: { borderBottomColor: data.colour } },
			h("span", null, data.table.name),
			// The context is the container's label now, so only the schema is worth repeating here.
			data.table.schema ? h("span", { className: "schema" }, data.table.schema) : null,
		),
		(data.table.columns ?? []).map((col) =>
			h(
				"div",
				{ className: "col", key: col.name },
				h("span", { className: "name" }, col.name, ...badges(col)),
				h("span", { className: "type" }, type(col)),
			),
		),
		h(Handle, { type: "source", position: Position.Bottom }),
	);
}

const nodeTypes = { entity: EntityNode, context: ContextNode };

const entitySize = (table) => ({ width: 240, height: 48 + (table.columns?.length ?? 0) * 22 });

/**
 * Lays the graph out with one container per DbContext.
 *
 * ELK is given a nested graph so it sizes each container around its own tables and routes edges
 * between them; React Flow then expects child positions relative to their parent, which is exactly
 * what ELK returns.
 */
async function layout(tables, edges) {
	const contexts = [...new Set(tables.map((t) => t.context))];

	const contextOf = new Map(tables.map((t) => [t.name, t.context]));

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
				"elk.padding": "[top=46,left=20,bottom=20,right=20]",
				"elk.spacing.nodeNode": "40",
				"elk.layered.spacing.nodeNodeBetweenLayers": "70",
			},
			children: tables
				.filter((t) => t.context === context)
				.map((table) => ({ id: table.name, ...entitySize(table) })),
			// Only relationships within this context: they order the tables sensibly. Cross-context
			// edges are left out because ELK's routing is unused — React Flow draws every edge itself
			// from the resulting node positions.
			edges: edges
				.filter((e) => contextOf.get(e.source) === context && contextOf.get(e.target) === context)
				.map((e) => ({ id: e.id, sources: [e.source], targets: [e.target] })),
		})),
	};

	const result = await elk.layout(graph);
	const nodes = [];

	for (const [index, container] of (result.children ?? []).entries()) {
		const context = container.id.slice("ctx:".length);
		const colour = CONTEXT_COLOURS[index % CONTEXT_COLOURS.length];

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
				data: { table, colour, match: "" },
			});
		}
	}

	return nodes;
}

function Diagram({ model }) {
	const tables = useMemo(() => flatten(model), [model]);
	const [nodes, setNodes, onNodesChange] = useNodesState([]);
	const [edges, setEdges, onEdgesChange] = useEdgesState([]);
	const [query, setQuery] = useState("");

	useEffect(() => {
		const built = buildEdges(tables);

		layout(tables, built).then((positioned) => {
			setNodes(positioned);
			setEdges(built);
		});
	}, [tables, setNodes, setEdges]);

	const search = useCallback(
		(value) => {
			setQuery(value);
			const needle = value.trim().toLowerCase();

			setNodes((current) =>
				current.map((n) => {
					// Containers are never dimmed; doing so would wash out the tables inside them.
					if (n.type !== "entity") return n;

					if (!needle) return { ...n, data: { ...n.data, match: "" } };

					const hit =
						n.id.toLowerCase().includes(needle) ||
						(n.data.table.columns ?? []).some((c) => c.name.toLowerCase().includes(needle));

					return { ...n, data: { ...n.data, match: hit ? "hit" : "dimmed" } };
				}),
			);
		},
		[setNodes],
	);

	return h(
		React.Fragment,
		null,
		h(
			"div",
			{ className: "toolbar" },
			h("input", {
				placeholder: "Search tables and columns…",
				value: query,
				onChange: (e) => search(e.target.value),
			}),
			h("span", { className: "count" }, `${tables.length} tables · ${edges.length} relationships`),
		),
		h(ReactFlow, {
			nodes,
			edges,
			onNodesChange,
			onEdgesChange,
			nodeTypes,
			fitView: true,
			// Padding keeps the top-left entity clear of the floating toolbar.
			fitViewOptions: { padding: 0.12 },
			proOptions: { hideAttribution: true },
			children: [
				h(Background, { key: "bg" }),
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
