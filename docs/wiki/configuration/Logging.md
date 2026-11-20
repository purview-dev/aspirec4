# Logging and Diagnostics

AspireC4 writes what it is doing, and anything that goes wrong, to two places: the AppHost's own log and
each diagram's **Console** tab in the Aspire dashboard. This page owns where each message goes, how to
see more of them, and the event ids to filter on.

AspireC4 sends nothing anywhere else. It has no usage telemetry, no traces and no metrics, and makes no
network call of its own beyond the container images it runs.

## Where messages go

| Diagram | AppHost log | Dashboard Console tab |
| --- | --- | --- |
| Architecture (LikeC4) | every message, under the category `Aspire.Hosting.AspireC4.Lifecycle.IAspireC4LifecycleHookTelemetry` | the LikeC4 server's own output, plus every warning below |
| Entity Framework Core ER diagram | — | every message, on the `aspirec4-er` row |
| ER model dump (`AspireC4.EntityFrameworkCore.ModelDump`) | — | — ; it runs at build time and reports to the build output as an MSBuild warning |

A failure that stops a diagram updating is always a **warning**, and always reaches the Console tab:
the dashboard is where you notice a stale diagram, so that is where it says why. The ER diagram's row
also turns **Failed** when a write fails, and back to **Running** on the next successful one.

## Seeing more

The architecture diagram's progress messages are at `Debug` level, so the default AppHost log shows only
its information messages and warnings. Lower the level for AspireC4 in the AppHost's `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Aspire.Hosting.AspireC4": "Debug"
    }
  }
}
```

or, without editing a file, `Logging__LogLevel__Aspire.Hosting.AspireC4=Debug` in the environment.

## Event ids

Every message has a fixed event id, so a log filter or an alert keeps working across releases. Ids are
never reused.

### Architecture diagram (1000–1099)

| Id | Level | Message |
| --- | --- | --- |
| 1001 | Debug | Generating the model from the resource graph |
| 1002 | Debug | Model written (only when its content changed) |
| 1003 | Debug | Publish mode: model generated, nothing started |
| 1004 | Debug | A resource changed state |
| 1005 | Debug | The Aspire dashboard's URL was found |
| 1006 | Debug | Regenerating after a state change |
| 1007 | Warning | A background watcher stopped; names the watcher and the exception |
| 1008 | Debug | `likec4 format` applied |
| 1009 | Debug | An additional DSL file was copied in |
| 1010 | Information | A `.gitignore` AspireC4 did not write was left alone |
| 1011 | Warning | The formatter could not be run |
| 1012 | Information | The LikeC4 version was resolved |
| 1013 | Warning | The LikeC4 version could not be resolved; names the command and the reason |
| 1014 | Debug | Applying the options snapshot |
| 1015 | Debug | Options snapshot applied |
| 1016 | Debug | Exporting images |
| 1017 | Information | Images exported |
| 1018 | Warning | One image format could not be exported |
| 1019 | Debug | Images already match the model |
| 1020 | Warning | Regenerating the diagram failed |
| 1021 | Warning | Image export failed |
| 1022 | Warning | An additional DSL file does not exist |
| 1023 | Warning | The formatter exited with an error |

### ER diagram (2000–2099)

| Id | Level | Message |
| --- | --- | --- |
| 2001 | Information | A file was written |
| 2002 | Information | The diagram was generated, with its context and table counts |
| 2003 | Information | Some contexts were recovered from metadata, so fluent configuration is missing |
| 2004 | Information | The diagram is unchanged |
| 2005 | Information | A `.gitignore` AspireC4 did not write was left alone |
| 2006 | Warning | No Entity Framework Core models were found |
| 2007 | Information | The viewer's JavaScript was written |
| 2008 | Warning | A project opted in with `WithEntityFrameworkModel()` produced no model, with what was tried |
| 2009 | Warning | A pass found nothing, so the previous diagram was kept |
| 2010 | Information | An image was exported |
| 2011 | Information | The images already match the diagram |
| 2012 | Information | Rendering through the viewer has started |
| 2013 | Warning | Viewer-fidelity export does not apply to this renderer |
| 2014 | Warning | Image export failed |
| 2015 | Warning | Background work failed (generating, regenerating, linking, exporting) |
| 2016 | Information | A project's model was read, and from which source: the model dump's status, descriptors found, contexts recovered from metadata |

## Related pages

- [Dashboard Integration](../Dashboard-Integration.md) — the rows, links and commands AspireC4 adds
- [Generated Output](../Generated-Output.md) — what is written, and the LikeC4 version check
- [Entity Framework Core ER Diagrams](../Entity-Framework-Diagrams.md) — the three model sources 2016 reports on
