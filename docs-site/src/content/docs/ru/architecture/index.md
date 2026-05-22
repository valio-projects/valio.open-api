---
title: Архитектура
description: Как устроена кодовая база.
translationKey: architecture
---

ValIO — это .NET 10 monorepo. Каждый фронтенд (CLI / Web / Desktop) —
тонкая оболочка над общим движком генерации.

## Структура решения

```
src/
├── ValIO.Core/              — IR, AST, контракты плагинов
├── ValIO.Parsers/           — парсер OpenAPI-документов
├── ValIO.Configuration/     — (де)сериализация и валидация valio.config.json
├── ValIO.Application/       — IGenerationOrchestrator + расширения DI
├── ValIO.Output/            — IGenerationOutputSink (Disk/Zip/Stream/InMemory)
├── ValIO.Localization/      — .resx-строки + IConsoleReporter
├── ValIO.Plugins.Bootstrap/ — AddAllGeneratorPlugins() в одну строку
│
├── ValIO.Generators.TypeScript/   — плагин TS
├── ValIO.Generators.CSharp/       — плагин C#
├── ValIO.Generators.Java/         — плагин Java
├── ValIO.Generators.Kotlin/       — плагин Kotlin
├── ValIO.Generators.Python/       — плагин Python
├── ValIO.Generators.Php/          — плагин PHP
├── ValIO.Generators.Swift/        — плагин Swift
├── ValIO.Generators.Cpp/          — плагин C++ для Unreal
│
├── ValIO.Cli/               — фронтенд на System.CommandLine (valio.exe)
├── ValIO.Web/               — фронтенд Blazor Server + MudBlazor
└── ValIO.Desktop/           — фронтенд Avalonia 11
```

## Поток генерации

```
        ┌──────────────┐
        │ Фронтенд     │  CLI / Web / Desktop — собирает GenerationConfiguration
        └──────┬───────┘
               │ IGenerationOrchestrator.GenerateAsync()
               ▼
       ┌──────────────┐
       │ Application  │
       └──────┬───────┘
              │   1. IOpenApiDocumentParser разбирает схему
              │   2. IrBuilder строит IR
              │   3. plugin.CreatePipeline(services).ExecuteAsync()
              ▼
        OrchestrationResult { Files, Warnings, Error }
              │
              │  Фронтенд выбирает приёмник вывода:
              ▼
       ┌───────────────────────┐
       │ ValIO.Output          │
       │  - DiskOutputSink     │
       │  - ZipFileOutputSink  │
       │  - ZipStreamOutputSink│
       │  - InMemoryOutputSink │
       └───────────────────────┘
```

Оркестратор никогда не работает с диском и не возвращает состояние,
завязанное на IO, поэтому его можно безопасно вызывать одновременно
из веб-circuit'а и из view-модели десктопа.

## С чего начать чтение

- `ValIO.Core/Plugins/IGeneratorPlugin.cs` — контракт плагина
- `ValIO.Core/Plugins/ICodeGenerationPipeline.cs` — пайплайн шагов
- `ValIO.Application/Services/GenerationOrchestrator.cs` — связывает всё вместе
- `ValIO.Output/Abstractions/IGenerationOutputSink.cs` — контракт приёмника
