---
title: Getting started
description: Install ValIO, point it at an OpenAPI schema, get clients.
translationKey: getting-started
---

ValIO ships as three front-ends backed by the same generator engine:

- **CLI** — `valio` executable (single .NET 10 binary).
- **Web** — ASP.NET Core 10 + Blazor Server (`ValIO.Web`).
- **Desktop** — Avalonia 11 (`ValIO.Desktop`), cross-platform.

## Quick CLI run

```bash
# Generate TypeScript clients into a folder
valio generate -s ./openapi.json -o ./generated -l typescript

# Same thing, but emit a zip archive
valio generate -s ./openapi.json -o ./generated.zip -l typescript

# Or use --zip and the .zip suffix is added for you
valio generate -s ./openapi.json -o ./generated --zip -l typescript

# Switch UI language (CLI messages + UI in Web/Desktop)
valio --lang ru generate -s ./openapi.json -o ./generated -l typescript
```

## Supported languages

| Language id   | Target                              |
|---------------|-------------------------------------|
| `typescript`  | TS clients (fetch / axios / ky / undici / ofetch / got / Angular / Solid) + Zod / Valibot / TypeBox schemas + TanStack Query / SWR / MSW |
| `csharp`      | C# DTOs and clients                 |
| `java`        | Java 21 + OkHttp                    |
| `kotlin`      | Kotlin + OkHttp + coroutines        |
| `python`      | Python 3.10+ + httpx + Pydantic     |
| `php`         | PHP 8 + Guzzle                      |
| `swift`       | Swift + URLSession                  |
| `cpp`         | Unreal Engine flavored C++          |

## Next steps

- [CLI reference](/cli/) — all `valio` subcommands and options
- [Web UI](/web/) — running the browser front-end
- [Desktop app](/desktop/) — running the Avalonia front-end
- [Configuration file](/configuration/) — multi-project `valio.config.json`
- [Contributing](/contributing/) — coding rules and plugin authoring
