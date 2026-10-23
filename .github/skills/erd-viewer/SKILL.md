---
name: erd-viewer
description: Verifies the generated Entity Framework ER diagram viewer in a real browser. Use this skill when asked to run, drive, screenshot or visually check the ER diagram, the interactive viewer, or the Mermaid preview — or after changing anything under src/src/EntityFrameworkCore/Assets/.
---

## Verifying the ER diagram viewer

The interactive viewer is the default renderer and it is **all browser**: ELK layout, card measurement,
edge markers, search, theming and the complex value-object folds only run once a browser has loaded
`app.js`. A green `just test` says nothing about any of it — the C# suite stops at the model JSON.

Do **not** hand-roll a driver. There is already a Playwright harness.

### The one command

```sh
just erd-check
```

That recipe does both halves: `scripts/erd-vendor.mts` populates `artifacts/erd-viewer-check/vendor`,
then `scripts/erd-viewer-check.mts` assembles the viewer from source, serves it, drives it in Chromium
and prints one `ok`/`FAIL` line per check. It exits non-zero if any check failed.

It defaults to `src/src/TestAppHost/.aspirec4/erd/schema.gen.json` as its model. Pass another:

```sh
just erd-check artifacts/some-other-model.json
just erd-mermaid-check                              # the Mermaid preview instead
```

Prerequisite, once per machine:

```sh
bunx playwright install chromium
```

### Getting a model to check against

The default model is whatever the sample AppHost last wrote. If it is missing or stale, regenerate it:

```sh
dotnet run --project src/src/TestAppHost
```

Wait for `src/src/TestAppHost/.aspirec4/erd/schema.gen.json` to appear, then stop the AppHost. Stopping
it by killing the process leaves its containers running — `docker ps` and remove them, or they hold
ports the next run wants. `.aspirec4/` is gitignored.

The suite needs a model with some variety in it, because several checks pick their own target from the
model: a self-referencing table, a column name no table name contains, a context holding more than one
table, and at least one flattened complex value object. The `TestAppHost.ComplexData` sample is built to
supply all four — prefer it over a hand-trimmed model.

### Reading the output

Each line is one assertion with the measurement beside it, which is the point — `120px` tells you what
the layout did, not merely that it passed. Artefacts land in `artifacts/erd-viewer-check/`:
`viewer-check.png`, `viewer-check-dark.png`, and the exported `viewer-export.svg` / `.png`.

Two traps that have bitten before:

- **Screen pixels are not layout pixels.** The view is zoomed to fit, so a 56px layout offset is 28
  screen pixels at scale 0.5. Every geometric assertion divides by the viewport scale first. A check
  that forgets passes on a small schema and fails the moment the diagram grows.
- **`fitContexts` pins a container's top to its topmost table.** Dragging that table carries the whole
  container with it, so its offset within the context never changes. Assertions about dragging or about
  a remembered arrangement must target a table that is *not* the topmost, or they hold vacuously.

### Adding a check

Add it to `scripts/erd-viewer-check.mts` next to the related section and let it run in the existing
`for (const reducedMotion of ...)` loop. Use `check(name, ok, detail)` — never `throw` — so one failure
does not hide the rest, and put the measurement in `detail`.

Derive targets from the model rather than hard-coding a table name, the way the existing column-search
check does, so the suite survives a change to the sample.

### When a screenshot is the actual question

If the task is "does this *look* right" rather than "does it work", the suite already writes
`viewer-check.png` and `viewer-check-dark.png` on every run — read those first. Only reach for a bespoke
Playwright script when you need an interaction the suite does not perform, and then put it in
`scripts/` and delete it again, or add it to the suite properly.

### Related

- `just erd-vendor` rebuilds the viewer's bundled JavaScript; pass `--embed` to refresh the copies
  shipped inside the package after a dependency bump.
- `just erd-svg <input>` renders an emitted Mermaid `.mmd` to SVG through a one-shot container.
