# Dashboard Integration

AspireC4 integrates with the Aspire dashboard so the diagram is easy to reach and links back to resource telemetry.

## The sidecar resource

`AddAspireC4()` creates an `aspirec4` resource (name configurable via the `name` parameter) of container type. Its state, URLs, and properties are forwarded from the inner LikeC4 server resource, which is always kept hidden. The dashboard shows:

- A **View LikeC4 Diagram** URL on the resource, opening `/view/index` (or the `DefaultViewId`).
- The **LikeC4 Version** property, resolved from the pinned tag or the startup version check.
- The server's console logs, relayed onto the outer resource's Console tab.

## Hiding the sidecar

When `HideFromDashboard` is set (via `WithHideFromDashboard(displayName)`), the `aspirec4` resource is also hidden and the diagram is instead surfaced on every **project** resource as:

- A **link** (`architecture-diagram`) injected into the project resource's URLs once the server is running.
- A **command** (`likec4-architecture-diagram`) that opens the diagram. The command is disabled until the LikeC4 server is `Running`.

`DashboardLinkDisplayName` controls the label shown (default `Architecture Diagram`).

## Dashboard links on diagram elements

`IncludeAspireDashboardLinks` (default `true`) adds links from each LikeC4 element back to the Aspire dashboard's console logs and structured logs pages for that resource. This requires `AutoIncludeAspireMetadata` to include `Links` (the default `All` includes it). The links are constructed at runtime once the dashboard URL is discovered.

### Security: browser tokens in links

`IncludeAspireTokenInDashboardLinks` (default `false`) embeds the Aspire browser token in those dashboard links so the browser is authenticated when the link is clicked (`/login?t=…&returnUrl=…`).

> [!WARNING]
> The token grants the same access as a browser session and is written into the generated diagram file, which may be shared or stored in an insecure location/source control. Only enable it if you understand the implications. Consider excluding the generated `.c4` file from source control and sharing it only over secure channels. For normal development, keep it disabled and navigate to the dashboard manually.

## View selection

The dashboard link opens `/view/{DefaultViewId}` (`index` by default — the ID of the auto-generated view). Set `DefaultViewId` to `null`/empty to link to the server root instead, which is useful when you prefer the LikeC4 server's own landing page. If you change `GeneratedViewId` in the generated file, set `DefaultViewId` to the same value so the dashboard link still opens the correct diagram.

## Hot Module Replacement

HMR keeps the diagram up to date in the browser as the file regenerates. The HMR endpoint is exposed in the dashboard (details view) and, on Windows/Docker Desktop, `CHOKIDAR_USEPOLLING`/`CHOKIDAR_INTERVAL` environment variables are set on the container so file changes are detected via polling. Disable HMR with `WithHMRDisabled()`. See [Configuration](Configuration.md) for `HMRPort` behavior across LikeC4 versions.

## Next pages

- [Generated Output](Generated-Output.md)
- [Configuration](Configuration.md)