# ValIO — Мастер-индекс задач рефакторинга

> **Обратная совместимость не нужна.** Ломаем и переписываем полностью.  
> Текущий рабочий код: IR/AST пайплайн. Всё остальное — legacy под удаление.

---

## Быстрая навигация по документации

| Документ | Читать для |
|----------|-----------|
| `docs/refactoring/01_AST_REFACTORING.md` | Visitor, AstNodeKind, broken Accept, ZodAstVisitorBase |
| `docs/refactoring/02_PLUGIN_ARCHITECTURE.md` | IGeneratorPlugin, Steps, IIrTypeMapper, ConfigurationLoader |
| `docs/refactoring/06_LEGACY_CLEANUP.md` | Что удалять и почему (полный каталог) |
| `docs/refactoring/07_IR_NODES_AND_BUILDERS.md` | IR иерархия, IrBuilder, IR→AST маппинг |
| `docs/refactoring/08_AST_BUILDERS_DEEP.md` | TypeScriptAstBuilder, CSharpAstBuilder декомпозиция |
| `docs/refactoring/09_DESIGN_PATTERNS.md` | Какие паттерны использовать и как |
| `docs/refactoring/03_NEW_TS_CLIENTS.md` | Новые HTTP транспорты TypeScript |
| `docs/refactoring/04_NEW_LANGUAGES.md` | PHP, Python, Java, Kotlin, Swift, Dart |
| `docs/refactoring/05_TS_VALIDATION.md` | Valibot, TypeBox, Zod v3/v4 |

---

## Сводная таблица задач

| ID | Задача | Фаза | Блокирует | Приоритет | Статус |
|----|--------|------|-----------|-----------|--------|
| **ФАЗА 0 — ФУНДАМЕНТ** | | | | | |
| [0.1](#01--исправить-accept-в-базовых-ast-нодах) | Исправить Accept в базовых AST нодах | 0 | 0.2, 0.3 | 🔴 КРИТИЧНО | ☐ |
| [0.2](#02--исправить-dispatch-в-ts-visitorах) | Исправить смешанный dispatch в TS Visitor'ах | 0 | 0.3 | 🔴 КРИТИЧНО | ☐ |
| [0.3](#03--удалить-astnodekind) | Удалить AstNodeKind | 0 | 0.4 | 🔴 КРИТИЧНО | ☐ |
| [0.4](#04--удалить-iastrenderer-icoderer-httpastvisitorbase) | Удалить IAstRenderer, ICodeRenderer, HttpAstVisitorBase | 0 | — | 🔴 КРИТИЧНО | ☐ |
| [0.5](#05--создать-plugin-инфраструктуру-core) | Создать Plugin-инфраструктуру (Core) | 0 | 0.9, 0.10, 1.x | 🔴 КРИТИЧНО | ☐ |
| [0.6](#06--создать-iiirtypemapper) | Создать IIrTypeMapper + реализации | 0 | 1.x, 4.x | 🔴 КРИТИЧНО | ☐ |
| [0.7](#07--удалить-всё-templatedata) | Удалить всё TemplateData (Core + TypeScript) | 0 | 0.8 | 🔴 КРИТИЧНО | ☐ |
| [0.8](#08--удалить-legacy-интерфейсы-и-сервисы-core) | Удалить legacy интерфейсы и сервисы Core | 0 | 0.9 | 🔴 КРИТИЧНО | ☐ |
| [0.9](#09--удалить-legacy-enum-ы) | Удалить GenerationLanguage / ValidationLibrary / QueryWrapperLibrary | 0 | — | 🔴 КРИТИЧНО | ☐ |
| [0.10](#010--переписать-configurationloader) | Переписать ConfigurationLoader | 0 | — | 🔴 КРИТИЧНО | ☐ |
| [0.11](#011--удалить-фабрики-и-god-objects) | Удалить CodeGeneratorFactory, TypeMapperFactory, BaseCodeGenerator | 0 | — | 🔴 КРИТИЧНО | ☐ |
| [0.12](#012--удалить-typescript-specific-legacy) | Удалить TypeScript-специфичный legacy (23 файла) | 0 | — | 🔴 КРИТИЧНО | ☐ |
| [0.13](#013--создать-servicecollectionextensions-новый) | Переписать ServiceCollectionExtensions | 0 | 0.5, 0.12 | 🔴 КРИТИЧНО | ☐ |
| [0.14](#014--удалить-irtype-kind-switch-в-bilderах) | Удалить IrTypeKind switch → pattern matching | 0 | — | 🟠 ВЫСОКИЙ | ☐ |
| [0.15](#015--refactor-astnode-toString) | Обновить ToString() в AstNode / DeclarationNode / TypeNode | 0 | 0.3 | 🟡 СРЕДНИЙ | ☐ |
| **ФАЗА 1 — TYPESCRIPT STEPS** | | | | | |
| [1.1](#11--создать-typescriptmodelsstep) | Создать TypeScriptModelsStep | 1 | 0.5, 0.6 | 🔴 КРИТИЧНО | ☐ |
| [1.2](#12--создать-typescriptenumsstep) | Создать TypeScriptEnumsStep | 1 | 0.5, 0.6 | 🔴 КРИТИЧНО | ☐ |
| [1.3](#13--создать-fetchclientstep) | Создать FetchClientStep | 1 | 0.5, 0.6 | 🔴 КРИТИЧНО | ☐ |
| [1.4](#14--создать-axiosclientstep) | Создать AxiosClientStep + рефакторинг AxiosAstBuilder | 1 | 0.5, 0.6 | 🔴 КРИТИЧНО | ☐ |
| [1.5](#15--декомпозировать-typescriptcodegenerator) | Декомпозировать TypeScriptCodeGenerator → Plugin | 1 | 1.1–1.4 | 🔴 КРИТИЧНО | ☐ |
| [1.6](#16--создать-typescriptindexstep) | Создать TypeScriptIndexStep (index.ts re-exports) | 1 | 1.1–1.4 | 🟡 СРЕДНИЙ | ☐ |
| [1.7](#17--расширяемый-рендерер-itypescriptnoderenderer) | Создать ITypeScriptNodeRenderer Chain of Responsibility | 1 | 0.1–0.4 | 🟠 ВЫСОКИЙ | ☐ |
| [1.8](#18--декомпозировать-csharpcodeGenerator) | Декомпозировать CSharpCodeGenerator → Steps | 1 | 0.5, 0.6 | 🟠 ВЫСОКИЙ | ☐ |
| **ФАЗА 2 — TYPESCRIPT VALIDATION** | | | | | |
| [2.1](#21--создать-zod3schemasstep) | Создать Zod3SchemasStep | 2 | 1.1 | 🔴 КРИТИЧНО | ☐ |
| [2.2](#22--создать-zod4schemasStep) | Создать Zod4SchemasStep | 2 | 1.1 | 🔴 КРИТИЧНО | ☐ |
| [2.3](#23--создать-valibotschemastep) | Создать ValibotSchemasStep + ValibotAstBuilder | 2 | 1.1 | 🟠 ВЫСОКИЙ | ☐ |
| [2.4](#24--создать-typeboxschemastep) | Создать TypeBoxSchemasStep + TypeBoxAstBuilder | 2 | 1.1 | 🟠 ВЫСОКИЙ | ☐ |
| **ФАЗА 3 — НОВЫЕ TS ТРАНСПОРТЫ** | | | | | |
| [3.1](#31--kyclientstep) | KyClientStep + KyAstBuilder | 3 | 1.3 | 🟠 ВЫСОКИЙ | ☐ |
| [3.2](#32--undicistep) | UndiciClientStep + UndiciAstBuilder | 3 | 1.3 | 🟡 СРЕДНИЙ | ☐ |
| [3.3](#33--ofetchstep) | OfetchClientStep + OfetchAstBuilder | 3 | 1.3 | 🟡 СРЕДНИЙ | ☐ |
| [3.4](#34--gotstep) | GotClientStep + GotAstBuilder | 3 | 1.3 | 🟡 СРЕДНИЙ | ☐ |
| [3.5](#35--angular-httpclient) | AngularRxjsClientStep + AngularPromiseClientStep | 3 | 1.3 | 🟠 ВЫСОКИЙ | ☐ |
| [3.6](#36--solidstart) | SolidStartClientStep | 3 | 1.3 | 🟡 СРЕДНИЙ | ☐ |
| [3.7](#37--msw-mock-service-worker) | MswStep + MswAstBuilder | 3 | 1.1 | 🟡 СРЕДНИЙ | ☐ |
| **ФАЗА 3b — TANSTACK ДЛЯ ФРЕЙМВОРКОВ** | | | | | |
| [3b.1](#3b1--tanstack-vue) | TanStackVueStep | 3 | 1.4, 3.1 | 🟠 ВЫСОКИЙ | ☐ |
| [3b.2](#3b2--tanstack-angular) | TanStackAngularStep | 3 | 1.4, 3.5 | 🟠 ВЫСОКИЙ | ☐ |
| [3b.3](#3b3--tanstack-solid) | TanStackSolidStep | 3 | 1.4, 3.6 | 🟡 СРЕДНИЙ | ☐ |
| [3b.4](#3b4--tanstack-svelte) | TanStackSvelteStep | 3 | 1.4 | 🟡 СРЕДНИЙ | ☐ |
| **ФАЗА 4 — НОВЫЕ ЯЗЫКИ** | | | | | |
| [4.1](#41--php-guzzle) | PHP плагин (Guzzle) | 4 | 0.5, 0.6 | 🟠 ВЫСОКИЙ | ☐ |
| [4.2](#42--php-laravel-http) | PHP плагин (Laravel HTTP) | 4 | 4.1 | 🟡 СРЕДНИЙ | ☐ |
| [4.3](#43--python-httpx) | Python плагин (HTTPX/aiohttp) | 4 | 0.5, 0.6 | 🟠 ВЫСОКИЙ | ☐ |
| [4.4](#44--java-okhttp) | Java плагин (OkHttp) | 4 | 0.5, 0.6 | 🟡 СРЕДНИЙ | ☐ |
| [4.5](#45--kotlin-okhttp) | Kotlin плагин (OkHttp/Coroutines) | 4 | 0.5, 0.6 | 🟡 СРЕДНИЙ | ☐ |
| [4.6](#46--swift-nsurlsession) | Swift плагин (URLSession) | 4 | 0.5, 0.6 | 🟡 СРЕДНИЙ | ☐ |
| [4.7](#47--dart-dio) | Dart плагин (Dio/http) | 4 | 0.5, 0.6 | 🟡 СРЕДНИЙ | ☐ |
| [4.8](#48--f-restsharp) | F# плагин (RestSharp) | 4 | 0.5, 0.6 | 🟢 НИЗКИЙ | ☐ |
| **ФАЗА 5 — ИНФРАСТРУКТУРА** | | | | | |
| [5.1](#51--generationorchestrator-facade) | Создать GenerationOrchestrator (Facade) | 5 | 0.5 | 🟠 ВЫСОКИЙ | ☐ |
| [5.2](#52--irtypedependencygraph-builder) | Выделить IrTypeDependencyGraphBuilder | 5 | — | 🟡 СРЕДНИЙ | ☐ |
| [5.3](#53--cachingschemaresolver-proxy) | Создать CachingSchemaResolver (Proxy) | 5 | — | 🟡 СРЕДНИЙ | ☐ |
| [5.4](#54--sourcefilebuilder) | Создать SourceFileBuilder утилиту | 5 | 1.x | 🟡 СРЕДНИЙ | ☐ |
| [5.5](#55--тесты) | Написать тесты для IR, AstBuilder, Steps | 5 | 1.x | 🟠 ВЫСОКИЙ | ☐ |

---

## Фаза 0 — Фундамент

> Все задачи фазы 0 должны быть завершены **до** начала фаз 1–4.  
> Они не добавляют функциональности — только устраняют фундаментальные проблемы архитектуры.

---

### 0.1 — Исправить Accept в базовых AST нодах

**📖 Документация:** `docs/refactoring/01_AST_REFACTORING.md` → раздел 2 "Корень проблемы"  
**🎯 Суть:** Четыре абстрактных базовых класса имеют конкретную реализацию `Accept`, которая вызывает dispatch-метод вместо конкретного Visit. Это ломает Visitor Pattern.

**Файлы для изменения:**

| Файл | Действие | Что сделать |
|------|----------|-------------|
| `ValIO.Core/Ast/Nodes/TypeNode.cs` | ✏️ Изменить | Удалить `override Accept` → `visitor.VisitType(this)` |
| `ValIO.Core/Ast/Nodes/StatementNode.cs` | ✏️ Изменить | Удалить `override Accept` → `visitor.VisitStatement(this)` |
| `ValIO.Core/Ast/Nodes/ExpressionNode.cs` | ✏️ Изменить | Удалить `override Accept` → `visitor.VisitExpression(this)` |
| `ValIO.Core/Ast/Nodes/DeclarationNode.cs` | ✏️ Изменить | Удалить `override Accept` → `visitor.VisitDeclaration(this)` |

**После удаления** — запустить компиляцию. Все файлы с ошибкой "не реализует AstNode.Accept" — добавить в них:
```csharp
public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    => visitor.VisitXxx(this);   // конкретный метод для этой ноды
```

**Файлы с конкретными нодами, которым добавить Accept:**
- Все файлы в `ValIO.Core/Ast/Nodes/Types/`
- Все файлы в `ValIO.Core/Ast/Nodes/Expressions/`
- Все файлы в `ValIO.Core/Ast/Nodes/Statements/`
- Все файлы в `ValIO.Generators.CSharp/Ast/Nodes/`
- Все файлы в `ValIO.Generators.TypeScript/Ast/Nodes/`

---

### 0.2 — Исправить смешанный dispatch в TS Visitor'ах

**📖 Документация:** `docs/refactoring/01_AST_REFACTORING.md` → раздел 3 "Вторичная проблема"  
**🎯 Суть:** `ZodAstVisitorBase` и `TypeScriptAstVisitorBase` используют `node.Kind` switch — должны использовать Accept.

**Файлы для изменения:**

| Файл | Действие | Что сделать |
|------|----------|-------------|
| `ValIO.Generators.TypeScript/Ast/Visitors/TypeScriptAstVisitorBase.cs` | ✏️ | Удалить `override VisitExpression()` (смешанный if+switch) |
| `ValIO.Generators.TypeScript/Zod/Ast/Visitors/ZodAstVisitorBase.cs` | ✏️ | Удалить `override Visit()` и `override VisitExpression()` |
| `ValIO.Generators.TypeScript/Ast/Nodes/Expressions/ObjectLiteralExpressionNode.cs` | ✏️ | Добавить Accept → `ITypeScriptAstVisitor.VisitObjectLiteralExpression(this)` |
| `ValIO.Generators.TypeScript/Ast/Nodes/Expressions/TemplateLiteralExpressionNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodPropertyNode.cs` | ✏️ | Добавить Accept → `IZodAstVisitor.VisitZodProperty(this)` |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodSchemaNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodObjectNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodPrimitiveSchemaNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodArrayNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodUnionNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodIntersectionNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodEnumNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodEffectsNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodExpressionSchemaNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Nodes/ZodEnumValueNode.cs` | ✏️ | Добавить Accept |
| `ValIO.Generators.TypeScript/Zod/Ast/Visitors/IZodAstVisitor.cs` | ✏️ | Добавить объявления всех Visit методов для Zod нод |

**Также проверить TanStack и Axios ноды** — добавить Accept где отсутствует:
- `TanStackQuery/Ast/Nodes/` — все 5 файлов
- `Axios/Ast/Nodes/` — все 2 файла

---

### 0.3 — Удалить AstNodeKind

**📖 Документация:** `docs/refactoring/01_AST_REFACTORING.md` → разделы 1, 5, 7  
**⚠️ Зависимость:** Выполнять строго ПОСЛЕ 0.1 и 0.2  
**🎯 Суть:** Удалить 130+ значений enum, который используется только для эмуляции dispatch.

**Файлы для удаления:**

| Файл | Действие |
|------|----------|
| `ValIO.Core/Ast/Enums/AstNodeKind.cs` | 🗑️ Удалить файл |

**Файлы для изменения (убрать `Kind`):**

| Файл | Что убрать |
|------|-----------|
| `ValIO.Core/Ast/Nodes/AstNode.cs` | `public abstract AstNodeKind Kind { get; }` |
| Все конкретные ноды (`*Node.cs`) | `public override AstNodeKind Kind => AstNodeKind.XXX;` |
| `ValIO.Core/Ast/Visitors/BaseAstVisitor.cs` | Методы `VisitType`, `VisitStatement`, `VisitExpression`, `VisitDeclaration` со switch |
| `ValIO.Core/Ast/Visitors/IAstVisitor.cs` | Убрать `VisitDeclaration(DeclarationNode)`, `VisitType(TypeNode)`, `VisitExpression(ExpressionNode)`, `VisitStatement(StatementNode)` |

**Сделать все Visit методы в BaseAstVisitor `virtual` с DefaultVisit:**
```csharp
public virtual TResult VisitSimpleType(SimpleTypeNode node) => DefaultVisit(node);
// ... для всех методов
```

---

### 0.4 — Удалить IAstRenderer, ICodeRenderer, HttpAstVisitorBase

**📖 Документация:** `docs/refactoring/01_AST_REFACTORING.md` → раздел 10  
**📖 Документация:** `docs/refactoring/06_LEGACY_CLEANUP.md` → раздел 7

**Файлы для удаления:**

| Файл | Причина |
|------|---------|
| `ValIO.Core/Ast/Visitors/IAstRenderer.cs` | Пустой маркерный интерфейс без смысла |
| `ValIO.Core/Interfaces/ICodeRenderer.cs` | TemplateData legacy |
| `ValIO.Core/Ast/Visitors/HttpAstVisitorBase.cs` | Не нужен после исправления IAstVisitor |

**Файлы для изменения:**
- Убрать `IAstRenderer` из всех `implements` деклараций (компилятор укажет)
- Убрать HTTP методы из `IAstVisitor.cs` если они дублируют `IHttpAstVisitor.cs`

---

### 0.5 — Создать Plugin-инфраструктуру (Core)

**📖 Документация:** `docs/refactoring/02_PLUGIN_ARCHITECTURE.md` → разделы 3–6, 11  
**📖 Паттерны:** `docs/refactoring/09_DESIGN_PATTERNS.md` → Strategy, Command, Facade

**Файлы для создания:**

| Файл | Что создать |
|------|-------------|
| `ValIO.Core/Plugins/IGeneratorPlugin.cs` | Контракт плагина (LanguageId, DisplayName, LanguageAliases, CreatePipeline, ConfigureServices) |
| `ValIO.Core/Plugins/IGeneratorOptionsDescriptor.cs` | Самостоятельный парсинг опций (ParseOptions, ValidateOptions, GetOptionDescriptions) |
| `ValIO.Core/Plugins/ICodeGenerationPipeline.cs` | Контракт pipeline (Steps, ExecuteAsync) |
| `ValIO.Core/Plugins/ICodeGenerationStep.cs` | Контракт шага (Name, Order, ShouldExecute, ExecuteAsync) |
| `ValIO.Core/Plugins/CodeGenerationPipeline.cs` | Реализация: сортирует шаги по Order, выполняет, агрегирует файлы |
| `ValIO.Core/Plugins/IGeneratorRegistry.cs` | Контракт реестра (FindPlugin, AllPlugins) |
| `ValIO.Core/Plugins/GeneratorRegistry.cs` | Реализация через IEnumerable<IGeneratorPlugin> из DI |
| `ValIO.Core/Plugins/GenerationContext.cs` | Контекст (IrDocument, Configuration, PluginOptions, GeneratedFiles, StepData) |
| `ValIO.Core/Plugins/ITransportDescriptor.cs` | Описание транспорта |
| `ValIO.Core/Plugins/OptionDescription.cs` | Описание опции для --help |

**Файлы для изменения:**

| Файл | Что изменить |
|------|--------------|
| `ValIO.Core/Models/GenerationConfiguration.cs` | Убрать `Language` (enum), добавить `LanguageId` (string) + `PluginOptions` (object?) |

---

### 0.6 — Создать IIrTypeMapper

**📖 Документация:** `docs/refactoring/02_PLUGIN_ARCHITECTURE.md` → раздел 14  
**📖 Документация:** `docs/refactoring/07_IR_NODES_AND_BUILDERS.md` → маппинг типов  
**📖 Паттерны:** `docs/refactoring/09_DESIGN_PATTERNS.md` → Strategy

**Файлы для создания:**

| Файл | Что создать |
|------|-------------|
| `ValIO.Core/Interfaces/IIrTypeMapper.cs` | `MapType(IrType) → TypeNode`, `IsOptionalInLanguage(IrProperty) → bool`, `GetImportTarget(IrType) → string?` |
| `ValIO.Generators.TypeScript/TypeScriptIrTypeMapper.cs` | Pattern matching на IrType: 14 примитивов + 8 составных типов. НЕ использовать `irType.Kind` switch — только `irType is XxxType` |
| `ValIO.Generators.CSharp/CSharpIrTypeMapper.cs` | Аналогично для C#: int/double/bool/DateTimeOffset/Guid/List/Dictionary |

**Файлы для удаления** (после создания IIrTypeMapper):

| Файл | Причина |
|------|---------|
| `ValIO.Generators.TypeScript/TypeScriptTypeMapper.cs` | Работает с OpenAPI напрямую, не с IR |
| `ValIO.Generators.CSharp/CSharpTypeMapper.cs` | Аналогично |

---

### 0.7 — Удалить всё TemplateData

**📖 Документация:** `docs/refactoring/06_LEGACY_CLEANUP.md` → разделы 2, 12.2

**Файлы для удаления (ValIO.Core):**

| Файл |
|------|
| `ValIO.Core/Models/TemplateData/ModelTemplateData.cs` |
| `ValIO.Core/Models/TemplateData/OperationTemplateData.cs` |
| `ValIO.Core/Models/TemplateData/ClientTemplateData.cs` |
| `ValIO.Core/Models/TemplateData/EnumTemplateData.cs` |
| `ValIO.Core/Models/TemplateData/Options/ClientTemplateDataOptions.cs` |
| `ValIO.Core/Models/TemplateData/Options/EnumTemplateDataOptions.cs` |
| `ValIO.Core/Models/TemplateData/Options/ITemplateDataOptions.cs` |
| `ValIO.Core/Models/TemplateData/Options/ModelTemplateDataOptions.cs` |
| `ValIO.Core/Models/TemplateData/Options/OperationTemplateDataOptions.cs` |
| `ValIO.Core/Models/TemplateData/Options/PropertyTemplateDataOptions.cs` |

**Файлы для удаления (ValIO.Generators.TypeScript):**

| Файл |
|------|
| `ValIO.Generators.TypeScript/Models/ApiTemplateData.cs` |
| `ValIO.Generators.TypeScript/Models/InterfaceTemplateData.cs` |
| `ValIO.Generators.TypeScript/Models/TypeScriptClientTemplateDataOptions.cs` |
| `ValIO.Generators.TypeScript/Models/TypeScriptModelTemplateDataOptions.cs` |
| `ValIO.Generators.TypeScript/Models/TypeScriptEnumTemplateDataOptions.cs` |
| `ValIO.Generators.TypeScript/Models/TypeScriptOperationTemplateDataOptions.cs` |
| `ValIO.Generators.TypeScript/Models/TypeScriptPropertyTemplateDataOptions.cs` |
| `ValIO.Generators.TypeScript/Models/CodeGeneration/CodeBlock.cs` |
| `ValIO.Generators.TypeScript/Models/CodeGeneration/FunctionParameter.cs` |
| `ValIO.Generators.TypeScript/Models/CodeGeneration/FunctionParameterBuilder.cs` |
| `ValIO.Generators.TypeScript/Models/CodeGeneration/FunctionSignature.cs` |
| `ValIO.Generators.TypeScript/Models/CodeGeneration/OptionsDefinition.cs` |
| `ValIO.Generators.TypeScript/Models/CodeGeneration/OptionsDefinitionBuilder.cs` |
| `ValIO.Generators.TypeScript/Models/CodeGeneration/TypeDefinition.cs` |
| `ValIO.Generators.TypeScript/Models/CodeGeneration/TypeDefinitionBuilder.cs` |

**Файлы для удаления (ValIO.Generators.CSharp):**

| Файл |
|------|
| `ValIO.Generators.CSharp/Models/CSharpClientTemplateDataOptions.cs` |
| `ValIO.Generators.CSharp/Models/CSharpEnumTemplateDataOptions.cs` |
| `ValIO.Generators.CSharp/Models/CSharpModelTemplateDataOptions.cs` |
| `ValIO.Generators.CSharp/Models/CSharpOperationTemplateDataOptions.cs` |
| `ValIO.Generators.CSharp/Models/CSharpPropertyTemplateDataOptions.cs` |
| `ValIO.Generators.CSharp/Models/ExceptionTemplateData.cs` |

---

### 0.8 — Удалить legacy интерфейсы и сервисы Core

**📖 Документация:** `docs/refactoring/06_LEGACY_CLEANUP.md` → разделы 3, 4

**Файлы для удаления (ValIO.Core/Interfaces):**

| Файл | Заменяется |
|------|-----------|
| `IClientRenderer.cs` | TemplateData legacy |
| `IModelRenderer.cs` | TemplateData legacy |
| `IEnumRenderer.cs` | TemplateData legacy |
| `IModelFormatRenderer.cs` | TemplateData + GenerationLanguage |
| `IEnumFormatRenderer.cs` | TemplateData + GenerationLanguage |
| `IOperationRenderer.cs` | TemplateData legacy |
| `IQueryWrapperRenderer.cs` | TemplateData + QueryWrapperLibrary |
| `IDependencyAnalyzer.cs` | → `IrTypeDependencyGraph` |
| `IEnumExtractor.cs` | → `IrBuilder.ExtractEnums()` |
| `IModelExtractor.cs` | → `IrBuilder.ExtractModels()` |
| `IOperationExtractor.cs` | → `IrBuilder.ExtractClients()` |
| `ITypeInfoBuilder.cs` | → `IrBuilder.BuildType()` |
| `IClientTemplateData.cs` | TemplateData legacy |
| `IModelTemplateData.cs` | TemplateData legacy |
| `IEnumTemplateData.cs` | TemplateData legacy |
| `IOperationTemplateData.cs` | TemplateData legacy |
| `ICodeGenerator.cs` | → `ICodeGenerationPipeline` |
| `ITypeMapper.cs` | → `IIrTypeMapper` |
| `IOperationNamingStrategy.cs` | → `IrBuilder` + `INamingConverter` |

**Файлы для удаления (ValIO.Core/Services):**

| Файл | Заменяется |
|------|-----------|
| `BaseDependencyAnalyzer.cs` | → `IrTypeDependencyGraph` |
| `BaseEnumExtractor.cs` | → `IrBuilder` |
| `BaseTypeInfoBuilder.cs` | → `IrBuilder` + `IrType` |
| `BaseTypeMapper.cs` | → `IIrTypeMapper` |
| `BaseCodeGenerator.cs` | → `BaseCodeGenerationPipeline` |
| `DependencyCollector.cs` | TypeInfo legacy |
| `ModelExtractor.cs` | → `IrBuilder` |
| `OperationExtractor.cs` | → `IrBuilder` |

**Файлы для удаления (ValIO.Core/Models):**

| Файл | Заменяется |
|------|-----------|
| `TypeInfo.cs` | → `IrType` иерархия |
| `GenerationOptions.cs` | → plugin-специфичные опции в `PluginOptions` |
| `ApiParameter.cs` | → `IrParameter` |
| `ApiProperty.cs` | → `IrProperty` |
| `ApiRequestBody.cs` | → `IrRequestBody` |
| `ApiResponse.cs` | → `IrResponse` |
| `SchemaCompositionInfo.cs` | → `IrComposition` |

---

### 0.9 — Удалить legacy enum-ы

**📖 Документация:** `docs/refactoring/06_LEGACY_CLEANUP.md` → раздел 9

**Файлы для удаления:**

| Файл | Заменяется |
|------|-----------|
| `ValIO.Core/Models/GenerationLanguage.cs` | → строка `LanguageId` в `IGeneratorPlugin` |
| `ValIO.Core/Models/ValidationLibrary.cs` | → строка pluginId ("zod3", "valibot", "typebox") |
| `ValIO.Core/Models/QueryWrapperLibrary.cs` | → строка pluginId ("tanstack-react", "swr") |
| `ValIO.Generators.TypeScript/Models/TypeScriptHttpTransports.cs` | → список строк транспортов в `TypeScriptGenerationOptions.Transports` |

**Также удалить из конфигурационных моделей:**

| Файл | Что удалить |
|------|-------------|
| `ValIO.Configuration/Models/ProjectConfig.cs` | Типизированные `Options` поля → заменить на `Dictionary<string, JsonElement>? Options` |

---

### 0.10 — Переписать ConfigurationLoader

**📖 Документация:** `docs/refactoring/02_PLUGIN_ARCHITECTURE.md` → раздел 7  
**🎯 Исходный файл:** `ValIO.Configuration/Services/ConfigurationLoader.cs` (~500 строк)

**Что удалить из ConfigurationLoader:**
- `ParseGenerationLanguage()` → удалить (язык ищется через `IGeneratorRegistry.FindPlugin()`)
- `ParseValidationLibrary()` → удалить (и исправить опечатку "valli" → исчезает вместе с методом)
- `ParseQueryWrapperLibrary()` → удалить
- `ParseCSharpOptions()` → перенести в `CSharpOptionsDescriptor`
- `ParseTypeScriptOptions()` → перенести в `TypeScriptOptionsDescriptor`

**Что оставить в ConfigurationLoader:**
- `MapFileOrganization()`
- `MapNamingConventions()`
- Логику поиска плагина через `IGeneratorRegistry`
- Делегирование парсинга опций в `plugin.OptionsDescriptor.ParseOptions(rawOptions, out errors)`

**Файлы для создания:**

| Файл | Что создать |
|------|-------------|
| `ValIO.Generators.TypeScript/Options/TypeScriptOptionsDescriptor.cs` | Парсинг всех TS опций из `Dictionary<string, object?>` |
| `ValIO.Generators.CSharp/Options/CSharpOptionsDescriptor.cs` | Парсинг C# опций |
| `ValIO.Generators.Cpp/Options/CppOptionsDescriptor.cs` | Парсинг C++ опций |

**Файлы для удаления:**

| Файл |
|------|
| `ValIO.Configuration/Models/ConfigOptions.cs` (если существует) |

---

### 0.11 — Удалить фабрики и God Objects

**📖 Документация:** `docs/refactoring/06_LEGACY_CLEANUP.md` → раздел 6

**Файлы для удаления:**

| Файл | Заменяется |
|------|-----------|
| `ValIO/Factories/CodeGeneratorFactory.cs` | → `GeneratorRegistry.FindPlugin()` |
| `ValIO/Factories/TypeMapperFactory.cs` | → DI (каждый плагин регистрирует свой `IIrTypeMapper`) |

---

### 0.12 — Удалить TypeScript-специфичный legacy

**📖 Документация:** `docs/refactoring/06_LEGACY_CLEANUP.md` → раздел 12

**Файлы для удаления:**

| Файл |
|------|
| `ValIO.Generators.TypeScript/Renderers/InterfaceModelRenderer.cs` |
| `ValIO.Generators.TypeScript/Renderers/TypeScriptEnumRenderer.cs` |
| `ValIO.Generators.TypeScript/Renderers/TypeScriptUnionTypeRenderer.cs` |
| `ValIO.Generators.TypeScript/Services/TypeScriptDependencyAnalyzer.cs` |
| `ValIO.Generators.TypeScript/Services/TypeScriptEnumExtractor.cs` |
| `ValIO.Generators.TypeScript/Services/TypeScriptOperationNamingStrategy.cs` |
| `ValIO.Generators.TypeScript/Services/TypeScriptTypeInfoBuilder.cs` |
| `ValIO.Generators.TypeScript/TypeScriptTypeMapper.cs` |
| `ValIO.Generators.TypeScript.Zod/` | **Весь проект** — пустая оболочка, реальный Zod код в `ValIO.Generators.TypeScript/Zod/` |

**Также удалить из CSharp:**

| Файл |
|------|
| `ValIO.Generators.CSharp/CSharpTypeMapper.cs` |
| `ValIO.Generators.CSharp/Services/CSharpNamingConverter.cs` (если дублирует Core) |

---

### 0.13 — Переписать ServiceCollectionExtensions

**📖 Документация:** `docs/refactoring/02_PLUGIN_ARCHITECTURE.md` → раздел 15  
**🎯 Исходный файл:** `ValIO/ServiceRegistration/ServiceCollectionExtensions.cs`

**Что сделать:**
1. Удалить методы `AddTypeScriptGenerators()`, `AddCSharpGenerators()`, `AddCppGenerators()`
2. Удалить все регистрации legacy классов (TypeScriptTypeMapper, InterfaceModelRenderer, TypeScriptEnumRenderer и т.д.)
3. Удалить закомментированный блок (~50 строк старых TemplateData рендереров)
4. Создать метод `AddValIOCore()` — только ядро (Registry, IrBuilder, NamingConverter, FileWriter)
5. Создать extension метод `AddGeneratorPlugin<TPlugin>()` — плагин регистрирует себя сам

---

### 0.14 — Удалить IrTypeKind switch → pattern matching

**📖 Документация:** `docs/refactoring/07_IR_NODES_AND_BUILDERS.md` → IrTypeKind  
**🎯 Суть:** `IrBuilder.BuildType()` и `CSharpAstBuilder.BuildType(IrType)` используют `switch(irType.Kind)` вместо `switch(irType)` или `is`-check.

**Файлы для изменения:**

| Файл | Что изменить |
|------|--------------|
| `ValIO.Core/IR/Types/IrType.cs` | Удалить `public abstract IrTypeKind Kind { get; }` |
| `ValIO.Core/IR/Enums/IrTypeKind.cs` | Удалить файл |
| Все `IrXxxType.cs` в `ValIO.Core/IR/Types/` | Удалить `public override IrTypeKind Kind => ...` |
| `ValIO.Core/IR/Builders/IrBuilder.cs` | Заменить `switch(irType.Kind)` на `switch(irType)` pattern matching |
| `ValIO.Generators.CSharp/Ast/Builders/CSharpAstBuilder.cs` | Заменить `switch(irType.Kind)` на pattern matching |
| `ValIO.Generators.TypeScript/Ast/Builders/TypeScriptAstBuilder.cs` | Аналогично |

```csharp
// БЫЛО:
switch (irType.Kind)
{
    case IrTypeKind.Primitive: ...
    case IrTypeKind.Array:     ...
}

// СТАЛО:
switch (irType)
{
    case IrPrimitiveType p:  ...
    case IrArrayType arr:    ...
    case IrModelReference r: ...
}
```

---

### 0.15 — Обновить ToString() в AstNode / DeclarationNode / TypeNode

**📖 Документация:** `docs/refactoring/01_AST_REFACTORING.md` → раздел 11

**Файлы для изменения:**

| Файл | Изменение |
|------|-----------|
| `ValIO.Core/Ast/Nodes/AstNode.cs` | `ToString() → GetType().Name` вместо `Kind` |
| `ValIO.Core/Ast/Nodes/TypeNode.cs` | `ToString() → $"{GetType().Name}{nullableStr}{optionalStr}"` |
| `ValIO.Core/Ast/Nodes/DeclarationNode.cs` | `ToString() → $"{GetType().Name}({Name.Name})"` |

---

## Фаза 1 — TypeScript Steps

> После завершения Фазы 0 декомпозировать `TypeScriptCodeGenerator` (678 строк, God Object) на изолированные Steps.

---

### 1.1 — Создать TypeScriptModelsStep

**📖 Документация:** `docs/refactoring/02_PLUGIN_ARCHITECTURE.md` → раздел 13.1  
**📖 Документация:** `docs/refactoring/08_AST_BUILDERS_DEEP.md` → TypeScriptModelAstBuilder  
**📖 Паттерны:** Command, Builder, Iterator, Strategy (IIrTypeMapper)

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.TypeScript/Steps/TypeScriptModelsStep.cs` | `Order=10`, сортирует по `IrTypeDependencyGraph.TopologicalOrder`, вызывает `TypeScriptModelAstBuilder.BuildModelSourceFile(IrModel)` |
| `ValIO.Generators.TypeScript/Ast/Builders/TypeScriptModelAstBuilder.cs` | Выделить из `TypeScriptAstBuilder` — только логика моделей |

**Алгоритм BuildModelSourceFile** (читать `08_AST_BUILDERS_DEEP.md` раздел "TypeScriptModelAstBuilder"):
1. `SourceFileBuilder` → добавить импорты зависимостей через `IIrTypeMapper.GetImportTarget()`
2. `InterfaceDeclarationNode` или `TypeAliasDeclarationNode` (для union/intersection)
3. Для каждого `IrProperty` → `PropertySignatureNode` с `IIrTypeMapper.MapType(property.Type)`
4. Обработка `IrModel.Composition` (AllOf/OneOf/AnyOf)

---

### 1.2 — Создать TypeScriptEnumsStep

**📖 Документация:** `docs/refactoring/02_PLUGIN_ARCHITECTURE.md` → раздел 13.2  
**📖 Документация:** `docs/refactoring/08_AST_BUILDERS_DEEP.md` → TypeScriptEnumAstBuilder

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.TypeScript/Steps/TypeScriptEnumsStep.cs` | `Order=20`, итерирует `IrDocument.Enums` |
| `ValIO.Generators.TypeScript/Ast/Builders/TypeScriptEnumAstBuilder.cs` | Выделить из `TypeScriptAstBuilder` — логика enum/union/const object |

**Форматы enum** зависят от `TypeScriptGenerationOptions.EnumFormat`:
- `Enum` → `export enum Name { A = 'a', B = 'b' }`
- `Union` → `export type Name = 'a' | 'b'`
- `ConstObject` → `export const Name = { A: 'a', B: 'b' } as const`

---

### 1.3 — Создать FetchClientStep

**📖 Документация:** `docs/refactoring/03_NEW_TS_CLIENTS.md` → раздел Fetch  
**📖 Документация:** `docs/refactoring/08_AST_BUILDERS_DEEP.md` → алгоритм Fetch клиента

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.TypeScript/Steps/FetchClientStep.cs` | `Order=40`, `ShouldExecute` проверяет `opts.Transports.Contains("fetch")` |
| `ValIO.Generators.TypeScript/Fetch/Ast/Builders/FetchAstBuilder.cs` | Выделить Fetch логику из `TypeScriptAstBuilder` |

---

### 1.4 — Создать AxiosClientStep + рефакторинг AxiosAstBuilder

**📖 Документация:** `docs/refactoring/02_PLUGIN_ARCHITECTURE.md` → раздел 13.3  
**📖 Документация:** `docs/refactoring/08_AST_BUILDERS_DEEP.md` → AxiosClientAstBuilder

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.TypeScript/Steps/AxiosClientStep.cs` | `Order=40`, `ShouldExecute` проверяет `opts.Transports.Contains("axios")` |

**Файлы для изменения:**

| Файл | Что изменить |
|------|-------------|
| `ValIO.Generators.TypeScript/Axios/Ast/Builders/AxiosAstBuilder.cs` | Переписать на основе `IrClient` (убрать TypeScriptClientTemplateData вход) |

---

### 1.5 — Декомпозировать TypeScriptCodeGenerator → Plugin

**📖 Документация:** `docs/refactoring/06_LEGACY_CLEANUP.md` → раздел 10 (таблица Steps)  
**🎯 Исходный файл:** `ValIO.Generators.TypeScript/TypeScriptCodeGenerator.cs` (~678 строк)

**Что сделать:**
1. Создать `ValIO.Generators.TypeScript/TypeScriptGeneratorPlugin.cs` — реализует `IGeneratorPlugin`
2. `CreatePipeline()` — собирает Steps из DI в зависимости от опций
3. `ConfigureServices()` — регистрирует все TypeScript сервисы (шаги, билдеры, рендерер)
4. Создать `ValIO.Generators.TypeScript/Options/TypeScriptGenerationOptions.cs` — чистые опции без legacy

**Файлы для удаления после:**

| Файл |
|------|
| `ValIO.Generators.TypeScript/TypeScriptCodeGenerator.cs` |

---

### 1.6 — Создать TypeScriptIndexStep

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.TypeScript/Steps/TypeScriptIndexStep.cs` | `Order=70`, генерирует `index.ts` с re-export всех сгенерированных файлов |

---

### 1.7 — Расширяемый рендерер ITypeScriptNodeRenderer

**📖 Документация:** `docs/refactoring/01_AST_REFACTORING.md` → раздел 9  
**📖 Паттерны:** `docs/refactoring/09_DESIGN_PATTERNS.md` → Chain of Responsibility

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.TypeScript/Ast/Renderers/ITypeScriptNodeRenderer.cs` | `Priority`, `CanRender(AstNode)`, `Render(AstNode, TypeScriptAstRenderer)` |
| `ValIO.Generators.TypeScript/Zod/Ast/Renderers/ZodNodeRenderer.cs` | Рендеринг всех Zod нод как `ITypeScriptNodeRenderer` |
| `ValIO.Generators.TypeScript/Axios/Ast/Renderers/AxiosNodeRenderer.cs` | Рендеринг Axios нод |
| `ValIO.Generators.TypeScript/TanStackQuery/Ast/Renderers/TanStackNodeRenderer.cs` | Рендеринг TanStack нод |

**Файлы для изменения:**

| Файл | Что изменить |
|------|-------------|
| `ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs` | Добавить `ImmutableArray<ITypeScriptNodeRenderer> _chain`; в `DefaultVisit` — перебрать цепь |

---

### 1.8 — Декомпозировать CSharpCodeGenerator → Steps

**📖 Документация:** `docs/refactoring/08_AST_BUILDERS_DEEP.md` → CSharpAstBuilder  
**🎯 Исходный файл:** `ValIO.Generators.CSharp/CSharpCodeGenerator.cs`

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.CSharp/CSharpGeneratorPlugin.cs` | `IGeneratorPlugin` реализация |
| `ValIO.Generators.CSharp/Steps/CSharpModelsStep.cs` | Order=10 |
| `ValIO.Generators.CSharp/Steps/CSharpEnumsStep.cs` | Order=20 |
| `ValIO.Generators.CSharp/Steps/CSharpClientStep.cs` | Order=40 |
| `ValIO.Generators.CSharp/Options/CSharpOptionsDescriptor.cs` | Парсинг C# опций |

---

## Фаза 2 — TypeScript Validation

> Читать: `docs/refactoring/05_TS_VALIDATION.md`, `docs/refactoring/02_PLUGIN_ARCHITECTURE.md` раздел 13.4

---

### 2.1 — Создать Zod3SchemasStep

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.TypeScript/Steps/Zod3SchemasStep.cs` | Extends `ZodSchemasStepBase`, `ValidationLibraryId = "zod3"`, `Order=30` |
| `ValIO.Generators.TypeScript/Zod/Ast/Builders/ZodSchemasStepBase.cs` | Abstract: Template Method — скелет алгоритма |

**Существующие файлы для адаптации:**
- `ValIO.Generators.TypeScript/Zod/Ast/Builders/ZodAstBuilder.cs` → принимает `ZodVersion` параметр
- `ValIO.Generators.TypeScript/Zod/Services/ZodSchemaRenderer.cs` → проверить, мигрировать в Step

---

### 2.2 — Создать Zod4SchemasStep

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.TypeScript/Steps/Zod4SchemasStep.cs` | Extends `ZodSchemasStepBase`, `ValidationLibraryId = "zod4"` |

**Zod v4 отличия** (см. `05_TS_VALIDATION.md`):
- `import { z } from "zod/v4"` вместо `"zod"`
- `z.email()` вместо `z.string().email()`
- `z.url()` вместо `z.string().url()`
- `z.object().check()` для cross-field валидации

---

### 2.3 — Создать ValibotSchemasStep

**📖 Документация:** `docs/refactoring/05_TS_VALIDATION.md` → Valibot раздел

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.TypeScript/Steps/ValibotSchemasStep.cs` | `ValidationLibraryId = "valibot"`, `Order=30` |
| `ValIO.Generators.TypeScript/Valibot/Ast/Builders/ValibotAstBuilder.cs` | Строит Valibot схемы (v.object, v.string, v.pipe) |
| `ValIO.Generators.TypeScript/Valibot/Ast/Nodes/` | AST ноды для Valibot |
| `ValIO.Generators.TypeScript/Valibot/Ast/Renderers/ValibotNodeRenderer.cs` | `ITypeScriptNodeRenderer` |

**Синтаксис Valibot:**
```typescript
import * as v from 'valibot';
const UserSchema = v.object({ name: v.string(), age: v.number() });
type User = v.InferOutput<typeof UserSchema>;
```

---

### 2.4 — Создать TypeBoxSchemasStep

**📖 Документация:** `docs/refactoring/05_TS_VALIDATION.md` → TypeBox раздел

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Generators.TypeScript/Steps/TypeBoxSchemasStep.cs` | `ValidationLibraryId = "typebox"`, `Order=30` |
| `ValIO.Generators.TypeScript/TypeBox/Ast/Builders/TypeBoxAstBuilder.cs` | Строит TypeBox схемы (Type.Object, Type.String) |
| `ValIO.Generators.TypeScript/TypeBox/Ast/Renderers/TypeBoxNodeRenderer.cs` | `ITypeScriptNodeRenderer` |

---

## Фаза 3 — Новые TypeScript транспорты

> Читать: `docs/refactoring/03_NEW_TS_CLIENTS.md` для каждого транспорта  
> Каждый транспорт = новый Step + новый AstBuilder + новый NodeRenderer

---

### 3.1 — KyClientStep

**Файлы для создания:**

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/KyClientStep.cs` |
| `ValIO.Generators.TypeScript/Ky/Ast/Builders/KyAstBuilder.cs` |
| `ValIO.Generators.TypeScript/Ky/Ast/Nodes/KyCallNode.cs` |
| `ValIO.Generators.TypeScript/Ky/Ast/Renderers/KyNodeRenderer.cs` |

---

### 3.2 — UndiciStep

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/UndiciClientStep.cs` |
| `ValIO.Generators.TypeScript/Undici/Ast/Builders/UndiciAstBuilder.cs` |
| `ValIO.Generators.TypeScript/Undici/Ast/Renderers/UndiciNodeRenderer.cs` |

---

### 3.3 — OfetchStep

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/OfetchClientStep.cs` |
| `ValIO.Generators.TypeScript/Ofetch/Ast/Builders/OfetchAstBuilder.cs` |
| `ValIO.Generators.TypeScript/Ofetch/Ast/Renderers/OfetchNodeRenderer.cs` |

---

### 3.4 — GotStep

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/GotClientStep.cs` |
| `ValIO.Generators.TypeScript/Got/Ast/Builders/GotAstBuilder.cs` |
| `ValIO.Generators.TypeScript/Got/Ast/Renderers/GotNodeRenderer.cs` |

---

### 3.5 — Angular HttpClient

**📖 Документация:** `docs/refactoring/03_NEW_TS_CLIENTS.md` → Angular раздел

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/AngularRxjsClientStep.cs` |
| `ValIO.Generators.TypeScript/Steps/AngularPromiseClientStep.cs` |
| `ValIO.Generators.TypeScript/Angular/Ast/Builders/AngularServiceAstBuilder.cs` |
| `ValIO.Generators.TypeScript/Angular/Ast/Renderers/AngularNodeRenderer.cs` |

**Angular особенности:** Генерируется `@Injectable()` Service класс (не функция), использует `HttpClient` из `@angular/common/http`, RxJS Observable или Promise.

---

### 3.6 — SolidStart

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/SolidStartClientStep.cs` |
| `ValIO.Generators.TypeScript/Solid/Ast/Builders/SolidAstBuilder.cs` |
| `ValIO.Generators.TypeScript/Solid/Ast/Renderers/SolidNodeRenderer.cs` |

---

### 3.7 — MSW (Mock Service Worker)

**📖 Документация:** `docs/refactoring/03_NEW_TS_CLIENTS.md` → MSW раздел

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/MswStep.cs` |
| `ValIO.Generators.TypeScript/Msw/Ast/Builders/MswAstBuilder.cs` |
| `ValIO.Generators.TypeScript/Msw/Ast/Renderers/MswNodeRenderer.cs` |

**MSW генерирует:** handlers файл с `http.get('/path', resolver)` и `handlers` массив.

---

## Фаза 3b — TanStack для фреймворков

> Читать: `docs/refactoring/03_NEW_TS_CLIENTS.md` → TanStack Vue/Angular/Solid/Svelte

### 3b.1 — TanStack Vue

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/TanStackVueStep.cs` |
| `ValIO.Generators.TypeScript/TanStackQuery/Vue/Builders/TanStackVueAstBuilder.cs` |

**Vue особенности:** `useQuery` из `@tanstack/vue-query`, результат — `composable` функция (`use{OperationName}`), файлы в `composables/`.

---

### 3b.2 — TanStack Angular

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/TanStackAngularStep.cs` |
| `ValIO.Generators.TypeScript/TanStackQuery/Angular/Builders/TanStackAngularAstBuilder.cs` |

---

### 3b.3 — TanStack Solid

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/TanStackSolidStep.cs` |

---

### 3b.4 — TanStack Svelte

| Файл |
|------|
| `ValIO.Generators.TypeScript/Steps/TanStackSvelteStep.cs` |

---

## Фаза 4 — Новые языки

> Читать: `docs/refactoring/04_NEW_LANGUAGES.md` для каждого языка  
> Каждый язык = новый `ValIO.Generators.XXX` проект + `IGeneratorPlugin` + `IIrTypeMapper` + Steps + AST

---

### 4.1 — PHP Guzzle

**Новый проект:** `ValIO.Generators.Php/`

**Файлы для создания:**

| Файл |
|------|
| `ValIO.Generators.Php/PhpGuzzleGeneratorPlugin.cs` |
| `ValIO.Generators.Php/PhpIrTypeMapper.cs` |
| `ValIO.Generators.Php/Options/PhpOptionsDescriptor.cs` |
| `ValIO.Generators.Php/Steps/PhpModelsStep.cs` |
| `ValIO.Generators.Php/Steps/PhpEnumsStep.cs` |
| `ValIO.Generators.Php/Steps/GuzzleClientStep.cs` |
| `ValIO.Generators.Php/Ast/Builders/PhpModelAstBuilder.cs` |
| `ValIO.Generators.Php/Ast/Builders/GuzzleClientAstBuilder.cs` |
| `ValIO.Generators.Php/Ast/Renderers/PhpAstRenderer.cs` |
| `ValIO.Generators.Php/Ast/Visitors/IPhpAstVisitor.cs` |
| `ValIO.Generators.Php/Ast/Nodes/` | PHP-специфичные ноды |
| `ValIO.Generators.Php.csproj` | |

**PHP маппинг типов:**
```
string → string, int/long → int, float/double → float,
bool → bool, DateTime → \DateTime, array → array, object → array,
Blob/File → string (base64)
```

---

### 4.2 — PHP Laravel HTTP

| Файл |
|------|
| `ValIO.Generators.Php/Steps/LaravelHttpClientStep.cs` |
| `ValIO.Generators.Php/Ast/Builders/LaravelClientAstBuilder.cs` |

---

### 4.3 — Python HTTPX

**Новый проект:** `ValIO.Generators.Python/`

| Файл |
|------|
| `ValIO.Generators.Python/PythonHttpxGeneratorPlugin.cs` |
| `ValIO.Generators.Python/PythonIrTypeMapper.cs` |
| `ValIO.Generators.Python/Steps/PythonModelsStep.cs` (Pydantic v2) |
| `ValIO.Generators.Python/Steps/HttpxClientStep.cs` |
| `ValIO.Generators.Python/Steps/AiohttpClientStep.cs` |
| `ValIO.Generators.Python/Ast/Builders/PydanticModelAstBuilder.cs` |
| `ValIO.Generators.Python/Ast/Renderers/PythonAstRenderer.cs` |
| `ValIO.Generators.Python.csproj` | |

**Python маппинг типов:**
```
string → str, int/long → int, float/double → float,
bool → bool, DateTime → datetime, array → list[T],
dict → dict[str, T], Blob → bytes, Uuid → UUID, Any → Any
```

---

### 4.4 — Java OkHttp

**Новый проект:** `ValIO.Generators.Java/`

| Файл |
|------|
| `ValIO.Generators.Java/JavaOkHttpGeneratorPlugin.cs` |
| `ValIO.Generators.Java/JavaIrTypeMapper.cs` |
| `ValIO.Generators.Java/Steps/JavaModelsStep.cs` |
| `ValIO.Generators.Java/Steps/OkHttpClientStep.cs` |
| `ValIO.Generators.Java.csproj` | |

---

### 4.5 — Kotlin OkHttp

**Новый проект:** `ValIO.Generators.Kotlin/`

| Файл |
|------|
| `ValIO.Generators.Kotlin/KotlinOkHttpGeneratorPlugin.cs` |
| `ValIO.Generators.Kotlin/KotlinIrTypeMapper.cs` |
| `ValIO.Generators.Kotlin/Steps/KotlinModelsStep.cs` |
| `ValIO.Generators.Kotlin/Steps/KotlinOkHttpClientStep.cs` |
| `ValIO.Generators.Kotlin.csproj` | |

---

### 4.6 — Swift NSURLSession

**Новый проект:** `ValIO.Generators.Swift/`

| Файл |
|------|
| `ValIO.Generators.Swift/SwiftGeneratorPlugin.cs` |
| `ValIO.Generators.Swift/SwiftIrTypeMapper.cs` |
| `ValIO.Generators.Swift/Steps/SwiftModelsStep.cs` |
| `ValIO.Generators.Swift/Steps/SwiftClientStep.cs` |
| `ValIO.Generators.Swift.csproj` | |

---

### 4.7 — Dart Dio

**Новый проект:** `ValIO.Generators.Dart/`

| Файл |
|------|
| `ValIO.Generators.Dart/DartDioGeneratorPlugin.cs` |
| `ValIO.Generators.Dart/DartIrTypeMapper.cs` |
| `ValIO.Generators.Dart/Steps/DartModelsStep.cs` |
| `ValIO.Generators.Dart/Steps/DioClientStep.cs` |
| `ValIO.Generators.Dart.csproj` | |

---

### 4.8 — F# RestSharp

**Новый проект:** `ValIO.Generators.FSharp/`

| Файл |
|------|
| `ValIO.Generators.FSharp/FSharpGeneratorPlugin.cs` |
| `ValIO.Generators.FSharp/FSharpIrTypeMapper.cs` |
| `ValIO.Generators.FSharp/Steps/FSharpModelsStep.cs` |
| `ValIO.Generators.FSharp/Steps/RestSharpClientStep.cs` |
| `ValIO.Generators.FSharp.csproj` | |

---

## Фаза 5 — Инфраструктура

---

### 5.1 — GenerationOrchestrator (Facade)

**📖 Паттерны:** `docs/refactoring/09_DESIGN_PATTERNS.md` → Facade

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO/Services/GenerationOrchestrator.cs` | Facade: parser → IrBuilder → registry → pipeline |

**Файлы для изменения:**

| Файл | Что изменить |
|------|-------------|
| `ValIO/CLI/GenerateCommand.cs` | Использовать `GenerationOrchestrator` вместо прямых вызовов |

---

### 5.2 — IrTypeDependencyGraphBuilder

**📖 Документация:** `docs/refactoring/07_IR_NODES_AND_BUILDERS.md` → IrTypeDependencyGraph

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Core/IR/Builders/IrTypeDependencyGraphBuilder.cs` | Выделить из `IrBuilder` — топологическая сортировка |

---

### 5.3 — CachingSchemaResolver (Proxy)

**📖 Паттерны:** `docs/refactoring/09_DESIGN_PATTERNS.md` → Proxy

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Core/Services/CachingSchemaResolver.cs` | Proxy поверх `ISchemaResolver` с `Dictionary<string, IOpenApiSchema>` кэшем |

---

### 5.4 — SourceFileBuilder

**📖 Паттерны:** `docs/refactoring/09_DESIGN_PATTERNS.md` → Builder

**Файлы для создания:**

| Файл | Содержимое |
|------|-----------|
| `ValIO.Core/Ast/Builders/SourceFileBuilder.cs` | Fluent builder: `AddImport()`, `EnsureImport()`, `Add()`, `Build()` с автоматической дедупликацией импортов |

---

### 5.5 — Тесты

**Существующий проект:** `ValIO.Generators.TypeScript.Tests/`

**Добавить тесты для:**

| Что тестировать | Файл |
|-----------------|------|
| `IrBuilder.BuildFromOpenApi()` → правильный IR | `Tests/IrBuilderTests.cs` |
| `TypeScriptIrTypeMapper.MapType()` → все 14 примитивов + составные типы | `Tests/TypeScriptIrTypeMapperTests.cs` |
| `TypeScriptModelAstBuilder.BuildModelSourceFile()` → AST структура | `Tests/TypeScriptModelAstBuilderTests.cs` |
| `TypeScriptAstRenderer` → рендеринг простых типов, union, interface | `Tests/TypeScriptAstRendererTests.cs` |
| `ZodAstBuilder.BuildSchema()` → Zod v3 и v4 синтаксис | `Tests/ZodAstBuilderTests.cs` |
| `TypeScriptModelsStep.ExecuteAsync()` → сгенерированные файлы | `Tests/TypeScriptModelsStepTests.cs` |
| `GeneratorRegistry.FindPlugin()` → поиск по alias | `Tests/GeneratorRegistryTests.cs` |

---

## Что удалить из .sln / структуры проектов

| Элемент | Действие |
|---------|----------|
| `ValIO.Generators.TypeScript.Zod/` | 🗑️ Удалить проект целиком — это пустая оболочка |
| `Examples/valio-master/` | 🗑️ Удалить или перенести в ветку `archive/legacy` в git |
| `ValIO.Core/Ast/Builders/HttpAstBuilder.cs` | 🗑️ Проверить использование — вероятно удалить |

---

## Итоговая структура проектов (целевое состояние)

```
ValIO.sln
├── ValIO/                          CLI + DI + Orchestrator
│   ├── CLI/GenerateCommand.cs
│   ├── Services/GenerationOrchestrator.cs
│   └── Program.cs
│
├── ValIO.Core/                     Только ядро — без языков
│   ├── Ast/                        AST ноды + Visitor (без AstNodeKind)
│   ├── IR/                         IR модели + IrBuilder
│   ├── Plugins/                    IGeneratorPlugin, ICodeGenerationStep, Pipeline
│   ├── Interfaces/                 IIrTypeMapper, ISchemaResolver, INamingConverter, IFileWriter
│   └── Services/                   NamingConverter, FileWriter, CachingSchemaResolver
│
├── ValIO.Configuration/            Конфигурация (без языков)
│   └── Services/ConfigurationLoader.cs  — только делегирует плагинам
│
├── ValIO.Parsers/                  OpenAPI парсинг
│
├── ValIO.Generators.TypeScript/    TypeScript плагин
│   ├── TypeScriptGeneratorPlugin.cs
│   ├── TypeScriptIrTypeMapper.cs
│   ├── Options/TypeScriptOptionsDescriptor.cs
│   ├── Steps/                      TypeScriptModelsStep, EnumsStep, FetchStep, AxiosStep, ...
│   ├── Ast/Builders/               TypeScriptModelAstBuilder, TypeScriptEnumAstBuilder
│   ├── Ast/Renderers/              TypeScriptAstRenderer + ITypeScriptNodeRenderer
│   ├── Fetch/, Axios/, Ky/         Транспорт-специфичные папки
│   ├── Zod/                        Zod AST
│   ├── Valibot/                    (НОВОЕ)
│   ├── TypeBox/                    (НОВОЕ)
│   └── TanStackQuery/              React/Vue/Angular/Solid/Svelte
│
├── ValIO.Generators.CSharp/        C# плагин
│   ├── CSharpGeneratorPlugin.cs
│   ├── CSharpIrTypeMapper.cs
│   └── Steps/, Ast/
│
├── ValIO.Generators.Cpp/           C++/UE плагин (уже существует)
│
├── ValIO.Generators.Php/           (НОВОЕ) PHP плагин
├── ValIO.Generators.Python/        (НОВОЕ) Python плагин
├── ValIO.Generators.Java/          (НОВОЕ) Java плагин
├── ValIO.Generators.Kotlin/        (НОВОЕ) Kotlin плагин
├── ValIO.Generators.Swift/         (НОВОЕ) Swift плагин
├── ValIO.Generators.Dart/          (НОВОЕ) Dart плагин
└── ValIO.Generators.TypeScript.Tests/
```

---

## Счётчик изменений

| Категория | Количество |
|-----------|-----------|
| Файлов удалить (Core legacy) | ~38 |
| Файлов удалить (TypeScript legacy) | ~23 |
| Файлов удалить (CSharp legacy models) | ~6 |
| Файлов изменить (AST Accept fix) | ~80+ |
| Файлов создать (Plugin infra Core) | ~10 |
| Файлов создать (TS Steps + Builders) | ~25 |
| Файлов создать (Validation: Valibot, TypeBox) | ~15 |
| Файлов создать (New TS Transports) | ~30 |
| Файлов создать (New Languages: 6 проектов) | ~60 |
| Тестовых файлов создать | ~10 |
| **Итого изменений** | **~300+** |
