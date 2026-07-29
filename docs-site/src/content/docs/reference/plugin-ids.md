---
title: Plugin IDs
description: Every generator plugin shipped today, with its canonical ID and aliases.
---

These are the language IDs accepted by the CLI's `-l` / `--language`
flag and by the `language` key in `valio.config.json`. Aliases resolve
to the same plugin (case-insensitive).

| `LanguageId` | Display name | Aliases                       | Main capabilities                                                                 |
|--------------|--------------|-------------------------------|-----------------------------------------------------------------------------------|
| `typescript` | TypeScript   | `ts`                          | 9 HTTP transports (fetch, axios, ky, undici, ofetch, got, angular-rxjs/promise, solid); Zod/Valibot/TypeBox schemas; TanStack Query (React/Vue/Angular/Solid/Svelte) and SWR wrappers; MSW mocks. |
| `csharp`     | C#           | `cs`, `c#`                    | DTOs (class/record/struct/record-struct), `HttpClient` clients, DataAnnotations validation, OpenTelemetry traces & metrics. |
| `java`       | Java         | `java21`, `java-okhttp`       | Java 21 records + OkHttp clients.                                                  |
| `kotlin`     | Kotlin       | `kt`                          | Kotlin data classes + OkHttp + coroutines, kotlinx-serialization or moshi.         |
| `python`     | Python       | `py`, `python3`, `py3`        | Pydantic v2 models + httpx (async or sync).                                        |
| `php`        | PHP          | `php8`, `php-guzzle`          | PHP 8 classes + Guzzle.                                                            |
| `swift`      | Swift        | —                             | Swift `Codable` types + URLSession async/await.                                    |
| `dart`       | Dart         | `dart-dio`                    | Dart classes + `dio` clients.                                                      |
| `fsharp`     | F#           | `fs`, `f#`                    | F# records + `HttpClient`.                                                         |
| `ruby`       | Ruby         | `rb`, `ruby-faraday`          | Ruby classes + Faraday.                                                            |
| `rust`       | Rust         | `rs`, `rust-reqwest`          | Rust structs (`serde`) + `reqwest`.                                                |

Registration lives in
[`src/ValIO.Plugins.Bootstrap/PluginsBootstrapExtensions.cs`](https://github.com/valio-projects/valio.open-api/blob/main/src/ValIO.Plugins.Bootstrap/PluginsBootstrapExtensions.cs).
Every front-end (CLI / Web / Desktop) calls `AddAllGeneratorPlugins()`
to register the full list above.

## Picking a language

```bash
# Canonical
valio generate -s o.json -o ./out -l typescript

# Alias works too — case-insensitive
valio generate -s o.json -o ./out -l TS
```

In a config file:

```json
{
  "projects": {
    "web": {
      "language": "typescript",
      "schema":   "./openapi.json",
      "outputPath": "./out"
    }
  }
}
```

## Adding a new plugin

A new entry in this table corresponds to:

1. A new project under `src/ValIO.Generators.<Lang>` with an
   `IGeneratorPlugin` implementation.
2. A registration line in `PluginsBootstrapExtensions`.
3. (Optional) Aliases in `LanguageAliases`.

See [Plugin contract](/architecture/plugin-contract/) for the
full step-by-step.
