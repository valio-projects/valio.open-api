# 08 — AST Builders: детальный разбор и рефакторинг

> **Этап:** 0–1 (строится поверх 01_AST_REFACTORING и 07_IR_NODES_AND_BUILDERS)  
> **Приоритет:** Высокий  
> **Зависит от:** 01_AST_REFACTORING, 06_LEGACY_CLEANUP, 07_IR_NODES_AND_BUILDERS

---

## Содержание

1. [Обзор: текущее состояние всех AstBuilder'ов](#1-обзор-текущее-состояние-всех-астbuilderов)
2. [TypeScriptAstBuilder — анализ и проблемы](#2-typescriptastbuilder--анализ-и-проблемы)
3. [CSharpAstBuilder — анализ и проблемы](#3-csharpastbuilder--анализ-и-проблемы)
4. [CppAstBuilder и UE builders — анализ](#4-cppastbuilder-и-ue-builders--анализ)
5. [HttpAstBuilder — общий в Core](#5-httpastbuilder--общий-в-core)
6. [Проблема TypeInfo vs IrType — миграционный gap](#6-проблема-typeinfo-vs-irtype--миграционный-gap)
7. [Проблема: BuildType(IrType) использует switch на Kind](#7-проблема-buildtypeirtype-использует-switch-на-kind)
8. [Импорты: алгоритм и проблемы](#8-импорты-алгоритм-и-проблемы)
9. [Целевая архитектура: разбить God Object на Step + Builder](#9-целевая-архитектура-разбить-god-object-на-step--builder)
10. [Алгоритмы новых AstBuilder'ов для каждого языка](#10-алгоритмы-новых-астbuilderов-для-каждого-языка)
11. [Интеграция AstBuilder в plugin Step](#11-интеграция-astbuilder-в-plugin-step)

---

## 1. Обзор: текущее состояние всех AstBuilder'ов

| Builder | Файл(ы) | Строк | Статус |
|---------|---------|-------|--------|
| `TypeScriptAstBuilder` | `TypeScriptAstBuilder.cs` + `.EndpointHttp.cs` | ~1950 | God Object, частично мигрирован на IrType |
| `CSharpAstBuilder` | `CSharpAstBuilder.cs` | 3391 | God Object, частично мигрирован на IrType |
| `CppAstBuilder` | `CppAstBuilder.cs` | большой | Не смотрели детально |
| `UEApiAstBuilder` | `UEApiAstBuilder.cs` | — | Unreal Engine специфичный |
| `UEEnumAstBuilder` | `UEEnumAstBuilder.cs` | — | |
| `UEModelAstBuilder` | `UEModelAstBuilder.cs` | — | |
| `UEParamsAstBuilder` | `UEParamsAstBuilder.cs` | — | |
| `UEResultAstBuilder` | `UEResultAstBuilder.cs` | — | |
| `HttpAstBuilder` | `ValIO.Core/Ast/Builders/HttpAstBuilder.cs` | — | Общий в Core |
| `TypeScriptParamsAstBuilder` | `TypeScriptParamsAstBuilder.cs` | — | params-объекты |
| `TypeScriptResultsAstBuilder` | `TypeScriptResultsAstBuilder.cs` | — | результаты |

### Общая проблема: двойные перегрузки BuildType

И в `TypeScriptAstBuilder`, и в `CSharpAstBuilder` существуют **две перегрузки**:

```csharp
// LEGACY — использует TypeInfo (должна быть удалена)
public TypeNode BuildType(TypeInfo typeInfo) { ... }

// НОВАЯ — использует IrType (правильный путь)
public TypeNode BuildType(IrType irType) { ... }
```

Оба builder'а поддерживают переходный период. Нужно завершить миграцию — убрать все вызовы `BuildType(TypeInfo)` и удалить перегрузку.

---

## 2. TypeScriptAstBuilder — анализ и проблемы

### Структура (partial классы)

```
TypeScriptAstBuilder.cs              (~1568 строк)
├── Constructor
├── BuildTypeAlias
├── BuildType(TypeInfo)              ← LEGACY
├── BuildLiteral(TypeInfo)           ← LEGACY
├── BuildImports                     ← сложная логика путей
├── BuildType(IrType)                ← правильный путь
├── BuildSourceFile(IrModel)         ← → SourceFileNode с interface
├── BuildInterface(IrModel)          ← interface декларация
├── BuildProperty(IrProperty)        ← PropertyDeclaration
├── BuildSourceFile(IrEnum)          ← → SourceFileNode с enum/type alias
├── BuildFunction(IrOperation)       ← HTTP клиент функция
├── BuildSourceFiles(IrDocument)     ← основная точка входа
├── BuildSourceFile(IrClient)        ← все операции клиента
├── BuildSourceFileForOperation      ← один файл на операцию
└── вспомогательные private методы

TypeScriptAstBuilder.EndpointHttp.cs (~380 строк)
├── BuildSourceFileForTransport      ← axios/fetch вариант файла
├── BuildFunctionForTransport        ← axios/fetch функция
└── BuildAxiosCall / BuildFetchCall  ← конкретные HTTP вызовы
```

### Проблемы TypeScriptAstBuilder

**1. BuildType(IrType) использует switch на Kind (не pattern matching)**

```csharp
// ТЕКУЩИЙ КОД — switch на Kind
public TypeNode BuildType(IrType irType)
{
    return irType.Kind switch
    {
        IrTypeKind.Primitive     => BuildPrimitiveType((IrPrimitiveType)irType),
        IrTypeKind.ModelReference => BuildModelReference((IrModelReference)irType),
        IrTypeKind.Array         => BuildArrayType((IrArrayType)irType),
        IrTypeKind.Dictionary    => BuildDictionaryType((IrDictionaryType)irType),
        IrTypeKind.Union         => BuildUnionType((IrUnionType)irType),
        IrTypeKind.Intersection  => BuildIntersectionType((IrIntersectionType)irType),
        IrTypeKind.Nullable      => BuildNullableType((IrNullableType)irType),
        IrTypeKind.Optional      => BuildOptionalType((IrOptionalType)irType),
        _ => throw new NotSupportedException(...)
    };
}
```

**Правильный вариант — pattern matching на тип:**

```csharp
// ЦЕЛЕВОЙ КОД — pattern matching без Kind
public TypeNode BuildType(IrType irType) => irType switch
{
    IrPrimitiveType p    => BuildPrimitiveType(p),
    IrModelReference r   => new SimpleTypeNode(r.ModelName),
    IrArrayType a        => new ArrayTypeNode(BuildType(a.ElementType)),
    IrDictionaryType d   => new GenericTypeNode(new IdentifierNode("Record"),
                                [BuildType(d.KeyType), BuildType(d.ValueType)]),
    IrNullableType n     => BuildNullableType(n),
    IrOptionalType o     => BuildOptionalType(o),
    IrUnionType u        => new UnionTypeNode(u.Types.Select(BuildType).ToImmutableList()),
    IrIntersectionType i => new IntersectionTypeNode(i.Types.Select(BuildType).ToImmutableList()),
    _                    => throw new NotSupportedException($"IrType {irType.GetType().Name}")
};
```

**2. Nullable/Optional обработка — сложная и запутанная**

```csharp
// BuildInterfacePropertyTypeNode — пример сложной логики
private TypeNode BuildInterfacePropertyTypeNode(IrProperty property)
{
    if (!IsIrModelPropertyNullish(property))
        return BuildType(property.Type);

    var irCore = property.Type is IrNullableType n ? n.InnerType : property.Type;
    var valueCore = BuildType(irCore);
    var withNull = new NullableTypeNode(valueCore);
    var undefinedType = new SimpleTypeNode(new IdentifierNode("undefined"), isPrimitive: true);
    return new UnionTypeNode(ImmutableList.Create<TypeNode>(withNull, undefinedType));
}
```

Это работает, но трудно читаемо. Нужно вынести логику nullable/optional в `IIrTypeMapper`:

```csharp
// IIrTypeMapper должен инкапсулировать это
TypeNode MapType(IrType irType);
TypeNode MapPropertyType(IrProperty property);  // учитывает IsRequired + nullable
```

**3. BuildImports — лишняя сложность**

`BuildImports` содержит сложную логику вычисления относительных путей (`CalculateRelativePath`). Это бизнес-логика определения структуры файлов, которая **не должна быть в AstBuilder**.

AstBuilder должен строить AST с символическими импортами (путями типа `@models/UserDto` или `../models/userDto`), а **реальное разрешение путей** — задача `ICodeGenerationStep`.

**4. Нет разделения: models vs enums vs clients**

`BuildSourceFiles(IrDocument)` — единая точка входа, которая генерирует все типы файлов. Это затрудняет использование builder'а для отдельных задач (например, генерация только Zod схем).

**5. _activeEndpointTransport — скрытое состояние**

```csharp
private TypeScriptEndpointTransport _activeEndpointTransport = TypeScriptEndpointTransport.Axios;
```

Это mutable state в builder'е. Builder должен быть stateless — транспорт должен передаваться как параметр метода.

### Что нужно сделать с TypeScriptAstBuilder

1. **Удалить `BuildType(TypeInfo typeInfo)`** — после полной миграции
2. **Заменить switch на Kind на pattern matching** в `BuildType(IrType)`
3. **Вынести логику импортов** из builder'а в `Step` или `ImportResolver`
4. **Разделить на специализированные builder'ы:**
   - `TypeScriptModelAstBuilder` — только модели
   - `TypeScriptEnumAstBuilder` — только enum'ы
   - `TypeScriptClientAstBuilder` — только HTTP клиенты (base)
   - `AxiosClientAstBuilder` — Axios-специфичные методы
   - `FetchClientAstBuilder` — Fetch-специфичные методы
5. **Убрать `_activeEndpointTransport`** — передавать как параметр
6. **Вынести `BuildInterfacePropertyTypeNode`** в `TypeScriptIrTypeMapper`

---

## 3. CSharpAstBuilder — анализ и проблемы

### Структура (3391 строка — критический God Object)

```
CSharpAstBuilder.cs (3391 строк)
├── BuildNamespace
├── BuildType(TypeInfo)                        ← LEGACY
├── BuildLiteral(TypeInfo)                     ← LEGACY
├── BuildType(IrType)                          ← правильный путь
├── BuildCompilationUnit(IrModel)              ← модель → CompilationUnit
├── BuildClass(IrModel)                        ← C# класс
├── BuildProperty(IrProperty, IrModel?)        ← свойство
├── BuildCompilationUnit(IrEnum)               ← enum → CompilationUnit
├── BuildEnum(IrEnum)                          ← C# enum
├── BuildMethod(IrOperation)                   ← HTTP метод
├── BuildCompilationUnits(IrDocument)          ← все из документа
├── BuildCompilationUnit(IrClient)             ← клиент → класс
├── BuildCompilationUnitForInterface(IrClient) ← I[Name]Client интерфейс
├── BuildCompilationUnitForIHttpClientMetrics  ← метрики (!)
├── BuildCompilationUnitForHttpClientMetrics   ← метрики (!)
├── BuildCompilationUnitForDecorator(IrClient) ← декоратор (!)
├── BuildCompilationUnitApiException           ← ApiException класс (!)
└── огромное количество private методов
```

### Проблемы CSharpAstBuilder

**1. Генерирует инфраструктурный код (HttpClientMetrics, Decorator, ApiException)**

`CSharpAstBuilder` генерирует не только модели и клиенты, но и:
- `IHttpClientMetrics` — интерфейс для метрик
- `HttpClientMetrics` — реализация метрик
- `Decorator` — декоратор паттерн для клиента
- `ApiException` — класс исключений API

Это должны быть отдельные `ICodeGenerationStep`, а не методы на builder'е. Builder'у не место содержать бизнес-логику «что генерировать».

**2. 3391 строк — нарушение SRP**

Builder отвечает за:
- Модели (records/classes)
- Enum'ы
- HTTP клиенты (interfaces + implementations)
- Инфраструктурный код (metrics, decorators)
- Обработку allOf/oneOf/anyOf
- JSON атрибуты
- Nullable-логику C#

**Решение:** Разбить на:
- `CSharpModelAstBuilder` — модели (records, classes)
- `CSharpEnumAstBuilder` — enum'ы
- `CSharpClientAstBuilder` — HTTP клиенты
- `CSharpInfrastructureAstBuilder` — ApiException, metrics (внутри Step)

**3. `BuildType(IrType)` — switch на Kind**

```csharp
public TypeNode BuildType(IrType irType)
{
    return irType.Kind switch
    {
        IrTypeKind.Primitive     => BuildPrimitiveType((IrPrimitiveType)irType),
        IrTypeKind.ModelReference => BuildModelReference((IrModelReference)irType),
        IrTypeKind.Array         => BuildArrayType((IrArrayType)irType),
        IrTypeKind.Dictionary    => BuildDictionaryType((IrDictionaryType)irType),
        IrTypeKind.Union         => BuildUnionType((IrUnionType)irType),
        IrTypeKind.Intersection  => BuildIntersectionType((IrIntersectionType)irType),
        IrTypeKind.Nullable      => BuildNullableType((IrNullableType)irType),
        IrTypeKind.Optional      => BuildType(((IrOptionalType)irType).InnerType),
        _ => throw new NotSupportedException(...)
    };
}
```

**Правильный вариант:**
```csharp
public TypeNode BuildType(IrType irType) => irType switch
{
    IrPrimitiveType p    => BuildPrimitiveType(p),
    IrModelReference r   => new SimpleTypeNode(r.ModelName),
    IrArrayType a        => new GenericTypeNode(new IdentifierNode("List"), [BuildType(a.ElementType)]),
    IrDictionaryType d   => new GenericTypeNode(new IdentifierNode("Dictionary"),
                                [BuildType(d.KeyType), BuildType(d.ValueType)]),
    IrNullableType n     => new NullableTypeNode(BuildType(n.InnerType)),
    IrOptionalType o     => new NullableTypeNode(BuildType(o.InnerType)),  // optional → nullable в C#
    IrUnionType _        => new SimpleTypeNode("object"),  // C# не поддерживает union
    IrIntersectionType i => BuildIntersectionType(i),
    _                    => throw new NotSupportedException($"IrType {irType.GetType().Name}")
};
```

**4. `_modelsByName` — mutable state**

```csharp
private Dictionary<string, IrModel>? _modelsByName;
```

Builder хранит состояние между вызовами — нарушает принцип stateless. Нужно передавать как параметр или создавать lookup внутри вызова метода.

**5. CSharpGenerationOptions — не является IGeneratorPlugin**

```csharp
public CSharpAstBuilder(INamingConverter namingConverter, CSharpGenerationOptions? options = null)
```

`CSharpGenerationOptions` — language-specific класс, не интегрированный в plugin-архитектуру. После внедрения plugin-системы опции должны парситься через `IGeneratorPlugin.ParseOptions()`.

---

## 4. CppAstBuilder и UE builders — анализ

### CppAstBuilder + UE разделение

C++ генератор разбит на несколько specialized builder'ов — в отличие от TypeScript и C# это хороший пример:

- `CppAstBuilder` — общий, базовые C++ файлы
- `UEApiAstBuilder` — Unreal Engine API клиент
- `UEEnumAstBuilder` — UE enum'ы (`UENUM(BlueprintType)`)
- `UEModelAstBuilder` — UE модели (`USTRUCT(BlueprintType)`)
- `UEParamsAstBuilder` — параметры запросов
- `UEResultAstBuilder` — результаты ответов

Это правильная декомпозиция, которую нужно повторить для TypeScript и C#.

### Что взять как образец из C++ подхода

1. Отдельный builder для каждой концептуальной категории
2. Builder принимает конкретный набор параметров, не весь `IrDocument`
3. Специфика фреймворка (UE Macros) инкапсулирована внутри builder'а

---

## 5. HttpAstBuilder — общий в Core

```csharp
// ValIO.Core/Ast/Builders/HttpAstBuilder.cs
// Используется и TypeScriptAstBuilder, и CSharpAstBuilder
```

`HttpAstBuilder` — общий builder для HTTP-специфичных AST нод (`HttpMethodNode`, `RouteNode`, `PathParameterNode` и т.д.). Это правильно: HTTP-параметры общие для всех языков на уровне AST.

### Что нужно проверить в HttpAstBuilder

1. Не должен генерировать язык-специфичный код
2. Должен работать только с `IrOperation` и производить HTTP AST ноды
3. После рефакторинга IAstVisitor (убрать HTTP из IAstVisitor) — `HttpAstBuilder` остаётся, но его ноды обрабатываются через `IHttpAstVisitor` в конкретных рендерерах

---

## 6. Проблема TypeInfo vs IrType — миграционный gap

### Текущая ситуация

```
TypeScriptAstBuilder
├── BuildType(TypeInfo)  ← LEGACY, использовался в TemplateData pipeline
└── BuildType(IrType)    ← НОВЫЙ, используется в IR→AST pipeline

CSharpAstBuilder
├── BuildType(TypeInfo)  ← LEGACY
└── BuildType(IrType)    ← НОВЫЙ
```

Переходный период: оба метода существуют параллельно. Проблема в том, что `TypeInfo` используется во вспомогательных методах, которые ещё не мигрированы:

```csharp
// TypeScriptAstBuilder — legacy методы с TypeInfo
public TypeNode BuildType(TypeInfo typeInfo)
{
    if (typeInfo.IsCollection)  // ← TypeInfo.IsCollection
    {
        var elementType = BuildTypeFromBaseType(
            typeInfo.GenericArguments.FirstOrDefault() ?? TypeInfo.Simple(typeInfo.BaseType));
        // ...
    }
}
```

### Стратегия миграции

**Шаг 1:** Найти все вызовы `BuildType(TypeInfo)` в кодовой базе:
```bash
grep -rn "BuildType(typeInfo\|BuildType(TypeInfo\|new TypeInfo\|TypeInfo\.Simple\|TypeInfo\.Nullable\|TypeInfo\.Collection" --include="*.cs"
```

**Шаг 2:** Для каждого вызова определить, откуда берётся `TypeInfo` — скорее всего из `ITypeMapper.MapTypeInfo()`. Этот маппер тоже legacy.

**Шаг 3:** Заменить цепочку:
```
OpenAPI schema → ITypeMapper.MapTypeInfo() → TypeInfo → BuildType(TypeInfo)
```
На:
```
IrType (уже в IrModel.Properties[].Type) → BuildType(IrType)
```

**Шаг 4:** После полной замены — удалить `BuildType(TypeInfo)` и `TypeInfo` класс.

### Почему TypeInfo до сих пор существует

`TypeInfo` существует потому что некоторые методы builder'а ещё принимают его:

```csharp
// TypeScriptParamsAstBuilder — использует TypeInfo?
public class TypeScriptParamsAstBuilder
{
    // Проверить: использует TypeInfo или уже IrType?
}
```

Нужно проверить все builder'ы на предмет использования `TypeInfo`.

---

## 7. Проблема: BuildType(IrType) использует switch на Kind

### Почему это проблема

```csharp
return irType.Kind switch
{
    IrTypeKind.Primitive      => BuildPrimitiveType((IrPrimitiveType)irType),
    IrTypeKind.ModelReference => BuildModelReference((IrModelReference)irType),
    // ...
    _ => throw new NotSupportedException(...)
};
```

1. **Небезопасный каст `(IrPrimitiveType)irType`** — если `Kind` и реальный тип рассинхронизированы — runtime exception
2. **Нарушение exhaustiveness через компилятор** — компилятор не предупредит если добавить новый `IrTypeKind` без обработки в switch
3. **`IrTypeKind` — лишний enum** — аналогично `AstNodeKind`, но для IR

### Исправление через pattern matching на тип записи

```csharp
// ПРАВИЛЬНО — pattern matching на конкретный тип
public TypeNode BuildType(IrType irType) => irType switch
{
    IrPrimitiveType p    => BuildPrimitiveType(p),   // компилятор знает что p — IrPrimitiveType
    IrModelReference r   => new SimpleTypeNode(r.ModelName),
    IrArrayType a        => new ArrayTypeNode(BuildType(a.ElementType)),
    IrDictionaryType d   => ...,
    IrNullableType n     => new NullableTypeNode(BuildType(n.InnerType)),
    IrOptionalType o     => BuildOptionalType(o),
    IrUnionType u        => new UnionTypeNode(u.Types.Select(BuildType).ToImmutableList()),
    IrIntersectionType i => new IntersectionTypeNode(i.Types.Select(BuildType).ToImmutableList()),
    _                    => throw new NotSupportedException($"Unknown IrType: {irType.GetType().Name}")
};
```

### Удалить IrTypeKind из IrType

```csharp
// БЫЛО:
public abstract record IrType
{
    public abstract IrTypeKind Kind { get; }  // ← удалить
}

// СТАЛО:
public abstract record IrType { }  // нет Kind — разрешение через тип записи
```

После удаления `Kind` — все switch(irType.Kind) перестанут компилироваться и их нужно будет заменить на pattern matching. Это такая же механическая работа как удаление `AstNodeKind`.

---

## 8. Импорты: алгоритм и проблемы

### Текущая логика в TypeScriptAstBuilder

```csharp
private IReadOnlyList<ImportNode> BuildImports(
    IReadOnlyList<string> dependencies,
    string? importPath,
    FileOrganization? fileOrganization = null,
    FileCategory? currentFileCategory = null,
    string? currentModelOrEnumName = null,
    string? currentOperationName = null)
{
    // Сложная логика вычисления относительных путей
    // CalculateRelativePath, NormalizePath, importsByFile group
}
```

Эта логика нарушает принцип **единственной ответственности** для builder'а. Builder должен создавать AST, а не заниматься разрешением файловых путей.

### Правильное разделение

```
ImportNode в AST
    │
    ├── Символический импорт: ImportNode(from: "@models/UserDto", ...)
    │   ← Builder создаёт этот вид
    │
    └── Реальный импорт: ImportNode(from: "../../models/user-dto", ...)
        ← ImportResolver преобразует перед рендерингом
```

**`ImportResolver`** — новый компонент (в `ICodeGenerationStep`):

```csharp
public class TypeScriptImportResolver
{
    private readonly FileOrganization _fileOrg;

    public SourceFileNode ResolveImports(SourceFileNode file, string currentFilePath)
    {
        var resolvedImports = file.Imports.Select(import =>
            new ImportNode(
                items: import.Items,
                from: ResolveRelativePath(import.From, currentFilePath),
                kind: import.Kind));

        return file with { Imports = resolvedImports.ToImmutableList() };
    }

    private string ResolveRelativePath(string symbolicPath, string fromPath)
    {
        // Только здесь — логика относительных путей
    }
}
```

### Правила именования импортов

TypeScript, в отличие от C#, требует явных импортов. Правила:

1. **Модели** импортируются как `type` (с `import type { X } from '...'`) — не копируют значение в runtime
2. **Enum'ы** импортируются без `type` — нужны в runtime (const enum)
3. **Функции клиентов** импортируются без `type` — нужны в runtime
4. **Zod схемы** импортируются без `type`

```csharp
// В builder'е: символические пути
var imports = new List<ImportNode>
{
    new ImportNode(
        items: [new IdentifierNode("UserDto")],
        from: "@models/user-dto",       // символический путь
        kind: ImportKind.TypeNamed),    // import type { UserDto }
    new ImportNode(
        items: [new IdentifierNode("RoleEnum")],
        from: "@models/role-enum",
        kind: ImportKind.Named),        // import { RoleEnum } (не type — нужен в runtime)
};
```

---

## 9. Целевая архитектура: разбить God Object на Step + Builder

### Принцип разбивки

```
ICodeGenerationStep (что генерировать и куда писать)
    │
    └── IAstBuilder (как строить AST из IR)
            │
            └── IIrTypeMapper (как маппить типы)
                    │
                    └── IAstRenderer (как рендерить AST в строку)
```

### TypeScript — целевая декомпозиция

```
ValIO.Generators.TypeScript/
├── Steps/
│   ├── TypeScriptModelsStep.cs        ← IrModel[] → model файлы
│   ├── TypeScriptEnumsStep.cs         ← IrEnum[]  → enum файлы
│   ├── TypeScriptIndexStep.cs         ← barrel exports index.ts
│   ├── AxiosClientStep.cs             ← IrClient[] → axios файлы
│   ├── FetchClientStep.cs             ← IrClient[] → fetch файлы
│   ├── KyClientStep.cs                ← IrClient[] → ky файлы
│   ├── ZodSchemasStep.cs              ← IrModel[] → zod схемы
│   └── TanStackQueryStep.cs           ← IrClient[] → tanstack hooks
│
├── Builders/
│   ├── TypeScriptModelAstBuilder.cs   ← IrModel → InterfaceDeclarationNode
│   ├── TypeScriptEnumAstBuilder.cs    ← IrEnum → EnumDeclarationNode
│   ├── AxiosClientAstBuilder.cs       ← IrClient → axios SourceFileNode
│   ├── FetchClientAstBuilder.cs       ← IrClient → fetch SourceFileNode
│   ├── KyClientAstBuilder.cs          ← IrClient → ky SourceFileNode
│   ├── ZodSchemaAstBuilder.cs         ← IrModel → zod SourceFileNode
│   └── TanStackQueryAstBuilder.cs     ← IrClient → tanstack SourceFileNode
│
├── Mappers/
│   └── TypeScriptIrTypeMapper.cs      ← IrType → TypeNode
│
└── Resolvers/
    └── TypeScriptImportResolver.cs    ← @model/X → ../../models/x
```

### C# — целевая декомпозиция

```
ValIO.Generators.CSharp/
├── Steps/
│   ├── CSharpModelsStep.cs            ← IrModel[] → .cs файлы
│   ├── CSharpEnumsStep.cs             ← IrEnum[] → .cs файлы
│   ├── CSharpClientsStep.cs           ← IrClient[] → interface + impl .cs
│   ├── CSharpApiExceptionStep.cs      ← ApiException.cs
│   └── RestSharpClientStep.cs         ← RestSharp-специфичный клиент
│
├── Builders/
│   ├── CSharpModelAstBuilder.cs       ← IrModel → RecordDeclarationNode
│   ├── CSharpEnumAstBuilder.cs        ← IrEnum → EnumDeclarationNode
│   ├── CSharpClientAstBuilder.cs      ← IrClient → ClassDeclarationNode
│   └── CSharpInterfaceAstBuilder.cs   ← IrClient → InterfaceDeclarationNode
│
└── Mappers/
    └── CSharpIrTypeMapper.cs          ← IrType → TypeNode
```

---

## 10. Алгоритмы новых AstBuilder'ов для каждого языка

### TypeScriptModelAstBuilder

```csharp
public class TypeScriptModelAstBuilder
{
    private readonly TypeScriptIrTypeMapper _typeMapper;
    private readonly INamingConverter _naming;

    /// <summary>
    /// IrModel → InterfaceDeclarationNode (или TypeAliasDeclarationNode для union)
    /// </summary>
    public DeclarationNode BuildModelDeclaration(IrModel model)
    {
        // oneOf/anyOf → type alias с union
        if (model.Composition?.Kind is IrCompositionKind.OneOf or IrCompositionKind.AnyOf)
            return BuildUnionTypeAlias(model);

        // обычная модель → interface
        return BuildInterface(model);
    }

    private InterfaceDeclarationNode BuildInterface(IrModel model)
    {
        var properties = model.Properties
            .Select(BuildPropertySignature)
            .ToList();

        // allOf → extends
        var extendsTypes = model.Composition?.Kind == IrCompositionKind.AllOf
            ? model.Composition.Types
                  .OfType<IrModelReference>()
                  .Select(r => (TypeNode)new SimpleTypeNode(r.ModelName))
                  .ToList()
            : null;

        return new InterfaceDeclarationNode(
            name: new IdentifierNode(model.Name),
            properties: properties,
            extends: extendsTypes,
            isExported: true,
            documentation: BuildJsDocComment(model.Description));
    }

    private PropertySignatureNode BuildPropertySignature(IrProperty property)
    {
        return new PropertySignatureNode(
            name: new IdentifierNode(property.Name),
            type: _typeMapper.MapPropertyType(property),
            isOptional: !property.IsRequired || property.Type is IrOptionalType,
            documentation: BuildJsDocComment(property.Description));
    }

    private TypeAliasDeclarationNode BuildUnionTypeAlias(IrModel model)
    {
        var unionTypes = model.Composition!.Types.Select(t => _typeMapper.MapType(t)).ToList();
        return new TypeAliasDeclarationNode(
            name: new IdentifierNode(model.Name),
            type: new UnionTypeNode(unionTypes),
            isExported: true,
            documentation: BuildJsDocComment(model.Description));
    }

    private CommentNode? BuildJsDocComment(string? description)
        => description != null ? new CommentNode(description, CommentStyle.JsDoc) : null;
}
```

### TypeScriptEnumAstBuilder

```csharp
public class TypeScriptEnumAstBuilder
{
    public enum EnumStyle { ConstEnum, UnionType, AsConst }

    private readonly EnumStyle _style;

    /// <summary>
    /// IrEnum → EnumDeclarationNode (const enum) или TypeAliasDeclarationNode (union)
    /// </summary>
    public DeclarationNode BuildEnum(IrEnum irEnum)
    {
        return _style switch
        {
            EnumStyle.ConstEnum  => BuildConstEnum(irEnum),
            EnumStyle.UnionType  => BuildUnionTypeAlias(irEnum),
            EnumStyle.AsConst    => BuildAsConstObject(irEnum),
            _ => BuildConstEnum(irEnum)
        };
    }

    private EnumDeclarationNode BuildConstEnum(IrEnum irEnum)
    {
        var members = irEnum.Values.Select(v => new EnumMemberDeclarationNode(
            name: new IdentifierNode(v.Name),
            value: BuildEnumMemberValue(v)
        )).ToList();

        return new EnumDeclarationNode(
            name: new IdentifierNode(irEnum.Name),
            members: members,
            isConst: true,
            isExported: true);
    }

    private TypeAliasDeclarationNode BuildUnionTypeAlias(IrEnum irEnum)
    {
        // string enum → "value1" | "value2" | "value3"
        // int enum → 0 | 1 | 2
        var unionTypes = irEnum.Values
            .Select(v => (TypeNode)new RawTypeNode(
                irEnum.IsStringEnum ? $"\"{v.Value}\"" : v.Value.ToString()!))
            .ToList();

        return new TypeAliasDeclarationNode(
            name: new IdentifierNode(irEnum.Name),
            type: new UnionTypeNode(unionTypes),
            isExported: true);
    }
}
```

### AxiosClientAstBuilder

```csharp
public class AxiosClientAstBuilder
{
    private readonly TypeScriptIrTypeMapper _typeMapper;

    /// <summary>
    /// IrClient → SourceFileNode (набор async функций с axios)
    /// </summary>
    public SourceFileNode BuildClientFile(IrClient client)
    {
        var functions = client.Operations
            .Select(BuildAxiosFunction)
            .ToList();

        var imports = BuildClientImports(client);

        return new SourceFileNode(
            declarations: functions,
            imports: imports);
    }

    private FunctionDeclarationNode BuildAxiosFunction(IrOperation op)
    {
        var parameters = BuildOperationParameters(op);
        var returnType = BuildReturnType(op);
        var body = BuildAxiosBody(op);

        return new FunctionDeclarationNode(
            name: new IdentifierNode(op.MethodName),
            parameters: parameters,
            returnType: returnType,
            body: body,
            isAsync: true,
            isExported: true,
            documentation: BuildOperationComment(op));
    }

    private BlockNode BuildAxiosBody(IrOperation op)
    {
        // const url = `/api/users/${id}`;
        var urlDecl = BuildUrlDeclaration(op);

        // const response = await axios.get<UserDto>(url, { params: { ... } });
        var axiosCallDecl = BuildAxiosCallDeclaration(op);

        // return response.data;
        var returnStmt = new ReturnStatementNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode("response"),
                new IdentifierNode("data")));

        return new BlockNode([urlDecl, axiosCallDecl, returnStmt]);
    }

    private StatementNode BuildUrlDeclaration(IrOperation op)
    {
        // Если нет path params → простая строка
        // Если есть path params → template literal
        ExpressionNode urlExpr = op.PathParameters.Any()
            ? BuildTemplateLiteralUrl(op.Path, op.PathParameters)
            : new LiteralExpressionNode(op.Path, LiteralKind.String);

        return new VariableDeclarationStatementNode(
            name: new IdentifierNode("url"),
            initializer: urlExpr,
            isConst: true);
    }

    private ExpressionNode BuildTemplateLiteralUrl(string path, IReadOnlyList<IrParameter> pathParams)
    {
        // "/api/users/{id}/posts/{postId}"
        // → `\api\users\${id}\posts\${postId}`
        var parts = new List<TemplateLiteralPart>();
        var remaining = path;

        foreach (var param in pathParams.OrderBy(p => path.IndexOf($"{{{p.Name}}}")))
        {
            var idx = remaining.IndexOf($"{{{param.Name}}}");
            if (idx >= 0)
            {
                parts.Add(new TemplateLiteralPart(remaining[..idx]));
                parts.Add(new TemplateLiteralPart(new IdentifierExpressionNode(param.Name)));
                remaining = remaining[(idx + param.Name.Length + 2)..];
            }
        }

        if (remaining.Length > 0)
            parts.Add(new TemplateLiteralPart(remaining));

        return new TemplateLiteralExpressionNode(parts);
    }
}
```

### ZodSchemaAstBuilder

```csharp
public class ZodSchemaAstBuilder
{
    private readonly TypeScriptIrTypeMapper _typeMapper;

    /// <summary>
    /// IrModel → VariableDeclarationNode (Zod schema)
    ///
    /// export const UserDtoSchema = z.object({
    ///   id: z.string(),
    ///   name: z.string(),
    ///   age: z.number().int().optional(),
    /// });
    ///
    /// export type UserDto = z.infer<typeof UserDtoSchema>;
    /// </summary>
    public IReadOnlyList<DeclarationNode> BuildZodSchema(IrModel model)
    {
        var schemaExpr = BuildZodObject(model);

        var schemaDecl = new VariableDeclarationNode(
            name: new IdentifierNode($"{model.Name}Schema"),
            initializer: schemaExpr,
            isConst: true,
            isExported: true);

        // type UserDto = z.infer<typeof UserDtoSchema>
        var typeAlias = new TypeAliasDeclarationNode(
            name: new IdentifierNode(model.Name),
            type: new GenericTypeNode(
                new IdentifierNode("z.infer"),
                [new RawTypeNode($"typeof {model.Name}Schema")]),
            isExported: true);

        return [schemaDecl, typeAlias];
    }

    private ExpressionNode BuildZodObject(IrModel model)
    {
        // z.object({ ... })
        var properties = model.Properties
            .Select(p => new PropertyAssignmentNode(
                p.Name,
                BuildZodTypeExpr(p.Type, p.IsRequired, p.Constraints)))
            .ToList();

        var zodObject = new CallExpressionNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode("z"),
                new IdentifierNode("object")),
            [new ObjectLiteralExpressionNode(properties)]);

        return zodObject;
    }

    private ExpressionNode BuildZodTypeExpr(IrType type, bool isRequired, IrPropertyConstraints? constraints)
    {
        var baseExpr = BuildZodBaseType(type);

        // Применяем constraints
        if (constraints != null)
            baseExpr = ApplyConstraints(baseExpr, constraints, type);

        // optional/nullable
        if (!isRequired)
            baseExpr = ChainCall(baseExpr, "optional");
        else if (type is IrNullableType)
            baseExpr = ChainCall(baseExpr, "nullable");

        return baseExpr;
    }

    private ExpressionNode BuildZodBaseType(IrType type) => type switch
    {
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.String }   => ZodMethod("string"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Number }   => ZodMethod("number"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Integer }  => ChainCall(ZodMethod("number"), "int"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Boolean }  => ZodMethod("boolean"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.DateTime } => ZodMethod("string").Then("datetime"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Uuid }     => ZodMethod("string").Then("uuid"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Any }      => ZodMethod("unknown"),
        IrModelReference r   => new IdentifierExpressionNode($"{r.ModelName}Schema"),
        IrArrayType a        => new CallExpressionNode(
                                    new MemberAccessExpressionNode(new IdentifierExpressionNode("z"), new IdentifierNode("array")),
                                    [BuildZodBaseType(a.ElementType)]),
        IrNullableType n     => ChainCall(BuildZodBaseType(n.InnerType), "nullable"),
        IrOptionalType o     => ChainCall(BuildZodBaseType(o.InnerType), "optional"),
        IrUnionType u        => new CallExpressionNode(
                                    new MemberAccessExpressionNode(new IdentifierExpressionNode("z"), new IdentifierNode("union")),
                                    [new ArrayLiteralExpressionNode(u.Types.Select(BuildZodBaseType).ToList())]),
        _ => ZodMethod("unknown")
    };

    private ExpressionNode ApplyConstraints(ExpressionNode expr, IrPropertyConstraints c, IrType type)
    {
        if (c.MinLength.HasValue) expr = ChainCall(expr, "min", c.MinLength.Value);
        if (c.MaxLength.HasValue) expr = ChainCall(expr, "max", c.MaxLength.Value);
        if (c.Pattern != null)    expr = ChainCall(expr, "regex", new LiteralExpressionNode($"/{c.Pattern}/", LiteralKind.Raw));
        if (c.Min.HasValue)       expr = ChainCall(expr, "gte", c.Min.Value);
        if (c.Max.HasValue)       expr = ChainCall(expr, "lte", c.Max.Value);
        return expr;
    }

    private static ExpressionNode ZodMethod(string name)
        => new CallExpressionNode(
            new MemberAccessExpressionNode(new IdentifierExpressionNode("z"), new IdentifierNode(name)),
            []);

    private static ExpressionNode ChainCall(ExpressionNode expr, string method, object? arg = null)
    {
        var args = arg == null
            ? (IReadOnlyList<ExpressionNode>)[]
            : [new LiteralExpressionNode(arg, arg is string ? LiteralKind.String : LiteralKind.Number)];
        return new CallExpressionNode(
            new MemberAccessExpressionNode(expr, new IdentifierNode(method)),
            args);
    }
}
```

---

## 11. Интеграция AstBuilder в plugin Step

### Пример: TypeScriptModelsStep

```csharp
public class TypeScriptModelsStep : ICodeGenerationStep
{
    private readonly TypeScriptModelAstBuilder _modelBuilder;
    private readonly TypeScriptEnumAstBuilder _enumBuilder;
    private readonly TypeScriptAstRenderer _renderer;
    private readonly TypeScriptImportResolver _importResolver;

    public int Order => 10;
    public string Name => "TypeScript Models";

    public async Task ExecuteAsync(GenerationContext context, CancellationToken ct)
    {
        var ir = context.IrDocument;
        var config = context.Config;
        var fileOrg = config.FileOrganization;

        // Генерация по одному файлу на модель
        foreach (var model in ir.Models)
        {
            // 1. Строим AST
            var declaration = _modelBuilder.BuildModelDeclaration(model);
            var imports = BuildModelImports(model, ir.TypeDependencies, fileOrg);
            var sourceFile = new SourceFileNode(
                declarations: [declaration],
                imports: imports);

            // 2. Разрешаем импорты (символические → относительные)
            var filePath = ResolveModelFilePath(model.Name, fileOrg);
            var resolvedFile = _importResolver.ResolveImports(sourceFile, filePath);

            // 3. Рендерим
            var code = _renderer.Render(resolvedFile);

            // 4. Добавляем в контекст
            context.GeneratedFiles[filePath] = code;
        }

        // Аналогично для enum'ов
        foreach (var irEnum in ir.Enums)
        {
            var declaration = _enumBuilder.BuildEnum(irEnum);
            // ...
        }
    }
}
```

### Пример: ZodSchemasStep

```csharp
public class ZodSchemasStep : ICodeGenerationStep
{
    private readonly ZodSchemaAstBuilder _zodBuilder;
    private readonly TypeScriptAstRenderer _renderer;

    public int Order => 20;  // после TypeScriptModelsStep
    public string Name => "Zod Schemas";

    public async Task ExecuteAsync(GenerationContext context, CancellationToken ct)
    {
        var declarations = new List<DeclarationNode>();
        var imports = new List<ImportNode>
        {
            // import { z } from 'zod';
            new ImportNode([new IdentifierNode("z")], "zod", ImportKind.Named)
        };

        foreach (var model in context.IrDocument.Models)
        {
            var zodDeclarations = _zodBuilder.BuildZodSchema(model);
            declarations.AddRange(zodDeclarations);

            // import { UserDtoSchema } from './models/user-dto' (для referenced models)
            // ...добавляем импорты referenced схем
        }

        var sourceFile = new SourceFileNode(declarations: declarations, imports: imports);
        var code = _renderer.Render(sourceFile);

        context.GeneratedFiles["schemas/index.ts"] = code;
    }
}
```

### Порядок Steps (Order)

| Step | Order | Описание |
|------|-------|----------|
| `IrBuildStep` | 0 | OpenAPI → IrDocument |
| `TypeScriptModelsStep` | 10 | IrModel[] → model файлы |
| `TypeScriptEnumsStep` | 20 | IrEnum[] → enum файлы |
| `AxiosClientStep` | 30 | IrClient[] → axios файлы |
| `FetchClientStep` | 31 | IrClient[] → fetch файлы |
| `KyClientStep` | 32 | IrClient[] → ky файлы |
| `ZodSchemasStep` | 40 | IrModel[] → zod схемы |
| `ValibotSchemasStep` | 41 | IrModel[] → valibot схемы |
| `TypeBoxSchemasStep` | 42 | IrModel[] → typebox схемы |
| `TanStackQueryStep` | 50 | IrClient[] → tanstack hooks |
| `TypeScriptIndexStep` | 90 | barrel exports (index.ts) |
| `WriteFilesStep` | 99 | запись файлов на диск |
