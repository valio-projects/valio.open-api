---
title: Architecture
description: How the codebase is laid out.
---

ValIO is a .NET 10 monorepo. Each front-end (CLI / Web / Desktop) is a
thin shell over the shared generator engine.

## Solution layout

```
src/
├── ValIO.Core/              — IR, AST, plugin contracts
├── ValIO.Parsers/           — OpenAPI document parser
├── ValIO.Configuration/     — valio.config.json (de)serialization + validation
├── ValIO.Application/       — IGenerationOrchestrator + DI extensions
├── ValIO.Output/            — IGenerationOutputSink (Disk/Zip/Stream/InMemory)
├── ValIO.Localization/      — .resx strings + IConsoleReporter
├── ValIO.Plugins.Bootstrap/ — AddAllGeneratorPlugins() in one line
│
├── ValIO.Generators.TypeScript/   — TS plugin
├── ValIO.Generators.CSharp/       — C# plugin
├── ValIO.Generators.Java/         — Java plugin
├── ValIO.Generators.Kotlin/       — Kotlin plugin
├── ValIO.Generators.Python/       — Python plugin
├── ValIO.Generators.Php/          — PHP plugin
├── ValIO.Generators.Swift/        — Swift plugin
├── ValIO.Generators.Cpp/          — Unreal-flavored C++ plugin
│
├── ValIO.Cli/               — System.CommandLine front-end (valio.exe)
├── ValIO.Web/               — Blazor Server + MudBlazor front-end
└── ValIO.Desktop/           — Avalonia 11 front-end
```

## Generation flow

```
        ┌──────────────┐
        │ Front-end    │  CLI / Web / Desktop — builds GenerationConfiguration
        └──────┬───────┘
               │ IGenerationOrchestrator.GenerateAsync()
               ▼
       ┌──────────────┐
       │ Application  │
       └──────┬───────┘
              │   1. IOpenApiDocumentParser parses schema
              │   2. IrBuilder builds IR
              │   3. plugin.CreatePipeline(services).ExecuteAsync()
              ▼
        OrchestrationResult { Files, Warnings, Error }
              │
              │  Front-end picks an output sink:
              ▼
       ┌───────────────────────┐
       │ ValIO.Output          │
       │  - DiskOutputSink     │
       │  - ZipFileOutputSink  │
       │  - ZipStreamOutputSink│
       │  - InMemoryOutputSink │
       └───────────────────────┘
```

The orchestrator never touches disk or returns IO-bound state, so it's
safe to call concurrently from a web circuit and from a desktop view
model at the same time.

## Where to look first

- `ValIO.Core/Plugins/IGeneratorPlugin.cs` — plugin contract
- `ValIO.Core/Plugins/ICodeGenerationPipeline.cs` — pipeline of steps
- `ValIO.Application/Services/GenerationOrchestrator.cs` — wires it together
- `ValIO.Output/Abstractions/IGenerationOutputSink.cs` — sink contract
