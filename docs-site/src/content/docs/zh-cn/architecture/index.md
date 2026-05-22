---
title: 架构
description: 代码库的组织方式。
translationKey: architecture
---

ValIO 是一个 .NET 10 monorepo。每个前端（CLI / Web / Desktop）都是
基于共享生成器内核的一层轻量壳。

## 解决方案结构

```
src/
├── ValIO.Core/              —— IR、AST、插件契约
├── ValIO.Parsers/           —— OpenAPI 文档解析器
├── ValIO.Configuration/     —— valio.config.json 的（反）序列化与校验
├── ValIO.Application/       —— IGenerationOrchestrator + DI 扩展
├── ValIO.Output/            —— IGenerationOutputSink（Disk/Zip/Stream/InMemory）
├── ValIO.Localization/      —— .resx 字符串 + IConsoleReporter
├── ValIO.Plugins.Bootstrap/ —— 一行调用的 AddAllGeneratorPlugins()
│
├── ValIO.Generators.TypeScript/   —— TS 插件
├── ValIO.Generators.CSharp/       —— C# 插件
├── ValIO.Generators.Java/         —— Java 插件
├── ValIO.Generators.Kotlin/       —— Kotlin 插件
├── ValIO.Generators.Python/       —— Python 插件
├── ValIO.Generators.Php/          —— PHP 插件
├── ValIO.Generators.Swift/        —— Swift 插件
├── ValIO.Generators.Cpp/          —— 面向 Unreal 的 C++ 插件
│
├── ValIO.Cli/               —— 基于 System.CommandLine 的前端（valio.exe）
├── ValIO.Web/               —— Blazor Server + MudBlazor 前端
└── ValIO.Desktop/           —— Avalonia 11 前端
```

## 生成流程

```
        ┌──────────────┐
        │ 前端         │  CLI / Web / Desktop —— 构建 GenerationConfiguration
        └──────┬───────┘
               │ IGenerationOrchestrator.GenerateAsync()
               ▼
       ┌──────────────┐
       │ Application  │
       └──────┬───────┘
              │   1. IOpenApiDocumentParser 解析架构
              │   2. IrBuilder 构建 IR
              │   3. plugin.CreatePipeline(services).ExecuteAsync()
              ▼
        OrchestrationResult { Files, Warnings, Error }
              │
              │  前端选择一个输出 sink：
              ▼
       ┌───────────────────────┐
       │ ValIO.Output          │
       │  - DiskOutputSink     │
       │  - ZipFileOutputSink  │
       │  - ZipStreamOutputSink│
       │  - InMemoryOutputSink │
       └───────────────────────┘
```

Orchestrator 完全不接触磁盘，也不会返回任何与 IO 绑定的状态，因此
可以在 Web circuit 和桌面 view model 中安全地并发调用。

## 从何处开始阅读

- `ValIO.Core/Plugins/IGeneratorPlugin.cs` —— 插件契约
- `ValIO.Core/Plugins/ICodeGenerationPipeline.cs` —— 步骤管道
- `ValIO.Application/Services/GenerationOrchestrator.cs` —— 将各部分串联
- `ValIO.Output/Abstractions/IGenerationOutputSink.cs` —— sink 契约
