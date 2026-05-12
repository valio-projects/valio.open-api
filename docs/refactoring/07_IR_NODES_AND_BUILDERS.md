# 07 — IR: узлы, IrBuilder и конвертация IR → AST

> **Этап:** 0 (фундамент, параллельно с 01 и 02)  
> **Приоритет:** Высокий  
> **Зависит от:** 01_AST_REFACTORING, 06_LEGACY_CLEANUP

---

## Содержание

1. [Архитектура IR — зачем и что это такое](#1-архитектура-ir--зачем-и-что-это-такое)
2. [Полная карта IR нод](#2-полная-карта-ir-нод)
3. [IrType иерархия — типы в IR](#3-irtype-иерархия--типы-в-ir)
4. [IrBuilder — алгоритм построения IR из OpenAPI](#4-irbuilder--алгоритм-построения-ir-из-openapi)
5. [Проблемы и улучшения IrBuilder](#5-проблемы-и-улучшения-irbuilder)
6. [Конвертация IR → AST: общая схема](#6-конвертация-ir--ast-общая-схема)
7. [IrType → AstTypeNode: маппинг для каждого языка](#7-irtype--asttypenode-маппинг-для-каждого-языка)
8. [IrModel → AST: алгоритм построения деклараций](#8-irmodel--ast-алгоритм-построения-деклараций)
9. [IrOperation → AST: алгоритм построения HTTP методов](#9-iroperation--ast-алгоритм-построения-http-методов)
10. [IrEnum → AST: алгоритм построения enum'ов](#10-irenum--ast-алгоритм-построения-enumов)
11. [IrTypeDependencyGraph — порядок генерации и импорты](#11-irtypedependencygraph--порядок-генерации-и-импорты)
12. [Что добавить в IR (недостающие сущности)](#12-что-добавить-в-ir-недостающие-сущности)
13. [Целевой IAstBuilder контракт](#13-целевой-iastbuilder-контракт)

---

## 1. Архитектура IR — зачем и что это такое

### Место IR в общем pipeline

```
OpenAPI Document
    │
    ▼
IrBuilder.BuildFromOpenApi()
    │
    ▼
IrDocument  ←── Language-Agnostic Intermediate Representation
    │               Не знает о TypeScript/C#/PHP
    │               Знает: модели, enum'ы, клиенты, типы
    │
    ▼
[Lang]AstBuilder.Build()
    │
    ▼
AST (язык-специфичное дерево)  ←── знает о синтаксисе
    │                                TypeScript: interface, type, async function
    │                                C#: class, record, Task<T>
    │                                PHP: class, ?Type
    │
    ▼
[Lang]AstRenderer.Render()
    │
    ▼
string (готовый исходный код)
```

### Зачем нужен IR (а не сразу AST из OpenAPI)

Без IR каждый новый язык делает разбор OpenAPI сам — дублируя логику работы с `$ref`, `allOf`/`oneOf`/`anyOf`, inline-схемами, nullable, форматами, и т.д. С IR:

- Логика разбора OpenAPI — **один раз** в `IrBuilder`
- Каждый AstBuilder получает чистые, уже разрешённые данные
- Добавление нового языка = только новый `AstBuilder`, без разбора OpenAPI

### Принципы IR

1. **Language-agnostic:** IR не знает о TypeScript, C#, PHP. Знает только о данных.
2. **Immutable:** Все IR записи — `sealed record` с `required init` полями.
3. **Полностью разрешённый:** Все `$ref` уже разрешены в `IrModelReference`. Никаких ссылок на внешние схемы.
4. **Типизированный:** Типы — `IrType` иерархия, не строки.
5. **Нормализованный:** Имена sanitized через `NamingConverter`. Дублей нет.

---

## 2. Полная карта IR нод

```
IrDocument
├── Metadata: IrDocumentMetadata
│   ├── Title: string?
│   ├── Version: string?
│   ├── Description: string?
│   └── ServerUrl: string?
│
├── Models: IReadOnlyList<IrModel>
│   └── IrModel
│       ├── Name: string                   (sanitized)
│       ├── Description: string?
│       ├── Properties: IReadOnlyList<IrProperty>
│       │   └── IrProperty
│       │       ├── OriginalName: string   (из OpenAPI, до санитизации)
│       │       ├── Name: string           (sanitized)
│       │       ├── Type: IrType           (разрешённый тип)
│       │       ├── IsRequired: bool
│       │       ├── Description: string?
│       │       └── Constraints: IrPropertyConstraints?
│       │           ├── Min: decimal?
│       │           ├── Max: decimal?
│       │           ├── ExclusiveMin: decimal?
│       │           ├── ExclusiveMax: decimal?
│       │           ├── MinLength: int?
│       │           ├── MaxLength: int?
│       │           ├── Pattern: string?
│       │           └── DefaultValue: object?
│       │
│       ├── Composition: IrComposition?   (allOf/oneOf/anyOf)
│       │   ├── Kind: IrCompositionKind   (AllOf | OneOf | AnyOf)
│       │   ├── Types: IReadOnlyList<IrType>
│       │   ├── DiscriminatorPropertyName: string?
│       │   └── DiscriminatorMapping: IReadOnlyDictionary<string, string>?
│       │
│       └── OriginalSchema: IOpenApiSchema?  (для отладки — удалить в prod?)
│
├── Enums: IReadOnlyList<IrEnum>
│   └── IrEnum
│       ├── Name: string
│       ├── Description: string?
│       ├── Values: IReadOnlyList<IrEnumValue>
│       │   └── IrEnumValue
│       │       ├── Name: string          (sanitized, PascalCase)
│       │       ├── Value: object         (string или int)
│       │       └── Description: string?
│       └── IsStringEnum: bool
│
├── Clients: IReadOnlyList<IrClient>
│   └── IrClient
│       ├── Name: string
│       └── Operations: IReadOnlyList<IrOperation>
│           └── IrOperation
│               ├── MethodName: string        (sanitized, camelCase)
│               ├── HttpMethod: string        ("GET", "POST", "PUT", "DELETE", "PATCH")
│               ├── Path: string             ("/api/users/{id}")
│               ├── Summary: string?
│               ├── Description: string?
│               ├── PathParameters: IReadOnlyList<IrParameter>
│               ├── QueryParameters: IReadOnlyList<IrParameter>
│               ├── HeaderParameters: IReadOnlyList<IrParameter>
│               ├── RequestBody: IrRequestBody?
│               │   ├── Type: IrType
│               │   ├── IsRequired: bool
│               │   ├── ContentType: string?
│               │   ├── IsJson: bool
│               │   ├── IsForm: bool
│               │   ├── IsMultipartFormData: bool
│               │   └── IsFormUrlEncoded: bool
│               ├── Responses: IReadOnlyList<IrResponse>
│               │   ├── StatusCode: string
│               │   ├── Type: IrType?
│               │   └── Description: string?
│               └── Tags: IReadOnlyList<string>?
│
└── TypeDependencies: IrTypeDependencyGraph
    ├── Dependencies: IReadOnlyDictionary<string, IReadOnlyList<string>>
    └── TopologicalOrder: IReadOnlyList<string>
```

---

## 3. IrType иерархия — типы в IR

```
IrType (abstract record)
│
├── IrPrimitiveType
│   └── PrimitiveKind: IrPrimitiveKind
│       ├── String      → TS: string     | C#: string     | PHP: string    | Python: str
│       ├── Number      → TS: number     | C#: double      | PHP: float     | Python: float
│       ├── Integer     → TS: number     | C#: int         | PHP: int       | Python: int
│       ├── Long        → TS: bigint     | C#: long        | PHP: int       | Python: int
│       ├── Boolean     → TS: boolean    | C#: bool        | PHP: bool      | Python: bool
│       ├── DateTime    → TS: string     | C#: DateTime    | PHP: \DateTime | Python: datetime
│       ├── Date        → TS: string     | C#: DateOnly    | PHP: \DateTime | Python: date
│       ├── Time        → TS: string     | C#: TimeOnly    | PHP: \DateTime | Python: time
│       ├── Binary      → TS: Blob       | C#: byte[]      | PHP: string    | Python: bytes
│       ├── ByteArray   → TS: string     | C#: byte[]      | PHP: string    | Python: bytes
│       ├── Uuid        → TS: string     | C#: Guid        | PHP: string    | Python: UUID
│       ├── Any         → TS: unknown    | C#: object      | PHP: mixed     | Python: Any
│       ├── Void        → TS: void       | C#: void        | PHP: void      | Python: None
│       └── File        → TS: File       | C#: IFormFile   | PHP: UploadFile | Python: bytes
│
├── IrModelReference
│   └── ModelName: string   → TS: ModelName | C#: ModelName | etc.
│
├── IrArrayType
│   └── ElementType: IrType → TS: T[]  | C#: List<T>  | PHP: T[]  | Python: list[T]
│
├── IrDictionaryType
│   ├── KeyType: IrType     → всегда string в OpenAPI
│   └── ValueType: IrType   → TS: Record<K,V> | C#: Dictionary<K,V> | PHP: array | Python: dict[K,V]
│
├── IrNullableType
│   └── InnerType: IrType   → TS: T | null | C#: T? | PHP: ?T | Python: Optional[T]
│
├── IrOptionalType
│   └── InnerType: IrType   → TS: T (свойство с ?) | C#: T? | PHP: ?T
│
├── IrUnionType
│   └── Types: IReadOnlyList<IrType>  → TS: A | B | C | C#: — | PHP: — | Python: Union[A, B]
│
└── IrIntersectionType
    └── Types: IReadOnlyList<IrType>  → TS: A & B | C#: наследование | PHP: —
```

### Проблема: IrTypeKind — ещё один Kind-enum

```csharp
public abstract record IrType
{
    public abstract IrTypeKind Kind { get; }  // ← то же самое что AstNodeKind, только для IR
}
```

`IrTypeKind` используется для switch-dispatch при маппинге `IrType → AstTypeNode`. Это паттерн из старого мира (`switch(node.Kind)`), но для IR — **допустимо**, потому что:

1. Это внутренний switch только в `IIrTypeMapper` — не во visitor'е
2. `IrType` — record, не class в иерархии visitor'а
3. Альтернатива — pattern matching на тип записи (лучше):

```csharp
// Вместо switch(irType.Kind):
public TypeNode MapType(IrType irType) => irType switch
{
    IrPrimitiveType p    => MapPrimitive(p.PrimitiveKind),
    IrModelReference r   => new SimpleTypeNode(r.ModelName),
    IrArrayType a        => new ArrayTypeNode(MapType(a.ElementType)),
    IrDictionaryType d   => new DictionaryTypeNode(MapType(d.KeyType), MapType(d.ValueType)),
    IrNullableType n     => new NullableTypeNode(MapType(n.InnerType)),
    IrOptionalType o     => new OptionalTypeNode(MapType(o.InnerType)),
    IrUnionType u        => new UnionTypeNode(u.Types.Select(MapType).ToList()),
    IrIntersectionType i => MapIntersection(i),
    _                    => throw new NotSupportedException(irType.GetType().Name)
};
```

**Pattern matching на тип** безопаснее и понятнее switch на Kind — компилятор проверяет exhaustiveness. `IrTypeKind` можно убрать из `IrType` и перейти на `is`-pattern.

---

## 4. IrBuilder — алгоритм построения IR из OpenAPI

### Точка входа

```csharp
public IrDocument BuildFromOpenApi(OpenApiDocument document, ICollection<string>? warnings = null)
{
    _inlineModels.Clear();
    _inlineModelCounter = 0;

    var models  = ExtractModels(document);   // компоненты/схемы → IrModel[]
    var enums   = ExtractEnums(document);    // компоненты/схемы → IrEnum[]
    var clients = ExtractClients(document, warnings); // paths → IrClient[]

    // Inline модели (созданные в ходе ExtractModels при обнаружении inline объектов)
    var allModels = models.Concat(_inlineModels.Values).ToList();

    var typeDependencies = BuildTypeDependencyGraph(allModels, enums, clients);

    return new IrDocument { ... };
}
```

### ExtractModels — разбор component schemas

1. Итерация по `document.Components.Schemas`
2. Пропуск enum схем (они идут в `ExtractEnums`)
3. `BuildModel(name, schema, document)` → `IrModel`
4. `BuildModel`:
   - Если `allOf` — берём свойства из inline-схем (не из `$ref`), так как `$ref` — базовый класс
   - Иначе — `ExtractProperties(schema)`
   - `ExtractComposition(schema)` → `IrComposition?` (allOf/oneOf/anyOf)

### ExtractEnums — разбор enum схем

Enum идентифицируется как схема с непустым `schema.Enum` и типом string/integer без Properties.

```csharp
private IrEnum BuildEnum(string name, IOpenApiSchema schema)
{
    var values = schema.Enum
        .Select((v, i) => new IrEnumValue
        {
            Name = SanitizeEnumValue(v.ToString()),
            Value = v,
            Description = null  // OpenAPI редко даёт описания enum значений
        })
        .ToList();

    return new IrEnum
    {
        Name = name,
        Description = schema.Description,
        Values = values,
        IsStringEnum = schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.String)
    };
}
```

### ExtractClients — разбор paths → клиенты

Группировка операций по тегам:
```
/users GET (tags: ["Users"])     →  IrClient "Users"
/users POST (tags: ["Users"])    →    IrOperation "createUser"
/users/{id} GET (tags: ["Users"]) →  IrOperation "getUserById"
/products GET (tags: ["Products"]) → IrClient "Products"
/products POST (tags: ["Products"])→   IrOperation "createProduct"
```

Если тегов нет — один default клиент `"Api"`.

### BuildType — рекурсивное построение IrType из OpenAPI schema

Алгоритм (упрощённо):

```
1. Если schema — $ref → IrModelReference { ModelName = ref.Id }

2. Разрешить $ref через SchemaResolver

3. Если oneOf/anyOf → IrUnionType

4. Если allOf → IrIntersectionType

5. Если нет Type, но есть Properties → inline IrModel + IrModelReference

6. Если нет Type → IrPrimitiveType.Any

7. Если type содержит Null + X → IrNullableType(IrPrimitiveType(X))

8. Если type = array → IrArrayType { ElementType = BuildType(schema.Items) }

9. Если type = object + AdditionalProperties → IrDictionaryType

10. Если type = object + Properties → inline IrModel + IrModelReference

11. Если type = string/number/integer/boolean + format → IrPrimitiveType(kind)
```

---

## 5. Проблемы и улучшения IrBuilder

### Проблема 1: IrModel.OriginalSchema утечка OpenAPI в IR

```csharp
public sealed record IrModel
{
    ...
    public IOpenApiSchema? OriginalSchema { get; init; }  // ← нарушение: IR знает об OpenAPI
}
```

`OriginalSchema` нарушает изоляцию IR. IR должен быть полностью самодостаточным — никаких ссылок на OpenAPI после BuildFromOpenApi.

**Исправление:** Удалить `OriginalSchema` из `IrModel`. Если нужна отладочная информация — добавить `OriginalSchemaName: string?` (только имя, не сам объект).

### Проблема 2: Inline модели — конфликт имён

```csharp
private string GenerateInlineModelNameFromContext(PropertyContext? propertyContext)
{
    // Генерирует имена типа "ParentModelNamePropertyName"
    // Может дать дубли если несколько inline моделей у одного родителя
}
```

**Исправление:** Добавить детерминированный counter или путь к свойству как часть имени. Например: `UserDtoAddressDetail` вместо `UserDtoAddress1`.

### Проблема 3: TypeScriptAstBuilder всё ещё принимает TypeInfo

```csharp
// TypeScriptAstBuilder.cs — LEGACY
public TypeNode BuildType(TypeInfo typeInfo)
{
    if (typeInfo.IsCollection)
    {
        var elementType = BuildTypeFromBaseType(typeInfo.GenericArguments.FirstOrDefault() ?? ...);
        return new ArrayTypeNode(elementType, ...);
    }
    // ...
}
```

`TypeInfo` — legacy класс (см. `06_LEGACY_CLEANUP.md`). `TypeScriptAstBuilder` должен принимать `IrType`, не `TypeInfo`. Это ключевой migration gap.

**Исправление:** Заменить `BuildType(TypeInfo)` → `BuildType(IrType)` через `IIrTypeMapper`.

### Проблема 4: IrClient.Name — нет group/tag метаданных

```csharp
public sealed record IrClient
{
    public required string Name { get; init; }
    public required IReadOnlyList<IrOperation> Operations { get; init; }
    // нет: Tags, Description, BaseUrl
}
```

**Исправление:** Добавить:
```csharp
public sealed record IrClient
{
    public required string Name { get; init; }
    public required IReadOnlyList<IrOperation> Operations { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];    // теги OpenAPI
    public string? Description { get; init; }                 // описание группы
}
```

### Проблема 5: IrOperation не хранит operationId

`OperationId` из OpenAPI теряется после sanitization. Иногда нужен для TanStack Query key, для MSW handler name, для комментариев.

**Исправление:**
```csharp
public sealed record IrOperation
{
    public required string MethodName { get; init; }      // sanitized
    public string? OperationId { get; init; }             // оригинальный operationId из OpenAPI
    // ...
}
```

### Проблема 6: IrResponse — нет различия success/error ответов

```csharp
public sealed record IrResponse
{
    public required string StatusCode { get; init; }  // "200", "400", "default"
    public IrType? Type { get; init; }
}
```

AstBuilder вынужден сам парсить StatusCode чтобы понять, это success или error. Лучше добавить вычисляемые свойства:

```csharp
public sealed record IrResponse
{
    public required string StatusCode { get; init; }
    public IrType? Type { get; init; }
    public string? Description { get; init; }

    // Вычисляемые (не сериализуются, readonly)
    public bool IsSuccess => StatusCode.StartsWith("2") || StatusCode == "default";
    public bool IsError   => StatusCode.StartsWith("4") || StatusCode.StartsWith("5");
    public int? StatusCodeInt => int.TryParse(StatusCode, out var n) ? n : null;
}
```

### Проблема 7: IrPropertyConstraints не полная

```csharp
public class IrPropertyConstraints
{
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    // ...
    public object? DefaultValue { get; init; }
    // нет: MultipleOf, ExclusiveMin/Max, Enum constraints, Format
}
```

Для Zod, Valibot, TypeBox генерации нужны все constraints. Дополнить:

```csharp
public sealed record IrPropertyConstraints
{
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public decimal? ExclusiveMin { get; init; }
    public decimal? ExclusiveMax { get; init; }
    public decimal? MultipleOf { get; init; }    // ← добавить
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public string? Pattern { get; init; }
    public int? MinItems { get; init; }          // ← добавить (для array)
    public int? MaxItems { get; init; }          // ← добавить (для array)
    public bool? UniqueItems { get; init; }      // ← добавить (для array)
    public object? DefaultValue { get; init; }
    public string? Format { get; init; }         // ← добавить (email, uri, uuid, etc.)
}
```

### Проблема 8: IrBuilder — mutability через _inlineModels

```csharp
private readonly Dictionary<string, IrModel> _inlineModels = new();
private int _inlineModelCounter = 0;
```

`IrBuilder` — stateful через `_inlineModels`. Это означает, что один и тот же `IrBuilder` нельзя использовать параллельно. Каждый вызов `BuildFromOpenApi` очищает словарь — но это неочевидно.

**Исправление:** Сделать `IrBuilder` stateless — передавать `_inlineModels` как локальную переменную или через `BuildContext` record:

```csharp
private sealed class BuildContext
{
    public Dictionary<string, IrModel> InlineModels { get; } = new();
    public int InlineModelCounter { get; set; }
    public ICollection<string>? Warnings { get; init; }
}

public IrDocument BuildFromOpenApi(OpenApiDocument document, ICollection<string>? warnings = null)
{
    var ctx = new BuildContext { Warnings = warnings };
    var models  = ExtractModels(document, ctx);
    var enums   = ExtractEnums(document);
    var clients = ExtractClients(document, ctx);
    // ...
}
```

---

## 6. Конвертация IR → AST: общая схема

### Принцип разделения ответственности

```
IrDocument
    │
    ├─────────────────────────────────────────────────────────────────┐
    │                                                                 │
    ▼                                                                 ▼
TypeScriptAstBuilder                                        CSharpAstBuilder
    │                                                                 │
    ├── BuildModelsFile(IrModel[]) → SourceFileNode                  ├── BuildModelsFile(IrModel[]) → CompilationUnitNode
    ├── BuildEnumsFile(IrEnum[]) → SourceFileNode                    ├── BuildEnumsFile(IrEnum[]) → CompilationUnitNode
    ├── BuildClientFile(IrClient) → SourceFileNode                   └── BuildClientFile(IrClient) → CompilationUnitNode
    ├── BuildZodSchemasFile(IrModel[]) → SourceFileNode
    └── BuildTanStackHooksFile(IrClient) → SourceFileNode
```

### Контракт IAstBuilder (целевой)

```csharp
// ValIO.Core/Ast/Builders/IAstBuilder.cs
// Текущий IAstBuilder — пустой интерфейс. Нужно расширить.

public interface IAstBuilder<TRootNode> where TRootNode : AstNode
{
    /// <summary>
    /// Строит AST файл для набора моделей
    /// </summary>
    TRootNode BuildModelsFile(
        IReadOnlyList<IrModel> models,
        GenerationConfiguration config);

    /// <summary>
    /// Строит AST файл для набора enum'ов
    /// </summary>
    TRootNode BuildEnumsFile(
        IReadOnlyList<IrEnum> enums,
        GenerationConfiguration config);

    /// <summary>
    /// Строит AST файл для HTTP клиента
    /// </summary>
    TRootNode BuildClientFile(
        IrClient client,
        GenerationConfiguration config);
}

// TypeScript-специфичный builder
public interface ITypeScriptAstBuilder : IAstBuilder<SourceFileNode>
{
    SourceFileNode BuildZodSchemasFile(IReadOnlyList<IrModel> models, GenerationConfiguration config);
    SourceFileNode BuildTanStackHooksFile(IrClient client, GenerationConfiguration config);
    SourceFileNode BuildParamsFile(IrClient client, GenerationConfiguration config);
    // + методы для конкретных транспортов (Axios, Fetch, Ky, undici, ...)
}

// C#-специфичный builder
public interface ICSharpAstBuilder : IAstBuilder<CompilationUnitNode>
{
    CompilationUnitNode BuildServiceFile(IrClient client, GenerationConfiguration config);
    CompilationUnitNode BuildDtoFile(IReadOnlyList<IrModel> models, GenerationConfiguration config);
}
```

---

## 7. IrType → AstTypeNode: маппинг для каждого языка

Каждый AstBuilder использует `IIrTypeMapper` для конвертации `IrType → TypeNode`. Маппинг специфичен для каждого языка/стека.

### TypeScript маппинг

```csharp
public class TypeScriptIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.String }   => new SimpleTypeNode("string"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Number }   => new SimpleTypeNode("number"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Integer }  => new SimpleTypeNode("number"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Long }     => new SimpleTypeNode("bigint"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Boolean }  => new SimpleTypeNode("boolean"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.DateTime } => new SimpleTypeNode("string"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Date }     => new SimpleTypeNode("string"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Uuid }     => new SimpleTypeNode("string"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Binary }   => new SimpleTypeNode("Blob"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.File }     => new SimpleTypeNode("File"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Any }      => new RawTypeNode("unknown"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Void }     => new SimpleTypeNode("void"),
        
        IrModelReference r   => new SimpleTypeNode(r.ModelName),
        IrArrayType a        => new ArrayTypeNode(MapType(a.ElementType)),
        IrDictionaryType d   => new GenericTypeNode(new IdentifierNode("Record"),
                                    [MapType(d.KeyType), MapType(d.ValueType)]),
        IrNullableType n     => new UnionTypeNode([MapType(n.InnerType), new SimpleTypeNode("null")]),
        IrOptionalType o     => MapType(o.InnerType),  // optional выражается через PropertySignature.IsOptional
        IrUnionType u        => new UnionTypeNode(u.Types.Select(MapType).ToList()),
        IrIntersectionType i => new IntersectionTypeNode(i.Types.Select(MapType).ToList()),
        
        _ => throw new NotSupportedException($"IrType {irType.GetType().Name} not supported for TypeScript")
    };
}
```

### C# маппинг

```csharp
public class CSharpIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.String }   => new SimpleTypeNode("string"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Number }   => new SimpleTypeNode("double"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Integer }  => new SimpleTypeNode("int"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Long }     => new SimpleTypeNode("long"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Boolean }  => new SimpleTypeNode("bool"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.DateTime } => new SimpleTypeNode("DateTime"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Date }     => new SimpleTypeNode("DateOnly"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Time }     => new SimpleTypeNode("TimeOnly"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Uuid }     => new SimpleTypeNode("Guid"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Binary }   => new ArrayTypeNode(new SimpleTypeNode("byte")),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.File }     => new SimpleTypeNode("IFormFile"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Any }      => new SimpleTypeNode("object"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Void }     => new SimpleTypeNode("void"),
        
        IrModelReference r   => new SimpleTypeNode(r.ModelName),
        IrArrayType a        => new GenericTypeNode(new IdentifierNode("List"), [MapType(a.ElementType)]),
        IrDictionaryType d   => new GenericTypeNode(new IdentifierNode("Dictionary"),
                                    [MapType(d.KeyType), MapType(d.ValueType)]),
        IrNullableType n     => new NullableTypeNode(MapType(n.InnerType)),  // T?
        IrOptionalType o     => new NullableTypeNode(MapType(o.InnerType)),  // T? (optional = nullable в C#)
        IrUnionType u        => new SimpleTypeNode("object"),  // C# не поддерживает union — fallback
        IrIntersectionType i => MapIntersectionToCSharp(i),   // → наследование или partial
        
        _ => throw new NotSupportedException($"IrType {irType.GetType().Name} not supported for C#")
    };
}
```

### PHP маппинг

```csharp
public class PhpIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.String }   => new SimpleTypeNode("string"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Number }   => new SimpleTypeNode("float"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Integer }  => new SimpleTypeNode("int"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Long }     => new SimpleTypeNode("int"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Boolean }  => new SimpleTypeNode("bool"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.DateTime } => new SimpleTypeNode("\\DateTime"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Uuid }     => new SimpleTypeNode("string"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Any }      => new SimpleTypeNode("mixed"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Void }     => new SimpleTypeNode("void"),
        
        IrModelReference r   => new SimpleTypeNode(r.ModelName),
        IrArrayType _        => new SimpleTypeNode("array"),   // PHP не имеет типизированных массивов в type hints
        IrDictionaryType _   => new SimpleTypeNode("array"),
        IrNullableType n     => new RawTypeNode($"?{MapType(n.InnerType)}"),  // ?Type
        IrOptionalType o     => new RawTypeNode($"?{MapType(o.InnerType)}"),
        IrUnionType u        => new RawTypeNode(string.Join("|", u.Types.Select(MapType).Select(t => t.Accept(renderer)))),
        
        _ => throw new NotSupportedException($"IrType {irType.GetType().Name} not supported for PHP")
    };
}
```

### Python маппинг

```csharp
public class PythonIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.String }   => new SimpleTypeNode("str"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Number }   => new SimpleTypeNode("float"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Integer }  => new SimpleTypeNode("int"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Boolean }  => new SimpleTypeNode("bool"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.DateTime } => new SimpleTypeNode("datetime"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Uuid }     => new SimpleTypeNode("UUID"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Binary }   => new SimpleTypeNode("bytes"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Any }      => new SimpleTypeNode("Any"),
        IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Void }     => new SimpleTypeNode("None"),
        
        IrModelReference r   => new SimpleTypeNode(r.ModelName),
        IrArrayType a        => new GenericTypeNode(new IdentifierNode("list"), [MapType(a.ElementType)]),
        IrDictionaryType d   => new GenericTypeNode(new IdentifierNode("dict"),
                                    [MapType(d.KeyType), MapType(d.ValueType)]),
        IrNullableType n     => new GenericTypeNode(new IdentifierNode("Optional"), [MapType(n.InnerType)]),
        IrOptionalType o     => new GenericTypeNode(new IdentifierNode("Optional"), [MapType(o.InnerType)]),
        IrUnionType u        => new GenericTypeNode(new IdentifierNode("Union"),
                                    u.Types.Select(MapType).ToList()),
        
        _ => throw new NotSupportedException($"IrType {irType.GetType().Name} not supported for Python")
    };
}
```

---

## 8. IrModel → AST: алгоритм построения деклараций

### TypeScript: IrModel → InterfaceDeclarationNode

```csharp
// TypeScriptAstBuilder
private InterfaceDeclarationNode BuildModelInterface(IrModel model)
{
    var properties = model.Properties
        .Select(p => BuildPropertySignature(p))
        .ToList();

    // Если есть composition (allOf) — extends
    var baseTypes = model.Composition?.Kind == IrCompositionKind.AllOf
        ? model.Composition.Types
              .OfType<IrModelReference>()
              .Select(r => (TypeNode)new SimpleTypeNode(r.ModelName))
              .ToList()
        : null;

    return new InterfaceDeclarationNode(
        name: new IdentifierNode(model.Name),
        properties: properties,
        extends: baseTypes,
        documentation: BuildComment(model.Description));
}

private PropertySignatureNode BuildPropertySignature(IrProperty property)
{
    var type = _typeMapper.MapType(property.Type);
    
    // Если optional — свойство помечается как optional, не тип
    var isOptional = !property.IsRequired || property.Type is IrOptionalType;
    
    return new PropertySignatureNode(
        name: new IdentifierNode(property.Name),
        type: type,
        isOptional: isOptional,
        documentation: BuildComment(property.Description));
}
```

### C#: IrModel → RecordDeclarationNode или ClassDeclarationNode

```csharp
// CSharpAstBuilder
private DeclarationNode BuildModelRecord(IrModel model)
{
    var properties = model.Properties
        .Select(p => BuildPropertyDeclaration(p))
        .ToList();

    // allOf → BaseList (наследование)
    var baseType = model.Composition?.Kind == IrCompositionKind.AllOf
        ? model.Composition.Types
              .OfType<IrModelReference>()
              .FirstOrDefault()
              ?.ModelName
        : null;

    return new RecordDeclarationNode(
        name: new IdentifierNode(model.Name),
        properties: properties,
        baseType: baseType != null ? new SimpleTypeNode(baseType) : null,
        attributes: new[] { BuildJsonSerializableAttribute() },
        documentation: BuildComment(model.Description));
}

private PropertyDeclarationNode BuildPropertyDeclaration(IrProperty property)
{
    var type = _typeMapper.MapType(property.Type);

    // JSON serialization attribute для snake_case → PascalCase маппинга
    var jsonPropertyAttr = property.OriginalName != property.Name
        ? new AttributeNode("JsonPropertyName", new LiteralExpressionNode(property.OriginalName, LiteralKind.String))
        : null;

    return new PropertyDeclarationNode(
        name: new IdentifierNode(property.Name),
        type: type,
        accessModifiers: AccessModifier.Public,
        getter: true,
        setter: true,           // в record: init
        isRequired: property.IsRequired,
        attributes: jsonPropertyAttr != null ? [jsonPropertyAttr] : [],
        documentation: BuildComment(property.Description));
}
```

---

## 9. IrOperation → AST: алгоритм построения HTTP методов

### Общий алгоритм

```
IrOperation
    │
    ├── signature: (path params + query params + body) → parameters list
    ├── return type: success response type → TypeNode
    ├── body:
    │     ├── URL building (path params → template string)
    │     ├── Query string building (query params → object / URLSearchParams)
    │     ├── HTTP call (axios.get/post/... или fetch)
    │     └── Response handling (data extraction)
    └── documentation: summary + description
```

### TypeScript Axios: IrOperation → MethodDeclarationNode

```csharp
private MethodDeclarationNode BuildAxiosOperation(IrOperation op)
{
    // 1. Параметры метода
    var parameters = BuildOperationParameters(op);

    // 2. Тип возврата
    var successResponse = op.Responses.FirstOrDefault(r => r.IsSuccess);
    var returnType = successResponse?.Type != null
        ? new GenericTypeNode(new IdentifierNode("Promise"), [_typeMapper.MapType(successResponse.Type)])
        : new GenericTypeNode(new IdentifierNode("Promise"), [new SimpleTypeNode("void")]);

    // 3. URL expression
    // "/api/users/{id}" + pathParams → `\api\users\${id}`
    var urlExpr = BuildUrlExpression(op.Path, op.PathParameters);

    // 4. Config object (query params, headers)
    var configExpr = BuildAxiosConfig(op.QueryParameters, op.HeaderParameters);

    // 5. HTTP call
    var httpCall = BuildAxiosCall(op.HttpMethod, urlExpr, op.RequestBody, configExpr, successResponse?.Type);

    // 6. Async function body
    var body = new BlockNode([new ReturnStatementNode(httpCall)]);

    return new MethodDeclarationNode(
        name: new IdentifierNode(op.MethodName),
        parameters: parameters,
        returnType: returnType,
        body: body,
        isAsync: true,
        isExported: true,
        documentation: BuildOperationComment(op));
}
```

### TypeScript Fetch: IrOperation → MethodDeclarationNode

Отличие от Axios — нет `axios.get(url, config)`, вместо этого `fetch(url, { method, headers, body })`:

```csharp
private ExpressionNode BuildFetchCall(IrOperation op, ExpressionNode urlExpr)
{
    var method = new LiteralExpressionNode(op.HttpMethod, LiteralKind.String);
    var options = new ObjectLiteralExpressionNode([
        new PropertyAssignmentNode("method", method),
        ...(op.RequestBody != null
            ? [new PropertyAssignmentNode("body", new CallExpressionNode(
                new MemberAccessExpressionNode(new IdentifierExpressionNode("JSON"), new IdentifierNode("stringify")),
                [new IdentifierExpressionNode("body")]))]
            : []),
        new PropertyAssignmentNode("headers", new ObjectLiteralExpressionNode([
            new PropertyAssignmentNode("Content-Type", new LiteralExpressionNode("application/json", LiteralKind.String))
        ]))
    ]);

    return new CallExpressionNode(
        new IdentifierExpressionNode("fetch"),
        [urlExpr, options]);
}
```

### TanStack Query: IrOperation → UseQuery/UseMutation hook

```csharp
private FunctionDeclarationNode BuildTanStackQueryHook(IrOperation op)
{
    var isQuery    = op.HttpMethod == "GET";
    var isMutation = op.HttpMethod is "POST" or "PUT" or "DELETE" or "PATCH";

    if (isQuery) return BuildUseQueryHook(op);
    else         return BuildUseMutationHook(op);
}

private FunctionDeclarationNode BuildUseQueryHook(IrOperation op)
{
    // export function useGetUser(id: string) {
    //   return useQuery({
    //     queryKey: ['getUser', id],
    //     queryFn: () => getUser(id),
    //   });
    // }
    var hookName = $"use{char.ToUpper(op.MethodName[0])}{op.MethodName.Substring(1)}";
    var parameters = BuildOperationParameters(op);

    var queryKey = new ArrayLiteralExpressionNode([
        new LiteralExpressionNode(op.MethodName, LiteralKind.String),
        ..parameters.Select(p => new IdentifierExpressionNode(p.Name.Name))
    ]);

    var queryFnArgs = parameters.Select(p => new IdentifierExpressionNode(p.Name.Name)).ToList();
    var queryFn = new ArrowFunctionExpressionNode(
        parameters: [],
        body: new CallExpressionNode(
            new IdentifierExpressionNode(op.MethodName),
            queryFnArgs));

    var useQueryCall = new CallExpressionNode(
        new IdentifierExpressionNode("useQuery"),
        [new ObjectLiteralExpressionNode([
            new PropertyAssignmentNode("queryKey", queryKey),
            new PropertyAssignmentNode("queryFn", queryFn)
        ])]);

    return new FunctionDeclarationNode(
        name: new IdentifierNode(hookName),
        parameters: parameters,
        returnType: null,   // TypeScript inferit
        body: new BlockNode([new ReturnStatementNode(useQueryCall)]),
        isExported: true);
}
```

---

## 10. IrEnum → AST: алгоритм построения enum'ов

### TypeScript: IrEnum → EnumDeclarationNode

```csharp
private EnumDeclarationNode BuildTypeScriptEnum(IrEnum irEnum)
{
    var members = irEnum.Values.Select(v => new EnumMemberDeclarationNode(
        name: new IdentifierNode(v.Name),
        value: v.Value switch
        {
            string s => new LiteralExpressionNode(s, LiteralKind.String),
            int i    => new LiteralExpressionNode(i, LiteralKind.Number),
            _        => new LiteralExpressionNode(v.Value.ToString()!, LiteralKind.String)
        },
        documentation: BuildComment(v.Description)
    )).ToList();

    return new EnumDeclarationNode(
        name: new IdentifierNode(irEnum.Name),
        members: members,
        isConst: true,  // const enum в TS — более эффективно
        isExported: true,
        documentation: BuildComment(irEnum.Description));
}
```

### TypeScript: IrEnum → TypeAliasDeclarationNode (union type)

Иногда предпочтительнее `type Status = "active" | "inactive"` вместо `const enum`:

```csharp
private TypeAliasDeclarationNode BuildTypeScriptEnumAsUnion(IrEnum irEnum)
{
    if (!irEnum.IsStringEnum)
        // Числовые enum'ы лучше как const enum
        return null!;

    var unionTypes = irEnum.Values
        .Select(v => (TypeNode)new RawTypeNode($"\"{v.Value}\""))
        .ToList();

    return new TypeAliasDeclarationNode(
        name: new IdentifierNode(irEnum.Name),
        type: new UnionTypeNode(unionTypes),
        isExported: true,
        documentation: BuildComment(irEnum.Description));
}
```

### C#: IrEnum → EnumDeclarationNode

```csharp
private EnumDeclarationNode BuildCSharpEnum(IrEnum irEnum)
{
    var members = irEnum.Values.Select((v, i) => new EnumMemberDeclarationNode(
        name: new IdentifierNode(v.Name),
        value: irEnum.IsStringEnum
            ? null   // string enum в C# не имеет числовых значений
            : v.Value is int intVal
                ? new LiteralExpressionNode(intVal, LiteralKind.Number)
                : new LiteralExpressionNode(i, LiteralKind.Number),  // fallback: порядковый номер
        documentation: BuildComment(v.Description)
    )).ToList();

    // JsonConverter для string enum'ов
    var attributes = irEnum.IsStringEnum
        ? new[] { new AttributeNode("JsonConverter", new CallExpressionNode(
            new IdentifierExpressionNode("typeof"),
            [new IdentifierExpressionNode("JsonStringEnumConverter")])) }
        : Array.Empty<AttributeNode>();

    return new EnumDeclarationNode(
        name: new IdentifierNode(irEnum.Name),
        members: members,
        accessModifier: AccessModifier.Public,
        attributes: attributes,
        documentation: BuildComment(irEnum.Description));
}
```

---

## 11. IrTypeDependencyGraph — порядок генерации и импорты

### Что это и зачем

```csharp
public sealed record IrTypeDependencyGraph
{
    // "UserDto" → ["AddressDto", "RoleDto"]   (UserDto зависит от AddressDto и RoleDto)
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Dependencies { get; init; }

    // Топологический порядок: сначала AddressDto, RoleDto, потом UserDto
    public required IReadOnlyList<string> TopologicalOrder { get; init; }
}
```

`IrTypeDependencyGraph` решает два класса задач:

**1. Порядок генерации** — чтобы при генерации в один файл базовые типы были объявлены раньше производных:
```typescript
interface AddressDto { ... }  // сначала — нет зависимостей
interface RoleDto { ... }     // сначала — нет зависимостей
interface UserDto {           // потом — зависит от AddressDto и RoleDto
  address: AddressDto;
  role: RoleDto;
}
```

**2. Порядок импортов** — при разбитии по файлам:
```typescript
// user.ts
import type { AddressDto } from './address';  // ← нужен импорт
import type { RoleDto }    from './role';     // ← нужен импорт

export interface UserDto { ... }
```

### Как используется в AstBuilder

```csharp
// При генерации одного файла с несколькими моделями
private SourceFileNode BuildModelsFile(IrDocument ir, FileOrganization org)
{
    // Сортировка по топологическому порядку
    var orderedModels = ir.TypeDependencies.TopologicalOrder
        .Select(name => ir.Models.FirstOrDefault(m => m.Name == name))
        .Where(m => m != null)
        .Cast<IrModel>()
        .ToList();

    var declarations = orderedModels.Select(m => BuildModelInterface(m)).ToList();
    var imports      = BuildImports(ir.TypeDependencies.Dependencies);

    return new SourceFileNode(declarations: declarations, imports: imports);
}
```

### Что улучшить в IrTypeDependencyGraph

1. **Добавить информацию об enum зависимостях:** Сейчас граф включает только модели. Enum'ы тоже могут быть зависимостями.

2. **Добавить различие import/composition зависимостей:** Нужно знать, зависимость — для `import` или для `extends`:
```csharp
public sealed record IrTypeDependency
{
    public string TargetTypeName { get; init; }
    public IrDependencyKind Kind { get; init; }  // Import | Extends | Implements
}
```

3. **Поддержка circular dependencies:** Текущий топологический sort не обрабатывает циклы. Нужно обнаружение и отчёт об ошибке.

---

## 12. Что добавить в IR (недостающие сущности)

### IrEnum — добавить числовые значения

```csharp
// Текущий IrEnumValue
public sealed record IrEnumValue
{
    public required string Name { get; init; }
    public required object Value { get; init; }
    public string? Description { get; init; }
    // нет: IntValue отдельно от StringValue
}

// Улучшенный IrEnumValue
public sealed record IrEnumValue
{
    public required string Name { get; init; }   // sanitized, PascalCase
    public required string OriginalName { get; init; }  // из OpenAPI
    public string? StringValue { get; init; }    // если string enum
    public int? IntValue { get; init; }          // если integer enum
    public string? Description { get; init; }
}
```

### IrModel — добавить IsAbstract и AdditionalProperties

```csharp
public sealed record IrModel
{
    // ... существующие поля ...
    public bool IsAbstract { get; init; }    // для абстрактных моделей (discriminated union base)
    public IrType? AdditionalProperties { get; init; }  // схема для дополнительных свойств
    public bool AllowsAdditionalProperties { get; init; }  // additionalProperties: true/false
}
```

### IrOperation — добавить Deprecated и Security

```csharp
public sealed record IrOperation
{
    // ... существующие поля ...
    public bool IsDeprecated { get; init; }
    public string? DeprecationMessage { get; init; }
    public IReadOnlyList<IrSecurityRequirement>? Security { get; init; }
    public string? OperationId { get; init; }   // оригинальный operationId
}

public sealed record IrSecurityRequirement
{
    public required string SchemeName { get; init; }  // "BearerAuth", "ApiKey"
    public IReadOnlyList<string> Scopes { get; init; } = [];
}
```

### IrDocument — добавить SecuritySchemes

```csharp
public sealed record IrDocument
{
    // ... существующие поля ...
    public IReadOnlyDictionary<string, IrSecurityScheme> SecuritySchemes { get; init; } = new Dictionary<string, IrSecurityScheme>();
}

public sealed record IrSecurityScheme
{
    public required string Name { get; init; }
    public required IrSecuritySchemeKind Kind { get; init; }  // Bearer | ApiKey | OAuth2 | OpenIdConnect | Basic
    public string? In { get; init; }   // header | query | cookie (для ApiKey)
    public string? Scheme { get; init; }  // "bearer" (для http)
}
```

---

## 13. Целевой IAstBuilder контракт

### Проблема текущего IAstBuilder

```csharp
// ValIO.Core/Ast/Builders/IAstBuilder.cs — ПУСТОЙ
public interface IAstBuilder
{
    // ничего нет
}
```

Пустой marker interface не даёт никакого контракта. Все конкретные builders типизированы отдельно (`ITypeScriptAstBuilder`, `ICSharpAstBuilder`) но без общего базового контракта.

### Целевая иерархия

```csharp
// ValIO.Core/Ast/Builders/IAstBuilder.cs
public interface IAstBuilder<out TFileNode> where TFileNode : AstNode
{
    /// <summary>
    /// Строит файл(-ы) моделей из IR
    /// </summary>
    IReadOnlyList<TFileNode> BuildModelFiles(
        IReadOnlyList<IrModel> models,
        IrTypeDependencyGraph dependencies,
        GenerationConfiguration config);

    /// <summary>
    /// Строит файл(-ы) enum'ов из IR
    /// </summary>
    IReadOnlyList<TFileNode> BuildEnumFiles(
        IReadOnlyList<IrEnum> enums,
        GenerationConfiguration config);

    /// <summary>
    /// Строит файл(-ы) HTTP клиентов из IR
    /// </summary>
    IReadOnlyList<TFileNode> BuildClientFiles(
        IReadOnlyList<IrClient> clients,
        IrTypeDependencyGraph dependencies,
        GenerationConfiguration config);
}

// TypeScript-специфичный builder (в ValIO.Generators.TypeScript)
public interface ITypeScriptAstBuilder : IAstBuilder<SourceFileNode>
{
    // Дополнительные методы для TS-специфичных стеков
    IReadOnlyList<SourceFileNode> BuildZodSchemaFiles(
        IReadOnlyList<IrModel> models,
        IReadOnlyList<IrEnum> enums,
        GenerationConfiguration config);

    IReadOnlyList<SourceFileNode> BuildTanStackHookFiles(
        IReadOnlyList<IrClient> clients,
        GenerationConfiguration config);

    IReadOnlyList<SourceFileNode> BuildParamsFiles(
        IReadOnlyList<IrClient> clients,
        GenerationConfiguration config);
}

// C#-специфичный builder (в ValIO.Generators.CSharp)
public interface ICSharpAstBuilder : IAstBuilder<CompilationUnitNode>
{
    IReadOnlyList<CompilationUnitNode> BuildServiceFiles(
        IReadOnlyList<IrClient> clients,
        GenerationConfiguration config);
}
```

### Правила реализации IAstBuilder

1. **Builder не рендерит** — возвращает AST ноды, не строки. Рендерингом занимается `AstRenderer`.
2. **Builder не знает о файловой системе** — не пишет файлы. Это задача `ICodeGenerationStep`.
3. **Builder не парсит OpenAPI** — принимает только IR. `IrBuilder` уже разрешил всё.
4. **Builder использует `IIrTypeMapper`** — не маппит типы напрямую.
5. **Builder immutable** — не хранит state между вызовами. Конфигурация через параметры.
6. **Builder создаёт полные узлы** — включая импорты, документацию, атрибуты.
