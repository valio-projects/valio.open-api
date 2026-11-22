# ValIO

> ValIO — type-safe HTTP clients, models, and validation schemas for 12 languages, generated from any OpenAPI 3.x schema.

[![CI](https://github.com/valio-projects/valio.open-api/actions/workflows/ci.yml/badge.svg)](https://github.com/valio-projects/valio.open-api/actions/workflows/ci.yml)
[![Release](https://github.com/valio-projects/valio.open-api/actions/workflows/release.yml/badge.svg)](https://github.com/valio-projects/valio.open-api/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/ValIO.Cli.svg)](https://www.nuget.org/packages/ValIO.Cli)
[![npm](https://img.shields.io/npm/v/@val.io/valio.svg)](https://www.npmjs.com/package/@val.io/valio)
[![License: LGPL v2.1](https://img.shields.io/badge/License-LGPL_v2.1-blue.svg)](./LICENSE)
[![Docs](https://img.shields.io/badge/docs-GitHub_Pages-blue)](https://valio-projects.github.io/valio.open-api/)

## Quick start

Install as a .NET global tool:

```bash
dotnet tool install --global ValIO.Cli
valio generate -s ./openapi.yaml -o ./out -l typescript
```

Install via npm:

```bash
npm install -g @val.io/valio
valio generate -s ./openapi.yaml -o ./out -l typescript
```

Build from source:

```bash
dotnet build
dotnet run --project src/ValIO.Cli -- generate -s ./openapi.yaml -o ./out -l typescript
```

## Why ValIO

- **12 target languages** — TypeScript, C#, Java, Kotlin, Python, PHP, C++, Swift, Rust, Ruby, F#, Dart.
- **Three frontends, one engine** — `ValIO.Cli` (System.CommandLine), `ValIO.Web` (Blazor Server + MudBlazor), `ValIO.Desktop` (Avalonia 11).
- **Plugin architecture** — every language ships as an `IGeneratorPlugin`; drop in your own without touching the core.
- **Pluggable outputs** — `IGenerationOutputSink` writes to disk, packs a zip file, streams a zip over HTTP, or stays in memory.
- **Localized UI** — English, Русский, Deutsch, 简体中文 across the CLI, web shell, and desktop app.

## Supported languages

| Language   | Default HTTP client                                                       | Validation library            | Notes                                |
|------------|---------------------------------------------------------------------------|-------------------------------|--------------------------------------|
| TypeScript | Fetch / Axios / Ky / Got / OFetch / Undici / Angular / Solid              | Zod 3/4, Valibot, TypeBox     | + TanStack Query, SWR, MSW           |
| C#         | HttpClient                                                                | None (POCOs)                  | System.Text.Json                     |
| Java       | OkHttp                                                                    | Bean Validation               | Jackson                              |
| Kotlin     | OkHttp                                                                    | None                          | Kotlinx.Serialization                |
| Python     | httpx                                                                     | Pydantic                      | async/sync                           |
| PHP        | Guzzle                                                                    | None                          | PSR-7                                |
| C++        | Unreal HTTP / cpp-httplib                                                 | None                          | `runtime: ue` (default) \| `std`: nlohmann_json, JSON-free model headers, C++17/20/23 |
| Swift      | URLSession                                                                | Codable                       | iOS/macOS                            |
| Rust       | Reqwest                                                                   | Serde                         | tokio                                |
| Ruby       | Faraday                                                                   | None                          | Ruby 3+                              |
| F#         | RestSharp                                                                 | None                          | discriminated unions                 |
| Dart       | http                                                                      | None                          | null-safe                            |

## Examples

### TypeScript (Fetch + Zod 4)

```bash
valio generate -s ./petstore.yaml -o ./out/ts -l typescript \
  --opt transports=fetch --opt validation=zod4
```

```ts
import { z } from "zod";
export const Pet = z.object({ id: z.number(), name: z.string() });
export type Pet = z.infer<typeof Pet>;
export async function getPetById(id: number): Promise<Pet> { /* ... */ }
```

### C# (HttpClient + System.Text.Json)

```bash
valio generate -s ./petstore.yaml -o ./out/cs -l csharp \
  --opt namespace=PetStore.Generated
```

```csharp
public sealed class Pet
{
    public long Id { get; init; }
    public required string Name { get; init; }
}
```

To generate PascalCase C# properties with exact JSON field names, set
`namingConvention.properties` to `PascalCase` in the C# project in `valio.config.json`.
Every property in that profile carries its original OpenAPI name as `JsonPropertyName`.
Set `options.strictStringEnums` to `true` for ordinal matching of the declared string enum values.
Set `options.pascalCaseEnumMembers` to `true` to generate C# members such as `Player` and
`InProgress` for `"player"` and `"in-progress"`. The option defaults to `false` and applies
to declarations and model defaults. Generation reports conflicting literals if two would
produce the same C# identifier.
For string enums, `options.generateXmlEnumAttributes` emits `[XmlEnum("wire-value")]` for
`XmlSerializer`. Independently, `options.generateDataContractAttributes` emits `[DataContract]`
on the enum so `DataContractSerializer` uses its `[EnumMember(Value = "wire-value")]` fields.
Both options default to `false`. To document individual values, set `x-enum-descriptions`
on the schema to an object keyed by exact wire literals or to an array in the same order and
of the same length as `enum`. Each supplied string becomes the member's XML summary in C#.
The generated client XML documentation includes the operation summary, parameter
and request-body descriptions, and the successful response description.

Set `options.generateResponseMetadata` to `true` to add a `WithResponse` method beside each
ordinary client method. `ApiResponse<T>` captures the observed status, normalized media type
and headers. `TryGetBody<TBody>` reads the typed payload selected by the declared status and JSON
media type, including an alternate error schema. `IsBodyless` confirms a declared empty response.
An unconfirmed response still carries its observed metadata.

Metadata methods send once, use paths relative to the injected `HttpClient.BaseAddress`, and apply
its timeout across headers and body. End a gateway base path with a slash. Configure the client to
disable redirects if the caller needs to observe redirect statuses. `options.responseMetadataMaxBodyBytes`
limits decoded UTF-8 JSON to 65,536 bytes by default.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Frontends:  ValIO.Cli      ValIO.Web         ValIO.Desktop     │
│              (System.       (Blazor Server +  (Avalonia 11 +    │
│               CommandLine)   minimal API)     CommunityToolkit) │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼   IGenerationOrchestrator
┌─────────────────────────────────────────────────────────────────┐
│                       ValIO.Application                         │
│   1. Parse schema (URL or file) via IOpenApiDocumentParser      │
│   2. Build IR via IrBuilder + schema resolver                   │
│   3. Resolve plugin via IGeneratorRegistry.FindPlugin           │
│   4. Run pipeline (each plugin = ordered ICodeGenerationSteps)  │
│   5. Return GenerationResult { Files, Warnings }                │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼   IGenerationOutputSink
                ┌────────────┴────────────┐
                ▼            ▼            ▼
        DiskOutputSink  ZipFileSink  ZipStreamSink / InMemorySink
```

## Project layout

```
ValIO.sln
├── src/
│   ├── ValIO.Core/                — IR, AST, plugin contracts
│   ├── ValIO.Parsers/             — OpenAPI document parsing
│   ├── ValIO.Configuration/       — valio.config.{json,yaml}
│   ├── ValIO.Application/         — orchestration engine
│   ├── ValIO.Output/              — Disk / Zip / Stream / InMemory sinks
│   ├── ValIO.Localization/        — en/ru/de/zh-CN resources
│   ├── ValIO.Plugins.Bootstrap/   — AddAllGeneratorPlugins() DI helper
│   ├── ValIO.Generators.*/        — 12 language plugins
│   ├── ValIO.Cli/                 — `valio` global tool
│   ├── ValIO.Web/                 — Blazor Server + REST API
│   └── ValIO.Desktop/             — Avalonia 11 app
├── tests/                         — NUnit 4 + FluentAssertions
├── docs/                          — design notes, ADRs
├── docs-site/                     — Starlight (Astro) documentation
├── packaging/npm/                 — npm wrapper + platform packages
└── Examples/                      — sample OpenAPI schemas + outputs
```

## Documentation

Full documentation lives at <https://valio-projects.github.io/valio.open-api/>.

Stable releases from `main` publish the .NET tool and libraries to NuGet, the
CLI to npm, and the web image to `ghcr.io/valio-projects/valio-web`. See
[CI/CD](docs/CI_CD.md) for release setup and
[web deployment](deploy/web/README.md) for the optional Docker Compose host.

To run the docs locally:

```bash
cd docs-site
npm install
npm run dev   # http://localhost:4321
```

## Contributing

Pull requests are welcome — see [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines, the development loop, and the code-of-conduct.

For the internals — how the IR, AST, builders, renderers, plugins, options and tests fit together, and
how to add a new option, transport, validation library, stack or language — read the
**[Developer Guide](./docs/DEVELOPER_GUIDE.md)**. Each project under `src/` and `tests/` also has its own
`README.md` describing its responsibility.

## License

ValIO is released under the [GNU Lesser General Public License v2.1](./LICENSE).
