---
title: Architektur
description: Wie die Codebasis aufgebaut ist.
translationKey: architecture
---

ValIO ist ein .NET-10-Monorepo. Jedes Frontend (CLI / Web / Desktop)
ist eine dünne Hülle über demselben Generator-Engine.

## Solution-Aufbau

```
src/
├── ValIO.Core/              — IR, AST, Plugin-Verträge
├── ValIO.Parsers/           — OpenAPI-Dokument-Parser
├── ValIO.Configuration/     — (De-)Serialisierung + Validierung von valio.config.json
├── ValIO.Application/       — IGenerationOrchestrator + DI-Erweiterungen
├── ValIO.Output/            — IGenerationOutputSink (Disk/Zip/Stream/InMemory)
├── ValIO.Localization/      — .resx-Strings + IConsoleReporter
├── ValIO.Plugins.Bootstrap/ — AddAllGeneratorPlugins() in einer Zeile
│
├── ValIO.Generators.TypeScript/   — TS-Plugin
├── ValIO.Generators.CSharp/       — C#-Plugin
├── ValIO.Generators.Java/         — Java-Plugin
├── ValIO.Generators.Kotlin/       — Kotlin-Plugin
├── ValIO.Generators.Python/       — Python-Plugin
├── ValIO.Generators.Php/          — PHP-Plugin
├── ValIO.Generators.Swift/        — Swift-Plugin
├── ValIO.Generators.Cpp/          — C++-Plugin für Unreal
│
├── ValIO.Cli/               — System.CommandLine-Frontend (valio.exe)
├── ValIO.Web/               — Blazor-Server- + MudBlazor-Frontend
└── ValIO.Desktop/           — Avalonia-11-Frontend
```

## Generierungs-Pipeline

```
        ┌──────────────┐
        │ Frontend     │  CLI / Web / Desktop — baut GenerationConfiguration
        └──────┬───────┘
               │ IGenerationOrchestrator.GenerateAsync()
               ▼
       ┌──────────────┐
       │ Application  │
       └──────┬───────┘
              │   1. IOpenApiDocumentParser parst das Schema
              │   2. IrBuilder erzeugt die IR
              │   3. plugin.CreatePipeline(services).ExecuteAsync()
              ▼
        OrchestrationResult { Files, Warnings, Error }
              │
              │  Frontend wählt einen Output-Sink:
              ▼
       ┌───────────────────────┐
       │ ValIO.Output          │
       │  - DiskOutputSink     │
       │  - ZipFileOutputSink  │
       │  - ZipStreamOutputSink│
       │  - InMemoryOutputSink │
       └───────────────────────┘
```

Der Orchestrator berührt nie die Festplatte und gibt keinen
IO-gebundenen Zustand zurück — er kann also gefahrlos gleichzeitig
aus einem Web-Circuit und einem Desktop-ViewModel aufgerufen werden.

## Wo zuerst hinschauen

- `ValIO.Core/Plugins/IGeneratorPlugin.cs` — Plugin-Vertrag
- `ValIO.Core/Plugins/ICodeGenerationPipeline.cs` — Pipeline aus Schritten
- `ValIO.Application/Services/GenerationOrchestrator.cs` — verbindet alles
- `ValIO.Output/Abstractions/IGenerationOutputSink.cs` — Sink-Vertrag
