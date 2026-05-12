# ValIO — Глобальный план рефакторинга и расширения

> **Статус:** Plan v1.0  
> **Целевая платформа:** .NET 10 / C# 13  
> **Принципы:** SOLID, GRASP, DRY, KISS, GoF Design Patterns

---

## Содержание

1. [Текущее состояние архитектуры](#1-текущее-состояние-архитектуры)
2. [Диагностика проблем](#2-диагностика-проблем)
3. [Целевая архитектура](#3-целевая-архитектура)
4. [Структура проектов (целевая)](#4-структура-проектов-целевая)
5. [Карта зависимостей](#5-карта-зависимостей)
6. [Roadmap этапов](#6-roadmap-этапов)
7. [Матрица стеков и генераторов](#7-матрица-стеков-и-генераторов)

---

## 1. Текущее состояние архитектуры

### Pipeline генерации (существующий)

```
OpenAPI Document
      │
      ▼
 IOpenApiDocumentParser
      │
      ▼
   IrBuilder  ──────────────────────── IR (IrDocument, IrModel, IrClient, ...)
      │
      ▼
 [Language]AstBuilder  ──────────────  AST (SourceFileNode, ...)
      │
      ▼
 [Language]AstRenderer  ────────────   String (code)
      │
      ▼
   IFileWriter  ──────────────────────  Files on disk
```

### Структура существующих проектов

| Проект | Назначение |
|--------|-----------|
| `ValIO` | CLI entry point, DI, Commands |
| `ValIO.Core` | IR, AST (base), Interfaces, Services |
| `ValIO.Configuration` | Config loading/parsing (JSON/YAML) |
| `ValIO.Parsers` | OpenAPI parsing |
| `ValIO.Generators.CSharp` | C# code generation |
| `ValIO.Generators.TypeScript` | TypeScript code generation (Fetch, Axios, Zod, TanStack) |
| `ValIO.Generators.Cpp` | C++/Unreal Engine code generation |

---

## 2. Диагностика проблем

### 2.1 `AstNodeKind` — лишняя абстракция

**Проблема:** `enum AstNodeKind` содержит 130+ значений для всех языков (C#, TypeScript, C++, Zod, Axios, TanStack, UE). Используется **только** в `BaseAstVisitor` для switch-dispatch вместо полиморфизма.

```csharp
// Текущий антипаттерн (BaseAstVisitor.cs):
public virtual TResult VisitType(TypeNode node) {
    return node.Kind switch {          // <-- type-tag dispatch
        AstNodeKind.RawType       => VisitRawType((RawTypeNode)node),
        AstNodeKind.SimpleType    => VisitSimpleType((SimpleTypeNode)node),
        // ...
    };
}
```

**Решение:** Полный переход на double-dispatch через `Accept`. Каждый узел сам вызывает нужный метод visitor'а — switch больше не нужен. `AstNodeKind` удаляется полностью.

```csharp
// Целевой паттерн (чистый Visitor):
public sealed class SimpleTypeNode : TypeNode {
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) 
        => visitor.VisitSimpleType(this);   // <-- тип определяет диспетчер сам
}
```

### 2.2 `ConfigurationLoader` — монолитный парсинг всех стеков

**Проблема:** `ConfigurationLoader.MapToGenerationConfiguration()` содержит явный парсинг опций для каждого языка/библиотеки:
- `ParseCSharpOptions()` — 200+ строк
- `ParseTypeScriptOptions()` — 150+ строк  
- `ParseHttpTransports()`, `ParseCppOptions()` и т.д.

При добавлении нового генератора/стека нужно **редактировать** `ConfigurationLoader` — нарушение OCP (Open/Closed Principle).

**Решение:** Паттерн **Self-registering Options Parser** (Visitor + Chain of Responsibility). Каждый генератор сам регистрирует свой `IGeneratorOptionsDescriptor`, который содержит: парсинг конфига, CLI аргументы, help-текст, валидацию.

### 2.3 `CodeGeneratorFactory` — switch по языкам

**Проблема:**
```csharp
return language switch {
    GenerationLanguage.CSharp => ...,
    GenerationLanguage.TypeScript => ...,
    GenerationLanguage.Cpp => ...,
    _ => throw new NotSupportedException(...)  // нужно редактировать при добавлении языка
};
```

**Решение:** Registry-паттерн — генераторы сами регистрируются в `IGeneratorRegistry`.

### 2.4 `ValidationLibrary` и `QueryWrapperLibrary` в Core

**Проблема:** Core знает о конкретных библиотеках (`Zod`, `Yup`, `TanStackQuery`). Новые библиотеки (Valibot, TypeBox, Ky и т.д.) требуют изменения Core.

**Решение:** Заменить enum'ы строками-идентификаторами. Каждый плагин/генератор регистрирует поддерживаемые идентификаторы.

### 2.5 Дублирование AST-нод между стеками

**Проблема:** Каждый TS-стек (Axios, Zod, TanStack) имеет свои Visitor/Renderer иерархии с большим overlapping. `AxiosAstVisitorBase`, `ZodAstVisitorBase`, `TanStackQueryAstVisitorBase` — каждый вынужден reimplementировать базовые TS методы.

**Решение:** Все специфичные для стека ноды (AxiosCallExpressionNode, ZodObjectNode и т.д.) рендерятся через единый `TypeScriptAstRenderer`, расширяемый через паттерн **Chain of Responsibility** / **Strategy** для стек-специфичных нод.

### 2.6 `TypeScriptCodeGenerator` — God Object

**Проблема:** `TypeScriptCodeGenerator` напрямую создаёт и вызывает все pipeline-компоненты (ZodSchemaRenderer, TanStackQueryHookAstBuilder, TypeScriptParamsAstBuilder и т.д.) — нарушение SRP.

**Решение:** Декомпозиция на **Pipeline Steps** через паттерн **Chain of Responsibility**. Каждый шаг (ModelsStep, EnumsStep, ZodSchemasStep, TanStackStep и т.д.) — самостоятельный компонент, регистрируемый в pipeline.

---

## 3. Целевая архитектура

### 3.1 Plugin/Extension Model

```
IGeneratorPlugin  (регистрирует всё что нужно для генерации)
    ├── IGeneratorOptionsDescriptor  (парсинг конфига, CLI args, help)
    ├── ICodeGenerationPipeline      (список шагов генерации)
    ├── IGeneratorRegistry entry     (какие языки/транспорты поддерживает)
    └── IAstRendererExtension[]      (расширения рендерера для своих нод)
```

### 3.2 Новый Pipeline (самодостаточные шаги)

```
OpenApiDocument
    │
    ▼
IrBuilder → IrDocument
    │
    ▼  foreach ICodeGenerationStep (ordered, registered by plugins)
    ├── ModelsStep          → models/*.ts
    ├── EnumsStep           → models/*/enum.ts
    ├── ZodSchemasStep      → models/*.schema.ts
    ├── ValibotSchemasStep  → models/*.schema.ts
    ├── TypeBoxSchemasStep  → models/*.schema.ts
    ├── FetchClientStep     → api/*.fetch.ts
    ├── AxiosClientStep     → api/*.axios.ts
    ├── KyClientStep        → api/*.ky.ts
    ├── UndiciClientStep    → api/*.undici.ts
    ├── TanStackReactStep   → api/*.query.tsx
    ├── TanStackVueStep     → api/*.query.ts
    ├── TanStackAngularStep → api/*.query.ts
    ├── MSWStep             → mocks/*.msw.ts
    └── ...
```

### 3.3 Самостоятельная регистрация опций

```csharp
// Каждый плагин регистрирует свой дескриптор:
services.AddGeneratorPlugin<TypeScriptFetchPlugin>();
services.AddGeneratorPlugin<TypeScriptAxiosPlugin>();
services.AddGeneratorPlugin<TypeScriptKyPlugin>();
services.AddGeneratorPlugin<ZodPlugin>();
services.AddGeneratorPlugin<ValibotPlugin>();
services.AddGeneratorPlugin<TanStackQueryReactPlugin>();
services.AddGeneratorPlugin<MswPlugin>();
// ...и т.д.
```

---

## 4. Структура проектов (целевая)

```
ValIO.sln
│
├── ValIO/                              # CLI entry point
│   ├── Program.cs
│   ├── CLI/
│   │   ├── RootCommand.cs
│   │   ├── GenerateCommand.cs          # refactored — не знает о конкретных генераторах
│   │   └── ValidateCommand.cs
│   └── ServiceRegistration/
│       └── ServiceCollectionExtensions.cs  # регистрирует плагины
│
├── ValIO.Core/                         # Language-agnostic ядро
│   ├── IR/                             # Intermediate Representation
│   │   ├── IrDocument.cs
│   │   ├── IrModel.cs
│   │   ├── IrClient.cs
│   │   ├── IrOperation.cs
│   │   ├── IrEnum.cs
│   │   ├── IrProperty.cs
│   │   ├── Types/
│   │   └── Builders/IrBuilder.cs
│   │
│   ├── Ast/                            # Base AST (language-agnostic)
│   │   ├── Nodes/                      # Базовые ноды (без Kind!)
│   │   │   ├── AstNode.cs              # abstract, без AstNodeKind
│   │   │   ├── Expressions/
│   │   │   ├── Statements/
│   │   │   ├── Types/
│   │   │   └── Http/
│   │   ├── Visitors/
│   │   │   ├── IAstVisitor.cs          # чистый Visitor без switch
│   │   │   ├── IBaseAstVisitor.cs
│   │   │   └── BaseAstVisitor.cs       # abstract, без switch on Kind
│   │   └── Utilities/
│   │       └── AstNodeCloner.cs
│   │
│   ├── Plugins/                        # NEW: Plugin contracts
│   │   ├── IGeneratorPlugin.cs
│   │   ├── IGeneratorOptionsDescriptor.cs
│   │   ├── ICodeGenerationPipeline.cs
│   │   ├── ICodeGenerationStep.cs
│   │   ├── IGeneratorRegistry.cs
│   │   └── GeneratorRegistry.cs
│   │
│   ├── Interfaces/                     # Общие интерфейсы
│   ├── Models/                         # Общие модели (без ValidationLibrary enum!)
│   │   ├── GenerationConfiguration.cs
│   │   ├── GenerationResult.cs
│   │   ├── FileOrganization.cs
│   │   └── NamingConventions.cs
│   └── Services/
│       ├── BaseCodeGenerator.cs
│       ├── IrBuilder.cs
│       ├── NamingConverter.cs
│       └── FileWriter.cs
│
├── ValIO.Configuration/                # Config loading (рефакторинг)
│   ├── Models/
│   │   ├── ValIOConfig.cs
│   │   └── ProjectConfig.cs            # Options как Dictionary<string, object>
│   ├── Services/
│   │   ├── ConfigurationLoader.cs      # без явного парсинга стеков
│   │   └── ConfigurationValidator.cs
│   └── Interfaces/
│
├── ValIO.Parsers/                       # OpenAPI parsing (без изменений)
│
├── ValIO.Generators.CSharp/            # C# generator plugin
│   ├── CSharpGeneratorPlugin.cs        # IGeneratorPlugin implementation
│   ├── CSharpOptionsDescriptor.cs      # регистрирует свои опции
│   ├── Steps/
│   │   ├── CSharpModelsStep.cs
│   │   ├── CSharpEnumsStep.cs
│   │   └── CSharpClientsStep.cs        # raw HttpClient
│   ├── Ast/
│   │   ├── Nodes/
│   │   ├── Builders/CSharpAstBuilder.cs
│   │   ├── Renderers/CSharpAstRenderer.cs
│   │   └── Visitors/
│   └── Services/
│
├── ValIO.Generators.TypeScript/        # TypeScript base + Fetch/Axios
│   ├── TypeScriptGeneratorPlugin.cs
│   ├── Options/
│   │   ├── TypeScriptOptionsDescriptor.cs
│   │   └── TypeScriptGenerationOptions.cs
│   ├── Steps/
│   │   ├── TypeScriptModelsStep.cs
│   │   ├── TypeScriptEnumsStep.cs
│   │   ├── TypeScriptFetchClientStep.cs
│   │   └── TypeScriptAxiosClientStep.cs
│   ├── Transports/
│   │   ├── Fetch/
│   │   └── Axios/
│   ├── Ast/
│   │   ├── Nodes/
│   │   ├── Builders/TypeScriptAstBuilder.cs
│   │   ├── Renderers/TypeScriptAstRenderer.cs  # extensible via INodeRenderer
│   │   └── Visitors/
│   └── Services/
│
├── ValIO.Generators.TypeScript.Ky/      # NEW: Ky HTTP client
├── ValIO.Generators.TypeScript.Undici/  # NEW: Undici HTTP client
├── ValIO.Generators.TypeScript.Ofetch/  # NEW: Ofetch HTTP client
├── ValIO.Generators.TypeScript.Got/     # NEW: Got HTTP client
│
├── ValIO.Generators.TypeScript.Zod/     # Zod v3 + v4 validation
├── ValIO.Generators.TypeScript.Valibot/ # NEW: Valibot validation
├── ValIO.Generators.TypeScript.TypeBox/ # NEW: TypeBox validation
│
├── ValIO.Generators.TypeScript.Angular/ # NEW: Angular HttpClient (RxJS + Promise)
├── ValIO.Generators.TypeScript.Solid/   # NEW: SolidStart
├── ValIO.Generators.TypeScript.Vue/     # NEW: Vue + TanStack Query
├── ValIO.Generators.TypeScript.Svelte/  # NEW: Svelte + TanStack Query
│
├── ValIO.Generators.TypeScript.TanStack/ # TanStack Query (React, Vue, Angular, Solid, Svelte)
├── ValIO.Generators.TypeScript.MSW/      # NEW: Mock Service Worker
│
├── ValIO.Generators.CSharp.RestSharp/    # NEW: RestSharp client
│
├── ValIO.Generators.Cpp/                 # C++/Unreal Engine
│
├── ValIO.Generators.PHP/                 # NEW: PHP
│   ├── ValIO.Generators.PHP.Guzzle/
│   └── ValIO.Generators.PHP.Laravel/
│
├── ValIO.Generators.Python/              # NEW: Python
│   ├── ValIO.Generators.Python.Requests/ # sync requests
│   ├── ValIO.Generators.Python.Httpx/    # HTTPX sync + async
│   └── ValIO.Generators.Python.Aiohttp/  # aiohttp async
│
├── ValIO.Generators.Java/                # NEW: Java
│   ├── ValIO.Generators.Java.Unirest/
│   ├── ValIO.Generators.Java.AsyncHttp/
│   └── ValIO.Generators.Java.OkHttp/
│
├── ValIO.Generators.Kotlin/              # NEW: Kotlin
│   └── ValIO.Generators.Kotlin.OkHttp/
│
├── ValIO.Generators.Swift/               # NEW: Swift
│   └── ValIO.Generators.Swift.NSURLSession/
│
└── ValIO.Generators.Dart/                # NEW: Dart
    └── ValIO.Generators.Dart.Http/       # package:http + dio
```

---

## 5. Карта зависимостей

```
                    ┌─────────────────┐
                    │   ValIO (CLI)   │
                    └────────┬────────┘
                             │ depends on
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
    ┌──────────────┐  ┌──────────┐  ┌────────────┐
    │ValIO.Config  │  │ValIO.    │  │ValIO.Core  │◄──────────────────┐
    │              │  │Parsers   │  │            │                   │
    └──────────────┘  └──────────┘  └──────┬─────┘                  │
                                           │                        │
                    ┌──────────────────────┤                        │
                    │         depends on ValIO.Core                  │
                    ▼                      ▼                        │
        ┌───────────────────┐  ┌─────────────────────┐             │
        │ValIO.Generators.  │  │ValIO.Generators.    │             │
        │CSharp             │  │TypeScript           │             │
        └───────────────────┘  └──────────┬──────────┘             │
                                          │                        │
                          ┌───────────────┼───────────────┐        │
                          ▼               ▼               ▼        │
                   ┌────────────┐ ┌────────────┐ ┌─────────────┐  │
                   │TS.Zod      │ │TS.TanStack │ │TS.Angular   │  │
                   │(v3/v4)     │ │            │ │             │  │
                   └────────────┘ └────────────┘ └─────────────┘  │
                                                                    │
        все генераторы зависят только от ValIO.Core ───────────────┘
```

### Правило зависимостей

- `ValIO.Core` — не зависит ни от чего (только .NET BCL, Microsoft.OpenApi)
- Все `ValIO.Generators.*` — зависят только от `ValIO.Core`
- `ValIO.Configuration` — зависит от `ValIO.Core` + каждого плагина (**через интерфейс IGeneratorOptionsDescriptor**)
- `ValIO` (CLI) — зависит от всех (точка сборки)

---

## 6. Roadmap этапов

### Этап 0: Подготовка (фундамент)

**Приоритет: Критический**  
**Блокирует:** все последующие этапы

- [ ] Удаление `AstNodeKind` → чистый double-dispatch Visitor  
  → Документ: `01_AST_REFACTORING.md`
- [ ] Удаление `ValidationLibrary` enum из Core → строковые идентификаторы  
- [ ] Удаление `QueryWrapperLibrary` enum из Core → строковые идентификаторы  
- [ ] Введение `IGeneratorPlugin` контракта  
  → Документ: `02_PLUGIN_ARCHITECTURE.md`
- [ ] Рефакторинг `ConfigurationLoader` — самостоятельная регистрация опций  
- [ ] Рефакторинг `CodeGeneratorFactory` → `GeneratorRegistry`

### Этап 1: TypeScript расширение (HTTP клиенты)

**Приоритет: Высокий**  
**Зависит от:** Этап 0

- [ ] `TypeScriptCodeGenerator` → декомпозиция на pipeline Steps  
- [ ] Новые HTTP транспорты: Ky, Undici, Ofetch, Got  
- [ ] Angular HttpClient (RxJS-based + Promise-based)  
- [ ] SolidStart  
- [ ] MSW (Mock Service Worker)  
  → Документ: `03_NEW_TS_CLIENTS.md`

### Этап 2: TypeScript расширение (Validation)

**Приоритет: Высокий**  
**Зависит от:** Этап 0

- [ ] Zod v3 → v4 миграция + поддержка обеих версий  
- [ ] Valibot  
- [ ] TypeBox  
  → Документ: `05_TS_VALIDATION.md`

### Этап 3: TanStack Query для всех фреймворков

**Приоритет: Средний**  
**Зависит от:** Этапы 1, 2

- [ ] TanStack Query for Vue  
- [ ] TanStack Query for Angular (+ NgRx вариант)  
- [ ] TanStack Query for Solid  
- [ ] TanStack Query for Svelte  
  → Документ: `03_NEW_TS_CLIENTS.md` (раздел TanStack)

### Этап 4: Новые языки

**Приоритет: Средний**  
**Зависит от:** Этап 0

- [ ] PHP: Guzzle + Laravel HTTP Client  
- [ ] Python: aiohttp, HTTPX (Sync), HTTPX (Async)  
- [ ] C#/F#: RestSharp  
- [ ] Java: Unirest, AsyncHttp, OkHttp  
- [ ] Kotlin: OkHttp  
- [ ] Swift: NSURLSession  
- [ ] Dart: package:http + Dio  
  → Документ: `04_NEW_LANGUAGES.md`

---

## 7. Матрица стеков и генераторов

### TypeScript

| Transport/Framework | Validation | Query Wrapper | MSW |
|---------------------|-----------|---------------|-----|
| **Fetch** (native) | - / Zod3 / Zod4 / Valibot / TypeBox | React / Vue / Angular / Solid / Svelte | ✓ |
| **Axios** | - / Zod3 / Zod4 / Valibot / TypeBox | React / Vue / Angular / Solid / Svelte | ✓ |
| **Ky** | - / Zod3 / Zod4 / Valibot / TypeBox | React / Vue / Angular / Solid / Svelte | ✓ |
| **Undici** | - / Zod3 / Zod4 / Valibot / TypeBox | React / Vue / Angular / Solid / Svelte | ✓ |
| **Ofetch** | - / Zod3 / Zod4 / Valibot / TypeBox | React / Vue / Angular / Solid / Svelte | ✓ |
| **Got** | - / Zod3 / Zod4 / Valibot / TypeBox | React / Vue / Angular / Solid / Svelte | ✓ |
| **Angular HttpClient** (RxJS) | - / Zod3 / Zod4 / Valibot / TypeBox | Angular TanStack / NgRx | ✓ |
| **Angular HttpClient** (Promise) | - / Zod3 / Zod4 / Valibot / TypeBox | Angular TanStack | ✓ |
| **SolidStart** | - / Zod3 / Zod4 / Valibot | Solid TanStack | ✓ |

### C# / F#

| Framework | Validation |
|-----------|-----------|
| HttpClient (raw) | DataAnnotations |
| RestSharp | DataAnnotations |

### PHP

| Framework |
|-----------|
| Guzzle |
| Laravel HTTP Client |

### Python

| Framework | Sync/Async |
|-----------|-----------|
| Requests | Sync |
| HTTPX | Sync |
| HTTPX | Async |
| aiohttp | Async |

### Java

| Framework |
|-----------|
| Unirest |
| AsyncHttp |
| OkHttp |

### Kotlin

| Framework |
|-----------|
| OkHttp |

### Swift

| Framework |
|-----------|
| NSURLSession |

### Dart

| Framework |
|-----------|
| package:http |
| Dio |

---

## Ключевые паттерны, применяемые в рефакторинге

| Паттерн | Применение |
|---------|-----------|
| **Visitor** (GoF) | Чистый double-dispatch в AST, без switch на Kind |
| **Chain of Responsibility** (GoF) | Pipeline шаги генерации кода |
| **Strategy** (GoF) | Взаимозаменяемые рендереры, парсеры, маперы |
| **Registry / Service Locator** | `IGeneratorRegistry` — регистрация и резолюция генераторов |
| **Composite** (GoF) | AST дерево нод |
| **Builder** (GoF) | `IrBuilder`, `[Lang]AstBuilder` |
| **Factory Method** (GoF) | Создание pipeline шагов |
| **Template Method** (GoF) | `BaseCodeGenerator`, `BaseAstVisitor` |
| **Decorator** (GoF) | Расширение рендереров без изменения базового класса |
| **Prototype** (GoF) | `AstNode.Clone()` |
| **Mediator** | `ICodeGenerationPipeline` координирует шаги |

---

---

## Карта документов рефакторинга

| Документ | Содержание |
|----------|-----------|
| `01_AST_REFACTORING.md` | Удаление `AstNodeKind`, исправление broken Accept в базовых нодах, ZodAstVisitorBase/TypeScriptAstVisitorBase, цепочка рефакторинга по шагам |
| `02_PLUGIN_ARCHITECTURE.md` | Plugin-архитектура, `IGeneratorPlugin`, `ICodeGenerationStep`, `IIrTypeMapper`, `ConfigurationLoader`, детальные алгоритмы всех TS шагов |
| `03_NEW_TS_CLIENTS.md` | Новые HTTP клиенты: undici, ofetch, got, ky, Angular HttpClient, SolidStart, MSW, TanStack Query (Vue/Angular/Solid/Svelte) |
| `04_NEW_LANGUAGES.md` | Новые языки: PHP (Guzzle, Laravel), Python (aiohttp, HTTPX), C# RestSharp, F#, Java (OkHttp, UniRest), Kotlin, Swift, Dart |
| `05_TS_VALIDATION.md` | Новые валидаторы: Valibot, TypeBox, Zod v3, Zod v4 — схемы, типы, интеграция |
| `06_LEGACY_CLEANUP.md` | Полный каталог legacy-кода под удаление: TemplateData (30+ файлов Core), TypeScript-специфичный legacy (23 файла), ServiceCollectionExtensions |
| `07_IR_NODES_AND_BUILDERS.md` | IR иерархия, `IrBuilder` алгоритм, IR→AST конвертация, `IrTypeDependencyGraph`, маппинг для TS/C#/PHP/Python |
| `08_AST_BUILDERS_DEEP.md` | Детальный разбор всех AstBuilder классов, `TypeScriptAstBuilder` и `CSharpAstBuilder` декомпозиция, алгоритмы построения моделей/клиентов/схем |
| `09_DESIGN_PATTERNS.md` | Применение GoF паттернов: Visitor, CoR, Strategy, Builder, Template Method, Command, Composite, Prototype, Iterator, Facade, State, Proxy |

*Документы связаны между собой. Рекомендуемый порядок прочтения: 01 → 02 → 07 → 08 → 06 → 03 → 04 → 05 → 09.*
