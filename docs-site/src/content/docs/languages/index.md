---
title: Supported languages
description: Target language matrix and per-plugin options.
---

ValIO is plugin-based: each generator lives in its own assembly and is
registered through `AddAllGeneratorPlugins()`. The full set of plugins
shipped today:

| Plugin                       | `languageId` | Aliases                | Notes |
|------------------------------|--------------|------------------------|-------|
| ValIO.Generators.TypeScript  | `typescript` | `ts`                   | 8 HTTP transports, Zod / Valibot / TypeBox, TanStack Query / SWR, MSW |
| ValIO.Generators.CSharp      | `csharp`     | `cs`, `c#`             | DTOs + clients |
| ValIO.Generators.Java        | `java`       | `java21`, `java-okhttp`| Java + OkHttp |
| ValIO.Generators.Kotlin      | `kotlin`     | `kt`                   | Kotlin + OkHttp + coroutines + kotlinx/moshi |
| ValIO.Generators.Python      | `python`     | `py`, `py3`            | httpx async/sync + Pydantic |
| ValIO.Generators.Php         | `php`        | `php8`, `php-guzzle`   | Guzzle |
| ValIO.Generators.Swift       | `swift`      |                        | URLSession |
| ValIO.Generators.Cpp         | `cpp`        |                        | Unreal Engine flavored |

## Per-plugin options

Each plugin exposes an `OptionsDescriptor.GetOptionDescriptions()` list.
The Web UI uses it to render a dynamic form. From CLI and config files
the same options are passed under `"options"` in `valio.config.json`.

The full per-plugin reference lives in individual pages — see the
sidebar — and the values are also surfaced in the Web UI tooltips via
`OptionDescription.DescriptionKey` resource lookups.
