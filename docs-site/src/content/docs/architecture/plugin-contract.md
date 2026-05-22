---
title: Plugin contract
description: IGeneratorPlugin — the one interface every language plugin implements.
---

Every language ValIO supports lives behind a single interface,
`IGeneratorPlugin`. The orchestrator never knows anything language-
specific — it just walks the plugin's pipeline.

## The interface

```csharp
public interface IGeneratorPlugin
{
    string LanguageId { get; }
    string DisplayName { get; }
    IReadOnlyList<string> LanguageAliases { get; }
    IGeneratorOptionsDescriptor OptionsDescriptor { get; }
    ICodeGenerationPipeline CreatePipeline(IServiceProvider services);
    void ConfigureServices(IServiceCollection services);
}
```

(Source: `src/ValIO.Core/Plugins/IGeneratorPlugin.cs`.)

## Members

| Member               | Purpose |
|----------------------|---------|
| `LanguageId`         | Canonical id used in CLI `-l` and config `language`. e.g. `"typescript"`. |
| `DisplayName`        | Human label for UI dropdowns. e.g. `"TypeScript"`. |
| `LanguageAliases`    | Extra accepted ids. e.g. `["ts", "typescript"]`. The registry indexes both. |
| `OptionsDescriptor`  | Parses raw JSON options and exposes descriptions for UI rendering. |
| `CreatePipeline`     | Builds the `ICodeGenerationPipeline` to run for this generation. |
| `ConfigureServices`  | Registers DI services the plugin needs (renderers, builders, naming converters, …). |

## Lifecycle

1. **Registration.** Each front-end calls
   `AddAllGeneratorPlugins()` from `ValIO.Plugins.Bootstrap`, which
   registers every shipped plugin as an `IGeneratorPlugin` singleton.
   The plugin's `ConfigureServices` runs during DI build to add its
   own services.
2. **Discovery.** `GeneratorRegistry` collects all plugins. The lookup
   table is case-insensitive and indexes the canonical id plus every
   alias.
3. **Options parsing.** Before generation, the config loader feeds the
   raw `options` dictionary into
   `OptionsDescriptor.ParseOptions(raw, out errors)`. If `errors` is
   non-empty the run fails with a list of messages.
4. **Pipeline construction.** The orchestrator calls
   `plugin.CreatePipeline(services)`. Plugins typically build a
   `CodeGenerationPipeline` over an ordered list of
   `ICodeGenerationStep` instances.
5. **Step execution.** Each step's `ShouldExecute(context)` gate runs
   first; if it returns `true`, `ExecuteAsync` mutates
   `context.GeneratedFiles`. See [Pipeline](/architecture/pipeline/).
6. **Output.** The orchestrator returns the `GeneratedFiles` list
   wrapped in an `OrchestrationResult`. A separate output sink
   materializes them; see [Output sinks](/architecture/output-sinks/).

## Options descriptor

```csharp
public interface IGeneratorOptionsDescriptor
{
    object? ParseOptions(Dictionary<string, JsonElement> raw, out IReadOnlyList<string> errors);
    IReadOnlyList<string> ValidateOptions(object options);
    IReadOnlyList<OptionDescription> GetOptionDescriptions();
}
```

`GetOptionDescriptions()` returns an `OptionDescription` per knob with
its kind (`Bool` / `String` / `Int` / `EnumSingle` / `EnumMulti`),
choices, default value, group label, and an optional resource key for
the localized description. The Web UI uses this list to render a
language-specific form dynamically; the CLI doesn't need it because
options come through pre-typed JSON.

## Adding a new plugin

1. Create `ValIO.Generators.MyLang` with an `IGeneratorPlugin`
   implementation and an `IGeneratorOptionsDescriptor`.
2. Implement at least one `ICodeGenerationStep` (typically: build IR
   → emit AST → render to strings → push `GeneratedFile`s into
   `context.GeneratedFiles`).
3. Register the plugin in `PluginsBootstrapExtensions.AddAllGeneratorPlugins`.
4. Add resx descriptions to `PluginStrings.resx` so the Web UI gets
   tooltips.

Because the orchestrator is plugin-agnostic, no other code changes are
required.

## Where to look

- `src/ValIO.Core/Plugins/IGeneratorPlugin.cs`
- `src/ValIO.Core/Plugins/IGeneratorOptionsDescriptor.cs`
- `src/ValIO.Core/Plugins/OptionDescription.cs`
- `src/ValIO.Core/Plugins/GeneratorRegistry.cs`
- `src/ValIO.Plugins.Bootstrap/PluginsBootstrapExtensions.cs`
