---
title: Localization
description: How ValIO's resx-based messages reach the user.
---

ValIO ships in four locales — `en`, `ru`, `de`, `zh-CN`. The
mechanism is standard .NET satellite assemblies built from `.resx`
files; no third-party translation framework is involved.

## Resource layout

```
src/ValIO.Localization/Resources/
├── ConfigurationStrings.resx       ← English (default)
├── GenerationStrings.resx
├── GenerationStrings.ru.resx       ← Russian overrides
├── GenerationStrings.de.resx
├── GenerationStrings.zh-CN.resx
├── ParserStrings.resx
├── PluginStrings.resx
├── ValidationStrings.resx
├── ValidationStrings.ru.resx
├── ValidationStrings.de.resx
└── ValidationStrings.zh-CN.resx
```

Each base file is English. Locale-suffixed files override individual
keys; missing keys fall back to English at runtime via
`ResourceManager`.

The five namespaces correspond to subsystems:

| File                       | Subsystem                                          |
|----------------------------|----------------------------------------------------|
| `GenerationStrings.resx`   | Orchestrator status messages (`generation.*`).     |
| `ParserStrings.resx`       | OpenAPI parser diagnostics (`parser.*`).           |
| `ValidationStrings.resx`   | Configuration validation messages (`validation.*`). |
| `ConfigurationStrings.resx`| Config loader errors (`configuration.*`).          |
| `PluginStrings.resx`       | Plugin option descriptions (UI tooltips).          |

## Registration

`AddValIOLocalization` is the single DI entry point:

```csharp
public static IServiceCollection AddValIOLocalization(this IServiceCollection services)
{
    services.AddLocalization(options => options.ResourcesPath = "Resources");
    services.AddSingleton<IConsoleReporter, StdoutConsoleReporter>();
    return services;
}
```

(Source: `src/ValIO.Localization/DependencyInjection/LocalizationServiceCollectionExtensions.cs`.)

## Reporter

User-facing messages don't go through `ILogger<T>` (which is for
diagnostics) but through `IConsoleReporter`:

```csharp
public interface IConsoleReporter
{
    void Info(string localizedMessage);
    void Warn(string localizedMessage);
    void Error(string localizedMessage);
}
```

The CLI binds it to `StdoutConsoleReporter` (writes to stdout/stderr
with simple coloring). The Web and Desktop front-ends each register
their own implementation that pushes the message into a UI surface
(snackbar, log panel).

## Setting the culture

The CLI parses `--lang` *before* DI build so that
`CultureInfo.DefaultThreadCurrentUICulture` is set when the resx
manager first resolves a key:

```csharp
var culture = CultureInfo.GetCultureInfo(lang);
CultureInfo.DefaultThreadCurrentCulture   = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
```

(Source: `src/ValIO.Cli/Program.cs`.)

The Web app sets a cookie and reloads with the chosen culture; the
Desktop app persists it to `desktop-settings.json` and re-applies it on
startup.

## Adding a new locale

1. Copy `GenerationStrings.resx` to `GenerationStrings.<culture>.resx`
   (e.g. `GenerationStrings.fr.resx` for French).
2. Translate the `<value>` elements; leave `<data name="…">` keys
   untouched. Untranslated keys fall back to English.
3. Repeat for any other `*.resx` files you want covered.
4. Update the `locales` map in `docs-site/astro.config.mjs` if you
   want a docs locale match.
5. Add the new culture code to `RootCommand`'s `--lang` description so
   `--help` lists it.

No code changes are required for the runtime to pick up the new
satellite assembly — the build wires it up by convention.

## Where to look

- `src/ValIO.Localization/Resources/`
- `src/ValIO.Localization/Reporters/IConsoleReporter.cs`
- `src/ValIO.Localization/Reporters/StdoutConsoleReporter.cs`
- `src/ValIO.Cli/Program.cs` (culture initialization)
