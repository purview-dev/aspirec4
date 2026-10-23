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
	NodeResizer,
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
 * The relationship's name, in both directions.
 *
 * A foreign key has a navigation on the dependent and usually an inverse collection on the principal.
 * Edges here are drawn principal -> dependent, so the principal's navigation is named first and the
 * arrow in the label agrees with the arrow on the edge. Where the model declares only one end, only
 * that one is named: a single arrow says which way it can be travelled, and nothing claims the other
 * direction exists. Only when neither is declared does it fall back to the constraint name, and then
 * to the columns.
 */
function relationshipLabel(fk) {
	const forward = fk.principalNavigationName;
	const reverse = fk.navigationName;

	if (forward && reverse) return `${forward} ⇄ ${reverse}`;
	if (forward) return `${forward} →`;
	if (reverse) return `← ${reverse}`;
	if (fk.constraintName) return fk.constraintName;

	return (fk.columns ?? []).join(", ");
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
				label: relationshipLabel(fk),
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

/*
 * Column presentation, shared by the card and the width measurement — the two have to agree, or a
 * measured width would not match what is rendered.
 */
const columnBadges = (col) => {
	const keys = [];
	if (col.isPrimaryKey) keys.push("pk");
	if (col.isForeignKey) keys.push("fk");
	if (col.isUnique && !col.isPrimaryKey) keys.push("uk");
	return keys;
};

const isEnumColumn = (col) => col.domainTypeKind === "Enum";

const COMPLEX_KIND = "ComplexValueObject";

/** The property a flattened complex member belongs to: the segment before the first underscore. */
const complexOwner = (col) => col.name.split("_")[0];

/**
 * Folds a table's flattened complex value-object columns under the property that owns them.
 *
 * EF maps a complex value object into its owner's table as one column per member, named
 * `{Property}_{Member}`, so a card otherwise shows a run of rows that belong together while saying
 * nothing about what they are. The owning prefixes are collected first, because a member whose own
 * type is an enum or a nested value object carries that type rather than the enclosing one and would
 * not be recognised on its own.
 *
 * @returns rows in model order: a column row per ordinary column, a group row per complex property.
 */
function rowPlan(table) {
	const columns = table.columns ?? [];
	const owners = new Set(columns.filter((c) => c.domainTypeKind === COMPLEX_KIND).map(complexOwner));
	const rows = [];
	const groups = new Map();

	for (const col of columns) {
		const owner = complexOwner(col);

		if (!col.name.includes("_") || !owners.has(owner)) {
			rows.push({ kind: "column", key: col.name, col });

			continue;
		}

		let group = groups.get(owner);

		if (!group) {
			group = { kind: "group", key: owner, name: owner, typeName: null, columns: [] };

			groups.set(owner, group);
			rows.push(group);
		}

		// The outermost member names the group: a nested value object's own members carry the inner type,
		// so the shallowest member is the one that reports the property's own type.
		const shallow = !col.name.slice(owner.length + 1).includes("_");

		if (group.typeName === null && col.domainTypeKind === COMPLEX_KIND && shallow) {
			group.typeName = col.domainTypeName;
		}

		group.columns.push(col);
	}

	for (const group of groups.values()) {
		group.typeName ??= group.columns[0]?.domainTypeName ?? "";
	}

	return rows;
}

/** Whether any table flattens a complex value object, and so has anything to expand. */
const hasComplexColumns = (tables) =>
	tables.some((t) => (t.columns ?? []).some((c) => c.domainTypeKind === COMPLEX_KIND));

/** The member name shown inside its group, with the owning property's prefix removed. */
const memberName = (group, col) => col.name.slice(group.name.length + 1);

/** A group row's type cell: the value object, and when it is closed what it is holding. */
const groupType = (group, open) => (open ? group.typeName : `${group.typeName} (${group.columns.length})`);

/** The group keys a card is showing: the reader's own, plus any opened to reveal a search match. */
const expandedOf = (data) => new Set([...(data.expanded ?? []), ...(data.forced ?? [])]);

/**
 * A row's type cell and the tooltip beside it.
 *
 * A member of an open group whose domain type is the group's own value object is shown as its database
 * type alone: the group row above it already names the value object, and repeating it on every member
 * crowds out the one thing those rows are there to say.
 *
 * @param group the group the row belongs to, or null for a column of the table itself.
 */
function rowType(col, group) {
	const own = group && col.domainTypeKind === COMPLEX_KIND && col.domainTypeName === group.typeName;
	const shown = own ? { ...col, domainTypeName: null, domainTypeKind: null } : col;

	return { text: columnType(shown), annotation: columnAnnotation(shown) };
}

const bothShown = () =>
	DOMAIN_TYPE_DISPLAY === "DomainTypeFirst" || DOMAIN_TYPE_DISPLAY === "DatabaseFirst";

/** The label kept alongside the headline; also the tooltip when it is not shown inline. */
const columnAnnotation = (col) => {
	if (!col.domainTypeName) return null;

	const marker = isEnumColumn(col);
	let db = col.storeType ?? "";
	if (col.maxLength > 0 && !db.includes("(")) db += `(${col.maxLength})`;

	if (DOMAIN_TYPE_DISPLAY === "DomainTypeFirst") return marker ? `${db}, enum` : db;
	if (DOMAIN_TYPE_DISPLAY === "DatabaseFirst") return marker ? `${col.domainTypeName}, enum` : col.domainTypeName;
	if (DOMAIN_TYPE_DISPLAY === "DomainTypeOnly") return db;
	return null;
};

const columnType = (col) => {
	let db = col.storeType ?? "";
	if (col.maxLength > 0 && !db.includes("(")) db += `(${col.maxLength})`;

	const domainFirst =
		!!col.domainTypeName && DOMAIN_TYPE_DISPLAY !== "DatabaseFirst" && DOMAIN_TYPE_DISPLAY !== "DatabaseOnly";
	const primary = domainFirst ? col.domainTypeName : db;

	const parts = [];
	if (bothShown() && col.domainTypeName) {
		parts.push(DOMAIN_TYPE_DISPLAY === "DatabaseFirst" ? col.domainTypeName : db);
	}
	if (isEnumColumn(col)) parts.push("enum");

	let t = parts.length === 0 ? primary : `${primary} (${parts.join(", ")})`;
	if (col.isNullable && !col.isPrimaryKey) t += "?";
	return t;
};

/** One ordinary column row, or one member of an opened complex value object. */
function ColumnRow({ col, name, type, needle, className }) {
	return h(
		"div",
		{
			// A table can match on a column the reader cannot see the reason for, so the row that caused
			// the hit is marked rather than only the card around it.
			className: `col${className}${needle && col.name.toLowerCase().includes(needle) ? " hit" : ""}`,
		},
		h(
			"span",
			{ className: "name" },
			h("span", { className: "col-name" }, name),
			...columnBadges(col).map((k) => h("span", { className: `badge ${k}`, key: k }, k.toUpperCase())),
		),
		h(
			"span",
			{
				className: "type",
				// The database type stays reachable even when the domain type is the headline.
				title: !bothShown() && type.annotation ? type.annotation : undefined,
			},
			type.text,
		),
	);
}

/**
 * The rows a card draws, with each complex value object folded under a row that opens it.
 *
 * The group's own row is a button rather than a decoration: a reader following a relationship into a
 * table wants the flattened members on demand, not a run of prefixed columns they have to reassemble.
 */
function cardRowElements(data, onToggle) {
	const expanded = expandedOf(data);
	const elements = [];

	for (const row of rowPlan(data.table)) {
		if (row.kind === "column") {
			elements.push(
				h(ColumnRow, {
					key: row.key,
					col: row.col,
					name: row.col.name,
					type: rowType(row.col, null),
					needle: data.needle,
					className: "",
				}),
			);

			continue;
		}

		const open = expanded.has(row.key);

		elements.push(
			h(
				"div",
				{ key: row.key, className: `col group${open ? " open" : ""}` },
				h(
					"span",
					{ className: "name" },
					h(
						"button",
						{
							type: "button",
							className: "chevron",
							"aria-expanded": open,
							title: `${open ? "Collapse" : "Expand"} ${row.name}`,
							onClick: (event) => {
								// The card is draggable and selectable; neither should happen on a disclosure.
								event.stopPropagation();
								onToggle(row.key);
							},
						},
						open ? "▾" : "▸",
					),
					h("span", { className: "col-name" }, row.name),
				),
				h("span", { className: "type" }, groupType(row, open)),
			),
		);

		if (!open) continue;

		for (const col of row.columns) {
			elements.push(
				h(ColumnRow, {
					key: col.name,
					col,
					name: memberName(row, col),
					type: rowType(col, row),
					needle: data.needle,
					className: " member",
				}),
			);
		}
	}

	return elements;
}

function EntityNode({ id, data, selected }) {
	const { setNodes } = useReactFlow();

	/** Opens or closes one group on this card, and re-sizes it to what it now draws. */
	const onToggle = useCallback(
		(key) =>
			setNodes((nodes) =>
				saveFrom(
					fitContexts(
						nodes.map((node) => {
							if (node.id !== id) return node;

							const expanded = new Set(node.data.expanded ?? []);

							if (!expanded.delete(key)) expanded.add(key);

							return resizeEntity({ ...node, data: { ...node.data, expanded: [...expanded] } });
						}),
					),
				),
			),
		[id, setNodes],
	);

	return h(
		"div",
		{
			className: `entity ${data.match}`,
			// The node's box is the card: the width is measured, and a manual resize overrides it.
			style: { borderColor: tint(data.colour, 0.5), width: "100%", height: "100%" },
		},
		h(NodeResizer, {
			isVisible: selected,
			minWidth: ENTITY_MIN_WIDTH,
			minHeight: ENTITY_MIN_HEIGHT,
			lineClassName: "entity-resize-line",
			handleClassName: "entity-resize-handle",
		}),
		h(Handle, { type: "target", position: Position.Top, isConnectable: false }),
		h(
			"header",
			{ style: { borderBottomColor: data.colour, background: tint(data.colour, 0.14) } },
			h("span", null, data.table.name),
			// The context is the container's label now, so only the schema is worth repeating here.
			data.table.schema ? h("span", { className: "schema" }, data.table.schema) : null,
		),
		// "names" mode is the overview: the header alone, so a large schema can be read at a glance.
		data.tables === "names" ? null : cardRowElements(data, onToggle),
		h(Handle, { type: "source", position: Position.Bottom, isConnectable: false }),
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
 * Card metrics, mirroring the type sizes and paddings in index.html.
 *
 * The height is fixed per row, but the width is measured from the content — see cardWidth — so a long
 * column name or store type widens its own card rather than being truncated. ELK still has to know a
 * card's size before one is rendered, which is why the measurement is taken against the stylesheet's
 * own fonts rather than the rendered DOM.
 */
const ENTITY_HEADER_HEIGHT = 36;
const ENTITY_ROW_HEIGHT = 28;
const ENTITY_BORDER = 2;

/** The floor a measured card falls back to, so a tiny table is still a readable card. */
const ENTITY_MIN_WIDTH = 200;
const ENTITY_MIN_HEIGHT = 64;

/*
 * Text measurement.
 *
 * The DOM is not available before the first layout, and a two-pass measure-then-relayout would flash
 * an overlapping diagram, so a canvas is used with the same font stack the stylesheet sets. A small
 * buffer absorbs the difference between canvas metrics and layout metrics.
 */
const measureContext = document.createElement("canvas").getContext("2d");

const FONT_SANS = '12.5px ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif';
const FONT_HEADER = '600 13px ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif';
const FONT_SCHEMA = '11px ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif';
const FONT_TYPE = '11px ui-monospace, "Cascadia Mono", "SF Mono", Menlo, monospace';
const FONT_BADGE = '700 9px ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif';

// Mirroring index.html: .col padding and gap, .name gap, and the badge's padding and letter-spacing.
const CARD_PADDING = 12;
const COLUMN_GAP = 10;
const BADGE_GAP = 5;
const BADGE_PADDING = 8;
const BADGE_LETTER_SPACING = 0.04;
const MEASURE_BUFFER = 3;

// Mirroring index.html: the chevron's box and the indent a grouped member row carries.
const CHEVRON_WIDTH = 14;
const CHEVRON_GAP = 4;
const MEMBER_INDENT = 18;

const textWidth = (text, font) => {
	if (!text) return 0;

	measureContext.font = font;

	return measureContext.measureText(text).width;
};

/** The width one column row needs, as a name, its badges and its type. */
function columnRowWidth(col, name, indent, type) {
	let row = indent + textWidth(name, FONT_SANS);

	for (const badge of columnBadges(col)) {
		const text = badge.toUpperCase();
		row += BADGE_GAP + textWidth(text, FONT_BADGE) + BADGE_PADDING + text.length * 9 * BADGE_LETTER_SPACING;
	}

	return row + COLUMN_GAP + textWidth(type, FONT_TYPE);
}

/**
 * The width a table's card needs for every column name, badge and type to fit.
 *
 * A collapsed group is measured as though it were open, so expanding one never re-widens the card and
 * nothing has to be laid out again to accommodate it.
 */
function cardWidth(table, tables) {
	let widest = textWidth(table.name, FONT_HEADER);

	if (table.schema) widest += COLUMN_GAP + textWidth(table.schema, FONT_SCHEMA);

	// In names mode only the header is drawn, so only the header has to fit.
	if (tables !== "names") {
		for (const row of rowPlan(table)) {
			if (row.kind === "column") {
				widest = Math.max(widest, columnRowWidth(row.col, row.col.name, 0, rowType(row.col, null).text));

				continue;
			}

			// The closed form carries the member count, so it is the wider of the two.
			widest = Math.max(
				widest,
				CHEVRON_WIDTH +
					CHEVRON_GAP +
					textWidth(row.name, FONT_SANS) +
					COLUMN_GAP +
					textWidth(groupType(row, false), FONT_TYPE),
			);

			for (const col of row.columns) {
				widest = Math.max(
					widest,
					columnRowWidth(col, memberName(row, col), MEMBER_INDENT, rowType(col, row).text),
				);
			}
		}
	}

	return Math.ceil(widest + CARD_PADDING * 2 + ENTITY_BORDER + MEASURE_BUFFER);
}

/** How many rows a card draws: a closed group is one row, an open one is itself plus its members. */
function cardRows(table, tables, expanded) {
	if (tables === "names") return 0;

	let rows = 0;

	for (const row of rowPlan(table)) {
		rows += row.kind === "column" ? 1 : 1 + (expanded.has(row.key) ? row.columns.length : 0);
	}

	return rows;
}

/**
 * The rendered card's size — measured for the width, fixed per row for the height.
 *
 * This is what a container is fitted around. The box ELK lays out may be wider when the table
 * references itself, because the self-loop needs a gutter beside the card.
 *
 * @param tables the table display mode: "columns" draws every column, "names" only the header.
 * @param expanded the complex value-object groups this card is showing the members of.
 */
const cardSize = (table, tables, expanded = new Set()) => ({
	width: Math.max(ENTITY_MIN_WIDTH, cardWidth(table, tables)),
	height: ENTITY_BORDER + ENTITY_HEADER_HEIGHT + cardRows(table, tables, expanded) * ENTITY_ROW_HEIGHT,
});

const SELF_LOOP_REACH = 280;

// Measured, not derived: the bezier's rightmost point lands at about this fraction of the control
// offset, and the label rides on it. The space needed beside the card is that overhang plus room
// for the widest label worth reading.
const SELF_LOOP_PEAK = 0.72;
const SELF_LOOP_LABEL_ALLOWANCE = 70;

/** The gutter a self-loop needs beside a card of the given width. */
const selfLoopGutter = (width) => SELF_LOOP_REACH * SELF_LOOP_PEAK - width / 2 + SELF_LOOP_LABEL_ALLOWANCE;

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
const entitySize = (table, selfReferencing, tables) => {
	const card = cardSize(table, tables);

	return {
		width: card.width + (selfReferencing ? selfLoopGutter(card.width) : 0),
		height: card.height,
	};
};

/**
 * Lays the graph out with one container per DbContext.
 *
 * ELK is given a nested graph so it sizes each container around its own tables and routes edges
 * between them; React Flow then expects child positions relative to their parent, which is exactly
 * what ELK returns.
 */
/** The container's own margin around its tables, mirroring the ELK padding above. */
const CONTEXT_PADDING = { top: 56, right: 32, bottom: 32, left: 32 };

/**
 * A node's current size. A manual resize sets the node's own width and height, which then wins over
 * the measured card size.
 */
const nodeWidth = (node) =>
	node.width ?? node.measured?.width ?? node.data?.card?.width ?? ENTITY_MIN_WIDTH;

const nodeHeight = (node) => node.height ?? node.measured?.height ?? node.data?.card?.height ?? 0;

/**
 * Re-sizes an entity for the groups it is currently showing.
 *
 * Only the height moves: a card is measured as though every group were open, so opening one never
 * changes its width and the arrangement around it stays put. A card the reader has resized by hand
 * keeps the size they gave it.
 */
function resizeEntity(node) {
	if (node.type !== "entity" || node.height != null) return node;

	// Only the row count changes, so the height is recomputed directly rather than re-measuring every
	// name and type through the canvas — this runs for every card on every keystroke of a search.
	const rows = cardRows(node.data.table, node.data.tables, expandedOf(node.data));
	const height = ENTITY_BORDER + ENTITY_HEADER_HEIGHT + rows * ENTITY_ROW_HEIGHT;

	if (node.data.card?.height === height && node.style?.height === height) return node;

	return {
		...node,
		style: { ...node.style, height },
		data: { ...node.data, card: { ...node.data.card, height } },
	};
}

/**
 * Resizes and moves each context container so it wraps its own tables, preserving every table's
 * absolute position.
 *
 * The container is the tables' positioning context, so growing it up or left means moving it and
 * offsetting the tables by the same amount — otherwise the whole group would drift.
 *
 * @returns the same array when nothing needed adjusting, so a caller can set it back without a loop.
 */
function fitContexts(nodes) {
	const childrenOf = new Map();

	for (const node of nodes) {
		if (!node.parentId) continue;

		const list = childrenOf.get(node.parentId) ?? [];

		list.push(node);
		childrenOf.set(node.parentId, list);
	}

	const adjustments = new Map();

	for (const node of nodes) {
		if (node.type !== "context") continue;

		const children = childrenOf.get(node.id) ?? [];

		if (children.length === 0) continue;

		const minX = Math.min(...children.map((c) => c.position.x));
		const minY = Math.min(...children.map((c) => c.position.y));
		const maxX = Math.max(...children.map((c) => c.position.x + nodeWidth(c)));
		const maxY = Math.max(...children.map((c) => c.position.y + nodeHeight(c)));

		const dx = minX - CONTEXT_PADDING.left;
		const dy = minY - CONTEXT_PADDING.top;
		const width = maxX - minX + CONTEXT_PADDING.left + CONTEXT_PADDING.right;
		const height = maxY - minY + CONTEXT_PADDING.top + CONTEXT_PADDING.bottom;

		if (dx === 0 && dy === 0 && width === node.style?.width && height === node.style?.height) continue;

		adjustments.set(node.id, {
			dx,
			dy,
			x: node.position.x + dx,
			y: node.position.y + dy,
			width,
			height,
		});
	}

	if (adjustments.size === 0) return nodes;

	return nodes.map((node) => {
		if (node.type === "context") {
			const a = adjustments.get(node.id);

			return a
				? {
						...node,
						position: { x: a.x, y: a.y },
						style: { ...node.style, width: a.width, height: a.height },
					}
				: node;
		}

		if (!node.parentId) return node;

		const a = adjustments.get(node.parentId);

		return a ? { ...node, position: { x: node.position.x - a.dx, y: node.position.y - a.dy } } : node;
	});
}

/*
 * The arrangement is saved in the browser, keyed by a fingerprint of the model, so a reload keeps the
 * reader's layout and a regenerated diagram (whose model has changed) falls back to ELK.
 */
const LAYOUT_KEY = "aspirec4-erd-layout";

const layoutFingerprint = (tables, mode) =>
	`${mode}|` +
	tables
		.map((t) => `${t.name}#${(t.columns ?? []).length}`)
		.sort()
		.join("|");

function saveLayout(nodes, tables, mode) {
	try {
		const containers = {};
		const entities = {};

		for (const node of nodes) {
			if (node.type === "context") {
				containers[node.id] = {
					x: node.position.x,
					y: node.position.y,
					width: node.style?.width,
					height: node.style?.height,
				};
			} else if (node.parentId) {
				const entry = { x: node.position.x, y: node.position.y, parentId: node.parentId };

				// Only a manual resize sets the node's own dimensions; a measured card is recomputed
				// from the model, so it is not stored.
				if (node.width != null || node.height != null) {
					entry.width = node.width;
					entry.height = node.height;
				}

				// Which groups the reader opened, but not the ones a search opened for them.
				if (node.data?.expanded?.length > 0) entry.expanded = node.data.expanded;

				entities[node.id] = entry;
			}
		}

		localStorage.setItem(
			LAYOUT_KEY,
			JSON.stringify({ fingerprint: layoutFingerprint(tables, mode), containers, entities }),
		);
	} catch {
		/* the arrangement still applies to this session */
	}
}

function loadLayout(tables, mode) {
	try {
		const raw = localStorage.getItem(LAYOUT_KEY);

		if (!raw) return null;

		const saved = JSON.parse(raw);

		return saved.fingerprint === layoutFingerprint(tables, mode) ? saved : null;
	} catch {
		return null;
	}
}

/**
 * Persists the arrangement using the tables carried by the nodes themselves.
 *
 * Folding a group changes a card's height, so it has to be saved like a drag or a resize — but it is
 * toggled from inside the card, which has no access to the model. Every entity node holds its own
 * table and the display mode, which is all the fingerprint needs.
 *
 * @returns the nodes it was given, so a caller can save and return in one step.
 */
function saveFrom(nodes) {
	const entities = nodes.filter((node) => node.type === "entity");

	if (entities.length > 0) {
		saveLayout(
			nodes,
			entities.map((node) => node.data.table),
			entities[0].data.tables,
		);
	}

	return nodes;
}

/** Rebuilds the graph from a saved arrangement, dropping anything the model no longer has. */
function restoreLayout(saved, tables, colourOf, mode) {
	const nodes = [];
	const known = new Set(tables.map((t) => t.name));

	for (const [id, box] of Object.entries(saved.containers ?? {})) {
		const context = id.slice("ctx:".length);

		nodes.push({
			id,
			type: "context",
			position: { x: box.x, y: box.y },
			style: { width: box.width, height: box.height },
			data: { label: context, colour: colourOf(context) },
			selectable: false,
			draggable: false,
		});
	}

	for (const [id, position] of Object.entries(saved.entities ?? {})) {
		const table = tables.find((t) => t.name === id);

		if (!table || !known.has(id)) continue;

		const expanded = position.expanded ?? [];
		const card = cardSize(table, mode, new Set(expanded));
		const manual = position.width != null || position.height != null;
		const width = manual ? position.width : card.width;
		const height = manual ? position.height : card.height;

		nodes.push({
			id,
			type: "entity",
			parentId: position.parentId,
			position: { x: position.x, y: position.y },
			// A manual size is the node's own; a measured one is only the rendered style, so the node
			// stays un-sized and keeps following its content.
			...(manual ? { width, height } : {}),
			style: { width, height },
			data: {
				table,
				colour: colourOf(table.context),
				match: "",
				needle: "",
				card,
				tables: mode,
				expanded,
				forced: [],
			},
		});
	}

	return nodes;
}

/** Assigns each context a stable colour, so nodes and edges agree on it. */
function contextColours(tables) {
	const contexts = [...new Set(tables.map((t) => t.context))];
	const byContext = new Map(contexts.map((c, i) => [c, CONTEXT_COLOURS[i % CONTEXT_COLOURS.length]]));

	return (context) => byContext.get(context) ?? CONTEXT_COLOURS[0];
}

async function layout(tables, edges, colourOf, mode) {
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
			"elk.spacing.nodeNode": "88",
		},
		children: contexts.map((context) => ({
			id: `ctx:${context}`,
			layoutOptions: {
				"elk.algorithm": "layered",
				"elk.direction": "DOWN",
				// Top carries the label that sits on the border line; the other three are even, so the
				// container reads as a frame around its tables rather than a box they are pushed into.
				"elk.padding": "[top=56,left=32,bottom=32,right=32]",
				// The gap between layers carries the relationship labels, which are drawn at the middle
				// of an edge over their own background: too little room and a label lands on the card
				// below it. The sideways gap only has to separate two cards, so it is smaller.
				"elk.spacing.nodeNode": "72",
				"elk.layered.spacing.nodeNodeBetweenLayers": "120",
				// Tables that no relationship connects form separate components, which ELK lays out one
				// by one and then packs — a seam the between-layer spacing never reaches. Left at its
				// default of 20 those groups sit almost touching, which reads as one crowded block
				// rather than as the unrelated groups they are.
				"elk.spacing.componentComponent": "120",
				// Edges routed around a card keep clear of it rather than running along its border, and
				// parallel edges stay far enough apart to be followed individually.
				"elk.spacing.edgeNode": "40",
				"elk.layered.spacing.edgeNodeBetweenLayers": "40",
				"elk.spacing.edgeEdge": "24",
				"elk.layered.spacing.edgeEdgeBetweenLayers": "24",
			},
			children: tables
				.filter((t) => t.context === context)
				.map((table) => ({ id: table.name, ...entitySize(table, selfReferencing.has(table.name), mode) })),
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
			const card = cardSize(table, mode);

			nodes.push({
				id: child.id,
				type: "entity",
				parentId: container.id,
				position: { x: child.x ?? 0, y: child.y ?? 0 },
				style: { width: card.width, height: card.height },
				// Complex value objects start folded: a fresh card shows the table, not its interiors.
				data: { table, colour, match: "", needle: "", card, tables: mode, expanded: [], forced: [] },
			});
		}
	}

	return nodes;
}

/*
 * Viewer settings.
 *
 * Persisted in the browser: the viewer is a static asset with no server, so a preference can only
 * live in localStorage. An "auto" theme is the absence of a pin — the stylesheet leaves :root at
 * `color-scheme: light dark` and every light-dark() token follows the operating system.
 */
const THEMES = ["auto", "light", "dark"];
const ANIMATIONS = ["off", "slow", "normal", "fast"];
const BACKGROUNDS = ["lines", "dots", "cross"];
const MOTIONS = ["system", "reduce", "allow"];
const TABLES = ["columns", "names"];

const SETTINGS_KEY = "aspirec4-erd-settings";

const SETTINGS_DEFAULTS = {
	theme: "auto",
	animation: "normal",
	background: "lines",
	motion: "system",
	tables: "columns",
};

const ANIMATION_DURATIONS = { slow: "1.8s", normal: "0.9s", fast: "0.45s" };

/** Reads the stored settings, tolerating an origin where storage is unavailable. */
function storedSettings() {
	try {
		const raw = localStorage.getItem(SETTINGS_KEY);
		const parsed = raw ? JSON.parse(raw) : {};

		return { ...SETTINGS_DEFAULTS, ...parsed };
	} catch {
		return { ...SETTINGS_DEFAULTS };
	}
}

function useSettings() {
	const [settings, setSettings] = useState(storedSettings);

	useEffect(() => {
		try {
			localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
		} catch {
			/* the choice still applies to this session */
		}

		const root = document.documentElement;

		if (settings.theme === "auto") delete root.dataset.theme;
		else root.dataset.theme = settings.theme;

		if (settings.motion === "system") delete root.dataset.motion;
		else root.dataset.motion = settings.motion;

		const duration = ANIMATION_DURATIONS[settings.animation];

		root.style.setProperty("--edge-animation", duration ? `dash ${duration} linear infinite` : "none");
	}, [settings]);

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

	const resolved = settings.theme === "auto" ? (systemDark ? "dark" : "light") : settings.theme;

	return [settings, setSettings, resolved];
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
 * Renders the current diagram to an image and returns it as a data URL.
 *
 * The crow's-foot markers are defined in a stand-alone &lt;svg&gt; outside the viewport, so a capture
 * of the viewport alone resolves every `url(#erd-…)` to nothing and loses the cardinality. A copy
 * rides along for the duration of the capture.
 *
 * Separate from {@link exportImage} so the AppHost's image export can render the diagram without
 * downloading it: a headless browser has nowhere to put a download, and intercepting one to get at
 * the bytes is a great deal of machinery for something the renderer already has in hand.
 *
 * @returns the data URL, or null when there is nothing laid out to capture.
 */
async function renderImage(format, nodes, title) {
	const viewport = document.querySelector(".react-flow__viewport");
	const bounds = drawingBounds(nodes);

	if (!viewport || !bounds) return null;

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

		return dataUrl;
	} finally {
		markers?.remove();
	}
}

/** Renders the current diagram to an image and downloads it. */
async function exportImage(format, nodes, title) {
	const dataUrl = await renderImage(format, nodes, title);

	if (!dataUrl) return false;

	await save(dataUrl, `${(title || "erd").replaceAll(/[^\w.-]+/g, "-").toLowerCase()}.${format}`);

	return true;
}

function Diagram({ model }) {
	const tables = useMemo(() => flatten(model), [model]);
	const complex = useMemo(() => hasComplexColumns(tables), [tables]);
	const [nodes, setNodes, onNodesChange] = useNodesState([]);
	const [edges, setEdges, onEdgesChange] = useEdgesState([]);
	const [query, setQuery] = useState("");
	const [cursor, setCursor] = useState(0);
	const [exporting, setExporting] = useState("");
	const [settings, setSettings, resolvedTheme] = useSettings();
	const [settingsOpen, setSettingsOpen] = useState(false);

	const { fitView } = useReactFlow();
	const input = useRef(null);
	// Whether Enter has already moved to the match the counter is pointing at. The first press
	// travels to it; later presses advance. Without this the first press would skip match one.
	const visited = useRef(false);

	useEffect(() => {
		const colourOf = contextColours(tables);
		const built = buildEdges(tables, colourOf);
		const saved = loadLayout(tables, settings.tables);

		// A remembered arrangement wins over ELK, so a reload keeps the reader's layout. It is keyed by
		// the model's fingerprint and the table display mode, so a regenerated diagram — or a mode
		// whose card sizes differ — falls back to a fresh layout.
		if (saved) {
			setNodes(restoreLayout(saved, tables, colourOf, settings.tables));
		} else {
			layout(tables, built, colourOf, settings.tables).then((positioned) => setNodes(positioned));
		}

		setEdges(built);
	}, [tables, settings.tables, setNodes, setEdges]);

	/**
	 * Applies React Flow's own changes, then fits each container around its tables and, once a drag or
	 * a resize ends, remembers the arrangement.
	 */
	const handleNodesChange = useCallback(
		(changes) => {
			onNodesChange(changes);

			const settled = changes.some(
				(c) =>
					(c.type === "position" && c.dragging === false) ||
					(c.type === "dimensions" && c.resizing === false),
			);

			if (!settled) return;

			setNodes((current) => {
				const fitted = fitContexts(current);

				saveLayout(fitted, tables, settings.tables);

				return fitted;
			});
		},
		[onNodesChange, setNodes, tables, settings.tables],
	);

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
			fitContexts(
				nodes.map((n) => {
					// Containers are never dimmed; doing so would wash out the tables inside them.
					if (n.type !== "entity") return n;

					const match = !needle ? "" : n.id === current ? "hit current" : hits.has(n.id) ? "hit" : "dimmed";

					// A match inside a folded complex value object would otherwise be reported and then not
					// shown. The groups opened this way are held apart from the reader's own, so clearing
					// the search folds them back rather than leaving the card as the search left it.
					const forced = !needle
						? []
						: rowPlan(n.data.table)
								.filter(
									(row) =>
										row.kind === "group" && row.columns.some((c) => c.name.toLowerCase().includes(needle)),
								)
								.map((row) => row.key);

					if (
						n.data.match === match &&
						n.data.needle === needle &&
						n.data.forced?.length === forced.length &&
						forced.every((key) => n.data.forced.includes(key))
					) {
						return n;
					}

					return resizeEntity({ ...n, data: { ...n.data, match, needle, forced } });
				}),
			),
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

	// The hook the AppHost's viewer-fidelity export calls, so an image produced headlessly is the same
	// one this toolbar produces. Published only once the nodes are laid out, which is what makes its
	// presence the signal that the diagram is ready to capture.
	useEffect(() => {
		if (nodes.length === 0) return undefined;

		globalThis.__aspirec4RenderImage = (format) => renderImage(format, nodes, document.title);

		return () => {
			delete globalThis.__aspirec4RenderImage;
		};
	}, [nodes]);

	/**
	 * Opens or folds every complex value object on every card.
	 *
	 * The arrangement is saved straight away: the cards have changed height, so a reload that restored
	 * the previous one would reopen the diagram with the containers fitted to the wrong sizes.
	 */
	const groupKeys = useCallback(
		(table) =>
			rowPlan(table)
				.filter((row) => row.kind === "group")
				.map((row) => row.key),
		[],
	);

	const setAllExpanded = useCallback(
		(open) =>
			setNodes((current) =>
				saveFrom(
					fitContexts(
						current.map((node) =>
							node.type === "entity"
								? resizeEntity({
										...node,
										data: { ...node.data, expanded: open ? groupKeys(node.data.table) : [] },
									})
								: node,
						),
					),
				),
			),
		[setNodes, groupKeys],
	);

	/** A segmented choice over a settings key, shared by the toolbar and the settings panel. */
	const segmented = (key, options, label) =>
		h(
			"div",
			{ className: "segmented", role: "group", "aria-label": label },
			...options.map((option) =>
				h(
					"button",
					{
						key: option,
						type: "button",
						"aria-pressed": settings[key] === option,
						onClick: () => setSettings((current) => ({ ...current, [key]: option })),
					},
					option[0].toUpperCase() + option.slice(1),
				),
			),
		);

	/** One settings row: a label and a segmented choice. */
	const optionRow = (label, key, options) =>
		h(
			"div",
			{ className: "settings-row" },
			h("span", { className: "settings-label" }, label),
			segmented(key, options, label),
		);

	// The table display is promoted to the toolbar: it changes the shape of the whole diagram, which is
	// too fundamental to hide behind a generic "Settings". The rest stays in the popover.
	const settingsPanel = h(
		"div",
		{ className: "settings-panel", role: "group", "aria-label": "Viewer settings" },
		optionRow("Theme", "theme", THEMES),
		optionRow("Edge animation", "animation", ANIMATIONS),
		optionRow("Background", "background", BACKGROUNDS),
		optionRow("Motion", "motion", MOTIONS),
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
			h("span", { className: "count" }, "View"),
			segmented("tables", TABLES, "View"),
			// Only shown when the model has something to fold, so it is not dead chrome on a diagram
			// whose tables are all plain columns.
			...(complex && settings.tables !== "names"
				? [
						h("span", { className: "sep" }),
						h("span", { className: "count" }, "Complex"),
						h(
							"div",
							{ className: "segmented", role: "group", "aria-label": "Complex value objects" },
							h(
								"button",
								{
									type: "button",
									title: "Show the members of every complex value object",
									onClick: () => setAllExpanded(true),
								},
								"Expand all",
							),
							h(
								"button",
								{
									type: "button",
									title: "Fold every complex value object back to its property",
									onClick: () => setAllExpanded(false),
								},
								"Collapse all",
							),
						),
					]
				: []),
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
			h(
				"div",
				{ className: "settings" },
				h(
					"button",
					{
						type: "button",
						"aria-expanded": settingsOpen,
						title: "Viewer settings",
						onClick: () => setSettingsOpen((open) => !open),
					},
					"Settings",
				),
				settingsOpen ? settingsPanel : null,
			),
		),
		h(ReactFlow, {
			nodes,
			edges,
			onNodesChange: handleNodesChange,
			onEdgesChange,
			nodeTypes,
			edgeTypes,
			colorMode: resolvedTheme,
			// Edges are drawn from the model, not by the reader, so nothing can be connected by hand —
			// the handles are anchors only.
			nodesConnectable: false,
			fitView: true,
			// Asymmetric, because the toolbar floats over the top-left corner: a uniform ratio either
			// has to be large enough for the toolbar — wasting the other three sides — or it leaves
			// the first container's label underneath it. The top figure is the toolbar's own height
			// plus a margin; the rest is breathing room.
			fitViewOptions: { padding: { top: "96px", right: "24px", bottom: "24px", left: "24px" } },
			proOptions: { hideAttribution: true },
			children: [
				h(Background, { key: "bg", variant: settings.background, gap: 28, color: "var(--grid)" }),
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
