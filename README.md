# ValIO — OpenAPI Code Generator

Type-safe HTTP clients, models, and validation schemas for 8 target languages,
generated from any OpenAPI 3.x schema — available as a CLI, a Web app, or a
desktop app.

## Highlights

- **8 target languages** out of the box: TypeScript, C#, Java, Kotlin, Python,
  PHP, C++ (Unreal), Swift.
- **Three frontends, one engine** — `ValIO.Cli`, `ValIO.Web` (Blazor Server +
  MudBlazor), `ValIO.Desktop` (Avalonia 11), all consuming the same
  `ValIO.Application` orchestrator.
- **Output to folder or .zip** — pick a directory, save a zip, or stream the
  zip straight to an HTTP response. Driven by the `IGenerationOutputSink`
  abstraction.
- **Localized UI** — English / Русский / Deutsch / 简体中文, both in the web
  shell and the desktop client.
- **REST API** — `/api/languages`, `/api/options/{language}`, `/api/validate`,
  `/api/generate`, plus a hand-crafted OpenAPI spec at `/api/spec`.
- **TypeScript power tools**: Zod 3/4, Valibot, TypeBox; Fetch, Axios, Ky,
  Undici, ofetch, Got, Angular HttpClient, SolidStart; TanStack Query
  (React/Vue/Angular/Solid/Svelte); SWR; MSW handlers.

## Repository layout

```
ValIO.sln
├── src/
│   ├── ValIO.Core/                 — IR, AST, plugin contracts
│   ├── ValIO.Parsers/              — OpenAPI document parsing
│   ├── ValIO.Configuration/        — valio.config.{json,yaml} loader/validator
│   ├── ValIO.Application/          — IGenerationOrchestrator (the shared engine)
│   ├── ValIO.Output/               — IGenerationOutputSink (Disk / Zip / Stream / InMemory)
│   ├── ValIO.Localization/         — .resx + IConsoleReporter for en/ru/de/zh-CN
│   ├── ValIO.Plugins.Bootstrap/    — AddAllGeneratorPlugins() helper
│   ├── ValIO.Generators.{TypeScript,CSharp,Java,Kotlin,Python,Php,Cpp,Swift}/
│   ├── ValIO.Cli/                  — System.CommandLine wrapper (valio.exe)
│   ├── ValIO.Web/                  — Blazor Server + minimal-API surface
│   └── ValIO.Desktop/              — Avalonia 11 + CommunityToolkit.Mvvm
├── tests/
│   ├── ValIO.Application.Tests/    — orchestrator + output sinks
│   └── ValIO.Generators.*.Tests/   — generator unit & integration tests
└── docs-site/                      — Starlight (Astro) documentation, multilingual
```

## Requirements

- .NET 10 SDK (`10.0.100` or newer)
- Node.js 22+ if you want to run the docs site (`docs-site/`)

## Quick start

### CLI

```powershell
dotnet build
.\run.bat generate -s https://petstore3.swagger.io/api/v3/openapi.json `
                   -o ./out -l typescript
.\run.bat generate -s ./openapi.yaml -o ./out.zip -l csharp --lang ru
.\run.bat --list-languages
```

`-o` accepts either a directory or a `.zip` file (the latter triggers
`ZipFileOutputSink`).  Use `--lang` (or `VALIO_LANG=`) to switch the CLI's
UI culture.

### Web (Blazor Server + REST API)

```powershell
dotnet run --project src/ValIO.Web --launch-profile http
# http://localhost:5080
#   /                  — landing page
#   /generate          — interactive form
#   /validate          — schema validator
#   /api/spec          — OpenAPI document
#   /healthz           — health check
```

POSTing to `/api/generate` returns a `.zip` of generated files (streamed via
`ZipStreamOutputSink`).  POST `/api/validate` returns a summary of the
schema without running any plugin.

### Desktop (Avalonia 11)

```powershell
dotnet run --project src/ValIO.Desktop
```

Features: schema preview after pick/drop, recent-files menu, dark-mode toggle,
folder/zip output picker, per-plugin dynamic option form with dependency
filtering, native file pickers via `IStorageProvider`.

## Configuration file

```json
{
  "projects": {
    "client-ts": {
      "schema": "./openapi.yaml",
      "language": "typescript",
      "outputPath": "./generated/ts",
      "options": {
        "transports": ["fetch", "axios"],
        "validation": "zod4",
        "queryWrapper": "tanstack-react",
        "mocks": "msw"
      }
    },
    "client-cs": {
      "schema": "https://petstore.swagger.io/v2/swagger.json",
      "language": "csharp",
      "outputPath": "./generated/cs.zip",
      "options": { "namespace": "PetStore.Generated" }
    }
  }
}
```

Then `valio generate --config valio.config.json --projects client-ts` (or
omit `--projects` to build all of them).

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

The frontends never touch the file system or HTTP transport — they hand the
`GeneratedFile[]` to an `IOutputSinkFactory` and let it write wherever the
user asked (a folder for the CLI, an HTTP response stream for the web, a
`.zip` for "save as" in the desktop).

## Tests

```powershell
dotnet test ValIO.sln
```

12 tests cover `ValIO.Application` (orchestrator + the four output sinks);
each generator has its own integration suite under `tests/ValIO.Generators.*.Tests/`.
Total: 281 passing, 1 skipped (TS strict-mode smoke that needs `tsc` on PATH).

## Documentation site

```powershell
cd docs-site
npm install
npm run dev   # http://localhost:4321
```

Built with [Starlight](https://starlight.astro.build) (Astro). Multilingual
out of the box — `src/content/docs/{en,ru,de,zh-cn}/`.

## Technology stack

| Layer        | Pick                                                   |
|--------------|--------------------------------------------------------|
| Runtime      | .NET 10.0                                              |
| OpenAPI      | Microsoft.OpenApi 3.x                                  |
| CLI          | System.CommandLine 2.x                                 |
| Web          | ASP.NET Core 10 + Blazor Server + MudBlazor 8.x        |
| Desktop      | Avalonia 11 + CommunityToolkit.Mvvm                    |
| Localization | Microsoft.Extensions.Localization + .resx              |
| Docs         | Astro + Starlight (Node 22+)                           |
| Tests        | NUnit 4 + FluentAssertions                             |

## License

TBD.
