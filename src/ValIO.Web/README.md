# ValIO.Web

The web frontend: a **Blazor Server** UI (MudBlazor) plus a minimal REST API for generating code in the
browser and downloading the result as a streamed `.zip`.

Like the other frontends it contains no generation logic — it calls `ValIO.Application` and uses the
streaming zip sink from `ValIO.Output`. Plugins are registered via
`ValIO.Plugins.Bootstrap.AddAllGeneratorPlugins`.
