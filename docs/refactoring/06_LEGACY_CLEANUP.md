# 06 — Полный каталог legacy-кода под удаление

> **Этап:** 0 (выполняется параллельно с 01 и 02)  
> **Приоритет:** Критический  
> **Зависит от:** 01_AST_REFACTORING (нельзя удалять интерфейсы пока не заменены их реализации)

---

## Содержание

1. [Общий принцип и порядок удаления](#1-общий-принцип-и-порядок-удаления)
2. [TemplateData система — полное удаление](#2-templatedata-система--полное-удаление)
3. [Устаревшие интерфейсы ValIO.Core/Interfaces](#3-устаревшие-интерфейсы-valiocoreinterfaces)
4. [Устаревшие сервисы ValIO.Core/Services](#4-устаревшие-сервисы-valiocoreservices)
5. [Устаревшие модели ValIO.Core/Models](#5-устаревшие-модели-valiocoremodels)
6. [Устаревшие фабрики ValIO/Factories](#6-устаревшие-фабрики-valiofactories)
7. [Устаревшие AST-слои](#7-устаревшие-ast-слои)
8. [Устаревшие проекты и файлы вне основного кода](#8-устаревшие-проекты-и-файлы-вне-основного-кода)
9. [Перечисления под замену](#9-перечисления-под-замену)
10. [BaseCodeGenerator и ICodeGenerator — реструктуризация](#10-basecodeгenerator-и-icodegenerator--реструктуризация)
11. [Сводная таблица всех удалений](#11-сводная-таблица-всех-удалений)

---

## 1. Общий принцип и порядок удаления

### Почему legacy не удалено сразу

В ValIO.Core существует два параллельных мира:

**Мир 1 (старый, TemplateData):** `OpenAPI → Extractors → TemplateData → Renderers → string`  
**Мир 2 (новый, IR/AST):** `OpenAPI → IrBuilder → IR → AstBuilder → AST → AstRenderer → string`

Весь рабочий код уже перешёл на Мир 2. Мир 1 существует только в виде мёртвого кода — интерфейсы, классы, перечисления, которые больше не используются ни в одном рабочем пути. Комментарии в `BaseCodeGenerator.cs` прямо говорят: «методы удалены — они использовались только в старом TemplateData подходе».

### Порядок удаления (важен для компилятора)

```
Шаг 1: Удалить реализации (Services, конкретные классы)
Шаг 2: Удалить базовые классы (BaseXxx)
Шаг 3: Удалить интерфейсы (IXxx)
Шаг 4: Удалить модели TemplateData
Шаг 5: Удалить перечисления (GenerationLanguage, ValidationLibrary, QueryWrapperLibrary)
Шаг 6: Удалить фабрики
Шаг 7: Удалить BaseCodeGenerator и ICodeGenerator (заменить на pipeline)
```

Нельзя удалять интерфейсы раньше их реализаций — компилятор укажет на ошибки.

---

## 2. TemplateData система — полное удаление

Это самый большой блок legacy. Вся папка `ValIO.Core/Models/TemplateData/` удаляется целиком.

### Файлы TemplateData моделей

**`ValIO.Core/Models/TemplateData/ModelTemplateData.cs`**
```csharp
// Содержит:
public class ModelTemplateData<TOptions, TPropertyOptions>  // legacy
public class PropertyTemplateData<TOptions>                 // legacy
public enum ModelFormat { Class, Record, Interface, TypeAlias, Struct }  // legacy
```
→ **Удалить.** Модели теперь представлены через `IrModel` → `TypeDeclarationNode` / `InterfaceDeclarationNode`.

**`ValIO.Core/Models/TemplateData/OperationTemplateData.cs`**
```csharp
public class OperationTemplateData<TOptions>  // legacy
// Содержит: MethodName, Path, HttpMethod, Parameters (список TypeInfo), ReturnType (TypeInfo)
```
→ **Удалить.** Операции представлены через `IrOperation` → методы в `MethodDeclarationNode`.

**`ValIO.Core/Models/TemplateData/ClientTemplateData.cs`**
```csharp
public class ClientTemplateData<TOptions>  // legacy
// Содержит: ClientName, Operations (список OperationTemplateData)
```
→ **Удалить.** Клиенты представлены через `IrClient` → `ClassDeclarationNode` / файл с функциями.

**`ValIO.Core/Models/TemplateData/EnumTemplateData.cs`**
```csharp
public class EnumTemplateData<TOptions>  // legacy
// Содержит: Name, Values (список строк или пар ключ-значение)
```
→ **Удалить.** Enum'ы представлены через `IrEnum` → `EnumDeclarationNode`.

### Файлы опций TemplateData

Вся папка `ValIO.Core/Models/TemplateData/Options/`:

| Файл | Удалить |
|------|---------|
| `ModelTemplateDataOptions.cs` | да |
| `PropertyTemplateDataOptions.cs` | да |
| `OperationTemplateDataOptions.cs` | да |
| `ClientTemplateDataOptions.cs` | да |
| `EnumTemplateDataOptions.cs` | да |
| `ITemplateDataOptions.cs` | да |

Все эти классы — language-specific опции для TemplateData рендереров. После перехода на AST-рендереры они не нужны.

### Итого по TemplateData

```
Удалить целиком:
ValIO.Core/
  Models/
    TemplateData/
      ModelTemplateData.cs
      OperationTemplateData.cs
      ClientTemplateData.cs
      EnumTemplateData.cs
      Options/
        ModelTemplateDataOptions.cs
        PropertyTemplateDataOptions.cs
        OperationTemplateDataOptions.cs
        ClientTemplateDataOptions.cs
        EnumTemplateDataOptions.cs
        ITemplateDataOptions.cs
```

---

## 3. Устаревшие интерфейсы ValIO.Core/Interfaces

Полный список интерфейсов под удаление с объяснением почему:

### IClientRenderer.cs — удалить

```csharp
public interface IClientRenderer
{
    string Render(IClientTemplateData data);  // TemplateData — legacy
}
```
**Почему:** Рендеринг HTTP клиентов теперь через `IrClient → AST → AstRenderer`. Никакой `IClientTemplateData` больше нет.

### IModelRenderer.cs — удалить

```csharp
public interface IModelRenderer
{
    string Render(ModelTemplateData data);  // TemplateData — legacy
}
```
**Почему:** Модели рендерятся через `IrModel → TypeDeclarationNode → TypeScriptAstRenderer/CSharpAstRenderer`.

### IEnumRenderer.cs — удалить

```csharp
public interface IEnumRenderer
{
    string Render(EnumTemplateData data);  // TemplateData — legacy
}
```
**Почему:** Enum'ы рендерятся через `IrEnum → EnumDeclarationNode → AstRenderer`.

### IModelFormatRenderer.cs — удалить

```csharp
public interface IModelFormatRenderer
{
    string Render(ModelTemplateData data, ModelFormat format, GenerationLanguage language);
    // ModelFormat — legacy enum; GenerationLanguage — legacy enum
}
```
**Почему:** `ModelFormat` и `GenerationLanguage` удаляются. Формат модели определяется настройкой плагина.

### IEnumFormatRenderer.cs — удалить

```csharp
public interface IEnumFormatRenderer
{
    string Render(EnumTemplateData data, EnumFormat format, GenerationLanguage language);
}
```
**Почему:** `EnumFormat` и `GenerationLanguage` удаляются.

### IOperationRenderer.cs — удалить

```csharp
public interface IOperationRenderer
{
    string Render(OperationTemplateData data);  // TemplateData — legacy
}
```
**Почему:** Операции рендерятся через `IrOperation → MethodDeclarationNode → AstRenderer`.

### IQueryWrapperRenderer.cs — удалить

```csharp
public interface IQueryWrapperRenderer
{
    string Render(OperationTemplateData data, QueryWrapperLibrary library, GenerationLanguage language);
    // QueryWrapperLibrary — legacy enum
}
```
**Почему:** TanStack Query и другие query wrappers теперь реализованы как отдельные `ICodeGenerationStep` через plugin-архитектуру.

### IDependencyAnalyzer.cs — удалить

```csharp
public interface IDependencyAnalyzer
{
    IReadOnlySet<string> GetModelDependencies(ModelTemplateData data);
    IReadOnlySet<string> GetOperationDependencies(OperationTemplateData data);
}
```
**Почему:** Граф зависимостей теперь строится на уровне IR через `IrTypeDependencyGraph` в `IrBuilder`. `BaseDependencyAnalyzer` и его реализации удаляются.

### IEnumExtractor.cs — удалить

```csharp
public interface IEnumExtractor
{
    IReadOnlyList<EnumTemplateData> ExtractEnums(IOpenApiSchema schema, ISchemaResolver resolver);
}
```
**Почему:** Enum'ы извлекаются в `IrBuilder.ExtractEnums()` на уровне IR.

### IModelExtractor.cs — удалить

```csharp
public interface IModelExtractor
{
    IReadOnlyList<ModelTemplateData> ExtractModels(OpenApiDocument document);
}
```
**Почему:** Модели извлекаются в `IrBuilder.ExtractModels()`.

### IOperationExtractor.cs — удалить

```csharp
public interface IOperationExtractor
{
    IReadOnlyList<OperationTemplateData> ExtractOperations(OpenApiDocument document);
}
```
**Почему:** Операции извлекаются в `IrBuilder.ExtractClients()` → `IrOperation`.

### ITypeInfoBuilder.cs — удалить

```csharp
public interface ITypeInfoBuilder
{
    TypeInfo BuildTypeInfo(IOpenApiSchema schema, ISchemaResolver resolver);
    TypeInfo BuildPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver resolver);
}
```
**Почему:** `TypeInfo` — legacy модель. Типы теперь строятся в `IrBuilder.BuildType()` → `IrType`. `ITypeInfoBuilder` и его реализация `BaseTypeInfoBuilder` удаляются.

### IClientTemplateData.cs, IModelTemplateData.cs, IEnumTemplateData.cs, IOperationTemplateData.cs — удалить

```csharp
public interface IClientTemplateData   { string ClientName { get; }; GenerationLanguage Language { get; }; ... }
public interface IModelTemplateData    { string Name { get; }; ... }
public interface IEnumTemplateData     { string Name { get; }; ... }
public interface IOperationTemplateData { string MethodName { get; }; ... }
```
**Почему:** TemplateData интерфейсы — основа старого pipeline. Полностью заменены IR-записями.

### ITypeMapper.cs — переработать, не удалять

```csharp
public interface ITypeMapper
{
    string MapType(IOpenApiSchema schema, ISchemaResolver schemaResolver);        // LEGACY — работает с OpenAPI
    string MapPropertyType(IOpenApiSchema schema, bool isRequired, ...);          // LEGACY
    string MapEnumType(IOpenApiSchema schema, ISchemaResolver schemaResolver);    // LEGACY
    TypeInfo MapTypeInfo(IOpenApiSchema schema, ...);                             // LEGACY — возвращает TypeInfo
    TypeInfo MapPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ...);   // LEGACY
}
```

**Проблема:** `ITypeMapper` работает напрямую с `IOpenApiSchema` — это обход IR и нарушает pipeline. Кроме того, возвращает `TypeInfo` — legacy модель.

**Правильный контракт после рефакторинга:**
```csharp
// ValIO.Core/Interfaces/IIrTypeMapper.cs — НОВЫЙ
public interface IIrTypeMapper
{
    /// <summary>
    /// Маппит IrType в AST TypeNode для конкретного языка/стека.
    /// Каждый генератор реализует свой маппер.
    /// </summary>
    TypeNode MapType(IrType irType);
    TypeNode MapPropertyType(IrType irType, bool isRequired);
}

// Пример реализации для TypeScript:
public class TypeScriptIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p  => MapPrimitive(p),
        IrArrayType a      => new ArrayTypeNode(MapType(a.ElementType)),
        IrDictionaryType d => new DictionaryTypeNode(MapType(d.KeyType), MapType(d.ValueType)),
        IrNullableType n   => new NullableTypeNode(MapType(n.InnerType)),
        IrOptionalType o   => new OptionalTypeNode(MapType(o.InnerType)),
        IrUnionType u      => new UnionTypeNode(u.Types.Select(MapType).ToImmutableList()),
        IrModelReference r => new SimpleTypeNode(r.ModelName),
        _ => throw new NotSupportedException($"IrType {irType.GetType().Name} not supported")
    };

    private TypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String   => new SimpleTypeNode("string"),
        IrPrimitiveKind.Number   => new SimpleTypeNode("number"),
        IrPrimitiveKind.Integer  => new SimpleTypeNode("number"),
        IrPrimitiveKind.Long     => new SimpleTypeNode("bigint"),
        IrPrimitiveKind.Boolean  => new SimpleTypeNode("boolean"),
        IrPrimitiveKind.DateTime => new SimpleTypeNode("string"),
        IrPrimitiveKind.Uuid     => new SimpleTypeNode("string"),
        IrPrimitiveKind.Binary   => new SimpleTypeNode("Blob"),
        IrPrimitiveKind.Any      => new RawTypeNode("unknown"),
        IrPrimitiveKind.Void     => new SimpleTypeNode("void"),
        _ => new RawTypeNode("unknown")
    };
}
```

Старый `ITypeMapper` (работающий с OpenAPI schema напрямую) **удаляется**. Новый `IIrTypeMapper` работает только с IR.

### ISchemaResolver.cs — оставить, переместить

```csharp
public interface ISchemaResolver
{
    IOpenApiSchema ResolveSchema(IOpenApiSchema schema);
    string? GetSchemaName(IOpenApiSchema schema);
}
```

**Почему оставить:** `ISchemaResolver` нужен `IrBuilder` для разрешения `$ref` ссылок при построении IR из OpenAPI. После того как IR построен, `ISchemaResolver` больше не используется. Можно оставить в `ValIO.Core/Interfaces/` или переместить в `ValIO.Core/IR/` как более подходящее место.

---

## 4. Устаревшие сервисы ValIO.Core/Services

### BaseDependencyAnalyzer.cs — удалить

```csharp
public abstract class BaseDependencyAnalyzer : IDependencyAnalyzer
{
    // Анализирует зависимости между ModelTemplateData
    // Использовался для определения порядка импортов в старом pipeline
}
```
**Почему:** Зависимости теперь в `IrTypeDependencyGraph`, который строится в `IrBuilder.BuildTypeDependencyGraph()`.

### BaseEnumExtractor.cs — удалить

```csharp
public abstract class BaseEnumExtractor : IEnumExtractor
{
    // Извлекал EnumTemplateData из OpenAPI схем
}
```
**Почему:** Заменён `IrBuilder.ExtractEnums()`.

### BaseTypeInfoBuilder.cs — удалить

```csharp
public abstract class BaseTypeInfoBuilder : ITypeInfoBuilder
{
    // Строил TypeInfo из OpenAPI схем
}
```
**Почему:** Заменён `IrBuilder.BuildType()` → `IrType`. `TypeInfo` удаляется.

### BaseTypeMapper.cs — удалить

```csharp
public abstract class BaseTypeMapper : ITypeMapper
{
    // Маппил OpenAPI schema в language-specific string типа
    // Используется в старом TemplateData pipeline
}
```
**Почему:** Заменён `IIrTypeMapper` (новый интерфейс, работающий с `IrType`).

### DependencyCollector.cs — удалить

```csharp
public class DependencyCollector
{
    // Вспомогательный класс для BaseDependencyAnalyzer
    // Собирал зависимости из TypeInfo
}
```
**Почему:** `TypeInfo` удаляется, `DependencyCollector` теряет смысл.

### ModelExtractor.cs — удалить

```csharp
public class ModelExtractor : BaseTypeInfoBuilder
{
    // Реализация IModelExtractor через BaseTypeInfoBuilder
}
```
**Почему:** Заменён `IrBuilder.ExtractModels()`.

### OperationExtractor.cs — удалить

```csharp
public class OperationExtractor : IOperationExtractor
{
    // Реализация IOperationExtractor
}
```
**Почему:** Заменён `IrBuilder.ExtractClients()` → `IrOperation`.

### SchemaResolver.cs — оставить, проверить

```csharp
public class SchemaResolver : ISchemaResolver
{
    // Разрешает $ref ссылки в OpenAPI документе
}
```

**Почему оставить:** `SchemaResolver` используется в `IrBuilder` и нужен. Переместить из `Services/` в `IR/` или оставить как есть — допустимо. Важно не удалять.

### NamingConverter.cs, FileWriter.cs — оставить

Эти сервисы используются в новом pipeline и не являются legacy:
- `NamingConverter` — нужен для `IrBuilder` и `AstBuilder`
- `FileWriter` — нужен для записи сгенерированных файлов

### BaseCodeGenerator.cs — реструктурировать (см. раздел 10)

---

## 5. Устаревшие модели ValIO.Core/Models

### TypeInfo.cs — удалить

```csharp
public class TypeInfo
{
    public string BaseType { get; }
    public bool IsCollection { get; }
    public bool IsDictionary { get; }
    public bool IsNullable { get; }
    public bool IsOptional { get; }
    public IReadOnlyList<TypeInfo> GenericArguments { get; }
    public GenerationLanguage? Language { get; }  // ← нарушение: знает о языке
    public TypeFormat Format { get; }             // ← legacy enum
    
    // Статические фабрики
    public static TypeInfo Simple(string typeName) => ...;
    public static TypeInfo Nullable(TypeInfo inner) => ...;
    public static TypeInfo Collection(TypeInfo element) => ...;
    public static TypeInfo Dictionary(TypeInfo key, TypeInfo value) => ...;
    public static TypeInfo Void() => ...;
}

public enum TypeFormat { Object, Enum, Union, Intersection }  // удалить
```

**Почему удалить:**
1. `TypeInfo.Language` знает о `GenerationLanguage` — нарушение слоистости
2. Заменяется `IrType` иерархией в `ValIO.Core/IR/Types/`
3. `TypeFormat` — дублирует информацию из `IrComposition` / `IrUnionType`

**Важно:** `TypeScriptAstBuilder.BuildType(TypeInfo typeInfo)` — **использует TypeInfo**. Это значит, что `TypeScriptAstBuilder` ещё не полностью мигрирован на IR. Нужно заменить `BuildType(TypeInfo)` → `BuildType(IrType)`. Подробнее в `08_AST_BUILDERS_DEEP.md`.

### GenerationOptions.cs — удалить

```csharp
public class GenerationOptions
{
    public ValidationLibrary? ValidationLibrary { get; set; }  // legacy enum
    public QueryWrapperLibrary? QueryWrapperLibrary { get; set; }  // legacy enum
    public object? CSharpOptions { get; set; }
    public object? TypeScriptOptions { get; set; }
    public object? CppOptions { get; set; }
}
```

**Почему удалить:** Заменяется plugin-специфичными опциями через `IGeneratorPlugin.ParseOptions(config)`. Каждый плагин парсит свою секцию конфигурации самостоятельно.

### ApiParameter.cs, ApiProperty.cs, ApiRequestBody.cs, ApiResponse.cs — удалить

```csharp
public class ApiParameter   { string Name; string Type; bool IsRequired; ... }
public class ApiProperty    { string Name; TypeInfo Type; bool IsRequired; ... }
public class ApiRequestBody { TypeInfo Type; bool IsRequired; string ContentType; ... }
public class ApiResponse    { string StatusCode; TypeInfo Type; string Description; ... }
```

**Почему удалить:** Это промежуточные модели из старого pipeline (OpenAPI → Api* → TemplateData). В новом pipeline есть:
- `IrParameter` вместо `ApiParameter`
- `IrProperty` вместо `ApiProperty`
- `IrRequestBody` вместо `ApiRequestBody`
- `IrResponse` вместо `ApiResponse`

### SchemaCompositionInfo.cs — проверить и удалить

```csharp
public class SchemaCompositionInfo
{
    public IrCompositionKind Kind { get; }
    public IReadOnlyList<string> TypeNames { get; }
}
```

Если используется только в `IrBuilder` внутри логики `ExtractComposition`, можно переделать в локальную private структуру или удалить — `IrComposition` уже несёт эту информацию.

### GenerationConfiguration.cs — частично переработать

```csharp
public class GenerationConfiguration
{
    public string OutputPath { get; set; }
    public GenerationLanguage Language { get; set; }  // ← legacy enum
    public GenerationOptions? Options { get; set; }   // ← legacy class
    public FileOrganization FileOrganization { get; set; }
    public NamingConventions NamingConventions { get; set; }
}
```

**Изменение:**
- Убрать `Language` (заменяется идентификатором плагина — строкой)
- Убрать `Options` (заменяется plugin-специфичной конфигурацией)
- Добавить `PluginId` (строка, например `"typescript.axios"`, `"csharp"`)
- Добавить `PluginOptions` (словарь или `JsonElement` для plugin-специфичных опций)

```csharp
// Новый GenerationConfiguration
public class GenerationConfiguration
{
    public required string OutputPath { get; init; }
    public required string PluginId { get; init; }       // "typescript.axios", "csharp", "php.guzzle"
    public JsonElement? PluginOptions { get; init; }     // плагин парсит сам
    public FileOrganization FileOrganization { get; init; }
    public NamingConventions NamingConventions { get; init; }
}
```

### FileGroupingStrategy.cs, FileOrganization.cs, NamingConvention.cs, NamingConventions.cs — оставить

Эти модели используются в новом pipeline и не являются legacy.

### ParameterLocation.cs — оставить или перенести в IR

```csharp
public enum ParameterLocation { Query, Path, Header, Cookie }
```

Если используется только в `IrOperation` — переименовать в `IrParameterLocation` и переместить в `ValIO.Core/IR/Enums/`.

---

## 6. Устаревшие фабрики ValIO/Factories

### CodeGeneratorFactory.cs — удалить

```csharp
public class CodeGeneratorFactory
{
    public ICodeGenerator CreateGenerator(GenerationLanguage language, GenerationOptions? options)
    {
        return language switch
        {
            GenerationLanguage.CSharp     => _serviceProvider.GetRequiredService<CSharpCodeGenerator>(),
            GenerationLanguage.TypeScript => _serviceProvider.GetRequiredService<TypeScriptCodeGenerator>(),
            GenerationLanguage.Cpp        => _serviceProvider.GetRequiredService<CppCodeGenerator>(),
            _ => throw new NotSupportedException(...)
        };
    }
}
```

**Почему удалить:** Это жёстко закодированный switch на `GenerationLanguage`. Заменяется `GeneratorRegistry` из plugin-архитектуры:

```csharp
// Новый подход
public class GeneratorRegistry
{
    private readonly Dictionary<string, IGeneratorPlugin> _plugins = new();

    public void Register(IGeneratorPlugin plugin)
        => _plugins[plugin.PluginId] = plugin;

    public IGeneratorPlugin GetPlugin(string pluginId)
        => _plugins.TryGetValue(pluginId, out var plugin)
           ? plugin
           : throw new NotSupportedException($"Plugin '{pluginId}' not registered");
}
```

### TypeMapperFactory.cs — удалить

```csharp
public class TypeMapperFactory
{
    public ITypeMapper CreateTypeMapper(GenerationLanguage language)
    {
        return language switch { ... };  // switch на GenerationLanguage — legacy
    }
}
```

**Почему удалить:** Маппер теперь регистрируется через DI каждым плагином. Централизованная фабрика с switch не нужна.

---

## 7. Устаревшие AST-слои

### IAstRenderer.cs — удалить

```csharp
// ValIO.Core/Ast/Visitors/IAstRenderer.cs
public interface IAstRenderer : IAstVisitor<string>
{
    // Пустой маркерный интерфейс — никакой ценности
}
```

**Почему:** После рефакторинга IAstVisitor dispatch-методы удаляются. Рендереры типизируются как `BaseAstVisitor<string>` с конкретным языковым интерфейсом (`ITypeScriptAstRenderer`, `ICSharpAstRenderer`). Маркерный интерфейс `IAstRenderer` не даёт ничего — только создаёт ложную абстракцию.

### ICodeRenderer.cs — удалить

```csharp
// ValIO.Core/Interfaces/ICodeRenderer.cs
public interface ICodeRenderer
{
    GenerationLanguage Language { get; }
    string RenderModel(IModelTemplateData data);
    string RenderEnum(EnumTemplateData data);
    string RenderClient(IClientTemplateData data);
    string RenderOperation(IOperationTemplateData data);
}
```

**Почему:** Весь интерфейс завязан на TemplateData. Рендеринг теперь через `node.Accept(visitor)`.

### HttpAstVisitorBase.cs — удалить

```csharp
// ValIO.Core/Ast/Visitors/HttpAstVisitorBase.cs
public abstract class HttpAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IHttpAstVisitor<TResult>
{
    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);
    // Абстрактные методы только для HTTP
}
```

**Почему:** После удаления HTTP методов из `IAstVisitor` нет смысла в `HttpAstVisitorBase`. HTTP рендереры напрямую реализуют `BaseAstVisitor<string>` + `IHttpAstVisitor<string>`. Промежуточный `HttpAstVisitorBase` — лишний уровень.

### ValIO.Generators.TypeScript.Zod/ (отдельный проект) — удалить

```
ValIO.Generators.TypeScript.Zod/
  Ast/
    Visitors/
      ZodAstVisitorBase.cs  ← единственный файл
  ValIO.Generators.TypeScript.Zod.csproj
```

Это практически пустой проект — остаток от ранней архитектуры. Реальная Zod-реализация находится в `ValIO.Generators.TypeScript/Zod/`. Проект удаляется полностью вместе с `.csproj`.

---

## 8. Устаревшие проекты и файлы вне основного кода

### Examples/valio-master/ — решение по ситуации

```
Examples/
  valio-master/
    ValIO.Core/
    ValIO.Generators.TypeScript/
    ValIO/
    ...
```

Это полная копия старой версии кода. Оставить только если нужна как reference для сравнения при рефакторинге. Иначе — удалить чтобы не создавать путаницу при поиске по коду.

**Рекомендация:** Переместить в отдельную ветку git (`archive/legacy-template-data`) и удалить из основной ветки.

---

## 9. Перечисления под замену

### GenerationLanguage.cs — удалить

```csharp
public enum GenerationLanguage { CSharp, TypeScript, Cpp }
```

**Почему:** Язык генерации теперь идентифицируется строковым `PluginId` плагина (`"typescript.fetch"`, `"csharp"`, `"php.guzzle"`). Enum ограничивает расширяемость.

**Заменяется на:** строки `PluginId` в `IGeneratorPlugin` и `GenerationConfiguration`.

**Где используется сейчас (полный аудит):**
- `ICodeGenerator.SupportsLanguage(GenerationLanguage)` → метод удаляется
- `CodeGeneratorFactory.CreateGenerator(GenerationLanguage, ...)` → фабрика удаляется
- `BaseCodeGenerator.Language` поле → заменяется `PluginId` строкой
- `ICodeRenderer.Language` → интерфейс удаляется
- `GenerationConfiguration.Language` → заменяется `PluginId`
- `TypeScriptCodeGenerator.SupportsLanguage(GenerationLanguage)` → метод удаляется

### ValidationLibrary.cs — удалить

```csharp
public enum ValidationLibrary
{
    Zod,
    Yup,
    Valli   // ОПЕЧАТКА: должно быть Valibot
}
```

**Почему:** Validation library теперь выражается через `PluginId` плагина (`"typescript.zod"`, `"typescript.valibot"`, `"typescript.typebox"`). Enum знает только о 3 библиотеках из планируемых 4+.

**Исправление опечатки Valli → Valibot:** Это не просто опечатка — правильное название библиотеки `valibot`. Enum удаляется, поэтому опечатка исчезает вместе с ним, но важно зафиксировать в документации.

### QueryWrapperLibrary.cs — удалить

```csharp
public enum QueryWrapperLibrary { TanStackQuery, SWR }
```

**Почему:** Аналогично ValidationLibrary — заменяется `PluginId`. TanStack Query для React, Vue, Angular, Solid — это разные плагины.

### TypeScriptHttpTransports.cs — заменить

```csharp
// ValIO.Generators.TypeScript/Models/TypeScriptHttpTransports.cs
[Flags]
public enum TypeScriptHttpTransports
{
    None  = 0,
    Fetch = 1,
    Axios = 2
}
```

**Почему переработать:** Enum знает только о 2 транспортах. Планируется 6+ (undici, ofetch, got, ky, Angular HttpClient, native fetch). Flags enum плохо масштабируется — каждый новый транспорт требует новой степени 2.

**Замена:** Транспорт идентифицируется `PluginId` плагина. Конфигурация может указывать несколько плагинов:

```yaml
# valIO.yaml
generators:
  - plugin: typescript.axios
    output: ./src/api/axios
  - plugin: typescript.fetch
    output: ./src/api/fetch
  - plugin: typescript.ky
    output: ./src/api/ky
```

---

## 10. BaseCodeGenerator и ICodeGenerator — реструктуризация

### Текущий ICodeGenerator — удалить

```csharp
public interface ICodeGenerator
{
    Task<GenerationResult> GenerateAsync(OpenApiDocument document, GenerationConfiguration config);
    bool SupportsLanguage(GenerationLanguage language);  // ← GenerationLanguage — legacy
    bool SupportsOptions(GenerationOptions? options);    // ← GenerationOptions — legacy
}
```

**Почему:** Оба вспомогательных метода используют legacy типы.

**Новый контракт:**
```csharp
// ValIO.Core/Pipeline/ICodeGenerationPipeline.cs
public interface ICodeGenerationPipeline
{
    /// <summary>
    /// Идентификатор плагина, который использует этот pipeline
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Запускает pipeline генерации кода
    /// </summary>
    Task<GenerationResult> RunAsync(IrDocument irDocument, GenerationConfiguration config, CancellationToken ct = default);
}
```

Pipeline собирает `ICodeGenerationStep[]` из DI и выполняет их по порядку.

### Текущий BaseCodeGenerator — реструктурировать

```csharp
public abstract class BaseCodeGenerator : ICodeGenerator
{
    protected readonly ITypeMapper TypeMapper;           // ← legacy ITypeMapper
    protected readonly INamingConverter NamingConverter; // ← оставить
    protected readonly IFileWriter FileWriter;           // ← оставить
    protected readonly ILogger Logger;                   // ← оставить
    protected readonly GenerationLanguage Language;      // ← legacy enum

    public abstract bool SupportsLanguage(GenerationLanguage language);  // ← legacy
    public abstract bool SupportsOptions(GenerationOptions? options);    // ← legacy
    public abstract Task<GenerationResult> GenerateAsync(OpenApiDocument document, GenerationConfiguration config);
}
```

**Изменения:**
- Убрать `ITypeMapper TypeMapper` (заменяется `IIrTypeMapper`)
- Убрать `GenerationLanguage Language` (заменяется `PluginId` из `IGeneratorPlugin`)
- Убрать `SupportsLanguage` и `SupportsOptions`
- Переименовать в `BaseCodeGenerationPipeline : ICodeGenerationPipeline`

```csharp
// Новая база
public abstract class BaseCodeGenerationPipeline : ICodeGenerationPipeline
{
    protected readonly INamingConverter NamingConverter;
    protected readonly IFileWriter FileWriter;
    protected readonly ILogger Logger;
    protected readonly IReadOnlyList<ICodeGenerationStep> Steps;

    public abstract string PluginId { get; }

    public virtual async Task<GenerationResult> RunAsync(
        IrDocument irDocument,
        GenerationConfiguration config,
        CancellationToken ct = default)
    {
        var context = new GenerationContext(irDocument, config);

        foreach (var step in Steps)
        {
            await step.ExecuteAsync(context, ct);
        }

        await FileWriter.WriteAllAsync(context.GeneratedFiles, config.OutputPath, ct);
        return context.ToResult();
    }
}
```

### TypeScriptCodeGenerator — разобрать на Steps

Текущий `TypeScriptCodeGenerator` (678 строк) — God Object. Разбивается на:

| Step | Ответственность |
|------|-----------------|
| `IrBuildStep` | OpenAPI → IrDocument (общий для всех языков) |
| `TypeScriptModelsStep` | IrDocument.Models → TS interface/type файлы |
| `TypeScriptEnumsStep` | IrDocument.Enums → TS enum файлы |
| `TypeScriptClientStep` | IrDocument.Clients → HTTP client файлы (базовый) |
| `AxiosClientStep` | IrDocument.Clients → Axios-специфичные файлы |
| `FetchClientStep` | IrDocument.Clients → Fetch-специфичные файлы |
| `ZodSchemasStep` | IrDocument.Models → Zod schema файлы |
| `TanStackQueryStep` | IrDocument.Clients → TanStack Query hooks |
| `TypeScriptParamsStep` | IrDocument.Clients → params объекты |

Каждый Step:
```csharp
public interface ICodeGenerationStep
{
    int Order { get; }  // порядок выполнения
    string Name { get; }  // для логирования
    Task ExecuteAsync(GenerationContext context, CancellationToken ct);
}

public class GenerationContext
{
    public IrDocument IrDocument { get; }
    public GenerationConfiguration Config { get; }
    public Dictionary<string, string> GeneratedFiles { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();
}
```

---

## 11. Сводная таблица всех удалений

### ValIO.Core/Interfaces/ — удалить файлы

| Файл | Причина |
|------|---------|
| `IClientRenderer.cs` | TemplateData legacy |
| `IModelRenderer.cs` | TemplateData legacy |
| `IEnumRenderer.cs` | TemplateData legacy |
| `IModelFormatRenderer.cs` | TemplateData + GenerationLanguage legacy |
| `IEnumFormatRenderer.cs` | TemplateData + GenerationLanguage legacy |
| `IOperationRenderer.cs` | TemplateData legacy |
| `IQueryWrapperRenderer.cs` | TemplateData + QueryWrapperLibrary legacy |
| `IDependencyAnalyzer.cs` | Заменён IrTypeDependencyGraph |
| `IEnumExtractor.cs` | Заменён IrBuilder.ExtractEnums |
| `IModelExtractor.cs` | Заменён IrBuilder.ExtractModels |
| `IOperationExtractor.cs` | Заменён IrBuilder.ExtractClients |
| `ITypeInfoBuilder.cs` | TypeInfo legacy |
| `IClientTemplateData.cs` | TemplateData legacy |
| `IModelTemplateData.cs` | TemplateData legacy |
| `IEnumTemplateData.cs` | TemplateData legacy |
| `IOperationTemplateData.cs` | TemplateData legacy |
| `ICodeRenderer.cs` | TemplateData + GenerationLanguage legacy |
| `ICodeGenerator.cs` | GenerationLanguage + GenerationOptions legacy — заменяется ICodeGenerationPipeline |
| `ITypeMapper.cs` | Работает с OpenAPI schema напрямую — заменяется IIrTypeMapper |

### ValIO.Core/Services/ — удалить файлы

| Файл | Причина |
|------|---------|
| `BaseDependencyAnalyzer.cs` | Заменён IrTypeDependencyGraph |
| `BaseEnumExtractor.cs` | Заменён IrBuilder.ExtractEnums |
| `BaseTypeInfoBuilder.cs` | TypeInfo legacy |
| `BaseTypeMapper.cs` | Заменён IIrTypeMapper |
| `DependencyCollector.cs` | TypeInfo legacy |
| `ModelExtractor.cs` | Заменён IrBuilder |
| `OperationExtractor.cs` | Заменён IrBuilder |
| `BaseCodeGenerator.cs` | Реструктурируется в BaseCodeGenerationPipeline |

### ValIO.Core/Models/ — удалить файлы

| Файл | Причина |
|------|---------|
| `TypeInfo.cs` | Заменён IrType иерархией |
| `GenerationOptions.cs` | Заменён plugin-специфичными опциями |
| `GenerationLanguage.cs` | Заменён PluginId строкой |
| `ValidationLibrary.cs` | Заменён PluginId; опечатка Valli→Valibot |
| `QueryWrapperLibrary.cs` | Заменён PluginId |
| `ApiParameter.cs` | Заменён IrParameter |
| `ApiProperty.cs` | Заменён IrProperty |
| `ApiRequestBody.cs` | Заменён IrRequestBody |
| `ApiResponse.cs` | Заменён IrResponse |
| `SchemaCompositionInfo.cs` | Заменён IrComposition |
| `TemplateData/ModelTemplateData.cs` | TemplateData legacy |
| `TemplateData/OperationTemplateData.cs` | TemplateData legacy |
| `TemplateData/ClientTemplateData.cs` | TemplateData legacy |
| `TemplateData/EnumTemplateData.cs` | TemplateData legacy |
| `TemplateData/Options/*.cs` (6 файлов) | TemplateData legacy |

### ValIO.Core/Ast/Visitors/ — удалить файлы

| Файл | Причина |
|------|---------|
| `IAstRenderer.cs` | Пустой маркерный интерфейс |
| `HttpAstVisitorBase.cs` | Не нужен после исправления IAstVisitor |

### ValIO.Core/Ast/Enums/ — удалить

| Файл | Причина |
|------|---------|
| `AstNodeKind.cs` | 130+ значений, заменяется системой типов |

### ValIO/Factories/ — удалить файлы

| Файл | Причина |
|------|---------|
| `CodeGeneratorFactory.cs` | switch на GenerationLanguage, заменяется GeneratorRegistry |
| `TypeMapperFactory.cs` | switch на GenerationLanguage, заменяется DI |

### Отдельные проекты

| Проект | Причина |
|--------|---------|
| `ValIO.Generators.TypeScript.Zod/` | Почти пустой, единственный файл ZodAstVisitorBase.cs — legacy |

### ValIO.Core/Models/ — изменить (не удалять)

| Файл | Изменение |
|------|-----------|
| `GenerationConfiguration.cs` | Убрать `Language`, `Options`; добавить `PluginId`, `PluginOptions` |

### ValIO.Core/Interfaces/ — оставить и переработать

| Файл | Изменение |
|------|-----------|
| `ITypeMapper.cs` | Заменить на `IIrTypeMapper.cs` — работает с `IrType`, не с OpenAPI schema |
| `ISchemaResolver.cs` | Оставить, возможно переместить в `ValIO.Core/IR/` |

### Ключевые новые файлы (создать)

| Файл | Назначение |
|------|-----------|
| `ValIO.Core/Pipeline/ICodeGenerationPipeline.cs` | Новый контракт генератора |
| `ValIO.Core/Pipeline/ICodeGenerationStep.cs` | Контракт шага генерации |
| `ValIO.Core/Pipeline/GenerationContext.cs` | Контекст, передаваемый между шагами |
| `ValIO.Core/Pipeline/BaseCodeGenerationPipeline.cs` | Базовый pipeline |
| `ValIO.Core/Interfaces/IIrTypeMapper.cs` | Новый маппер IrType → AST TypeNode |
| `ValIO.Core/Plugin/IGeneratorPlugin.cs` | Контракт плагина |
| `ValIO.Core/Plugin/GeneratorRegistry.cs` | Реестр плагинов |

---

## 12. TypeScript-генератор — TypeScript-специфичный legacy

Это отдельный и очень объёмный блок legacy, который **полностью находится внутри** `ValIO.Generators.TypeScript/` и не был учтён в предыдущих секциях. Все эти файлы относятся к старому TemplateData-пути (Мир 1) и не используются в новом IR/AST-пайплайне (Мир 2).

---

### 12.1 Models/CodeGeneration/ — ещё один уровень генерации кода

Папка `ValIO.Generators.TypeScript/Models/CodeGeneration/` содержит **третий уровень абстракции** (помимо TemplateData и AST) для генерации TypeScript кода. Это промежуточный слой, который никогда не стал частью основного пайплайна — он был создан в ходе эксперимента и так и остался висеть.

**Принцип работы (старый, не активный):**
```
TemplateData → CodeGeneration models (FunctionSignature, OptionsDefinition, ...) → string.Format → файл
```

Этот слой дублирует AST нодам, только через строки (string-based, не дерево).

#### Файлы к удалению:

**`ValIO.Generators.TypeScript/Models/CodeGeneration/CodeBlock.cs`**

```csharp
// Содержит CodeBlock (список CodeLine) + CodeLine (строка + отступ)
// Заменяется: StatementNode / BlockStatementNode в AST

public sealed class CodeBlock
{
    public required IReadOnlyList<CodeLine> Lines { get; init; }
    public int BaseIndentLevel { get; init; }
    public string Format()  // Форматирует через StringBuilder + indent
}

public sealed class CodeLine
{
    public required string Content { get; init; }
    public int IndentLevel { get; init; }
    public static CodeLine Empty => ...;
}
```

**Почему удалить:** `CodeBlock` — это ручная имитация AST через строки. В новом пайплайне блоки кода представлены через `BlockStatementNode`, `SourceFileNode`, рендеринг через `TypeScriptAstRenderer` с управлением отступами через `_indentLevel`. Формат через `Format()` — string-building без дерева, невозможно анализировать или трансформировать.

---

**`ValIO.Generators.TypeScript/Models/CodeGeneration/FunctionParameter.cs`**

```csharp
// Содержит FunctionParameter + GenericParameter
// FunctionParameter.Format() → "name?: type = default"
// CreateOptionsParameter(OptionsDefinition) — статический фабричный метод

public sealed class FunctionParameter
{
    public required string Name { get; init; }
    public required string Type { get; init; }  // строка, не IrType/TypeNode
    public bool IsOptional { get; init; }
    public string? Description { get; init; }
    public string? DefaultValue { get; init; }
    public string Format() → "name?: Type = default"
    public static FunctionParameter CreateOptionsParameter(OptionsDefinition) → ...
}
```

**Почему удалить:** `FunctionParameter` дублирует `FunctionDeclarationNode.Parameters` в AST — только строковый вариант. В AST параметр — это `PropertySignatureNode` или именованный параметр функции в `FunctionDeclarationNode`. Всё рендерится через `TypeScriptAstRenderer`.

---

**`ValIO.Generators.TypeScript/Models/CodeGeneration/FunctionParameterBuilder.cs`**

```csharp
// Builder (FluentAPI) для FunctionParameter
// Fluent методы: WithName, WithType, WithOptional, WithDescription, WithDefault, Build
```

**Почему удалить:** Builder для объекта, который сам удаляется.

---

**`ValIO.Generators.TypeScript/Models/CodeGeneration/FunctionSignature.cs`**

```csharp
// Содержит FunctionSignature + GenericParameter
// Format(indentLevel) → многострочная строка с "export async function Name<T>(...): ReturnType {"

public sealed class FunctionSignature
{
    public required string Name { get; init; }
    public IReadOnlyList<GenericParameter> GenericParameters { get; init; }
    public required IReadOnlyList<FunctionParameter> Parameters { get; init; }
    public string? ReturnType { get; init; }
    public bool IsAsync { get; init; }
    public bool IsArrowFunction { get; init; }
    public string Format(int indentLevel = 0) → ...  // собственная реализация с StringBuilder
}
```

**Почему удалить:** Это ручная реализация того, что делает `FunctionDeclarationNode` + `TypeScriptAstRenderer`. `FunctionSignature.Format()` содержит ~50 строк форматирования кода через `StringBuilder` — это ровно то, что вынесено в `TypeScriptAstRenderer.VisitFunctionDeclaration()`.

---

**`ValIO.Generators.TypeScript/Models/CodeGeneration/OptionsDefinition.cs`**

```csharp
// Содержит OptionsDefinition + OptionsProperty
// Format(indentLevel) → "options?: { prop1?: Type1; prop2: Type2; }"

public sealed class OptionsDefinition
{
    public required IReadOnlyList<OptionsProperty> Properties { get; init; }
    public bool IsOptional { get; init; } = true;
    public string Format(int indentLevel = 0) → ...
}

public sealed class OptionsProperty
{
    public required string Name { get; init; }
    public required string Type { get; init; }  // строка, не TypeNode
    public bool IsOptional { get; init; }
    public string? Description { get; init; }
    public string Format(int indentLevel = 0) → "${indent}{Name}{optional}: {Type};"
}
```

**Почему удалить:** Заменяется `ObjectLiteralExpressionNode` + `PropertySignatureNode` в AST.

---

**`ValIO.Generators.TypeScript/Models/CodeGeneration/OptionsDefinitionBuilder.cs`**

Builder для `OptionsDefinition`. Удаляется вместе с `OptionsDefinition`.

---

**`ValIO.Generators.TypeScript/Models/CodeGeneration/TypeDefinition.cs`**

```csharp
// Содержит TypeDefinition + TypeObjectDefinition
// TypeDefinition = строковое описание одного свойства типа
// TypeObjectDefinition = коллекция TypeDefinition, Format() → "{ prop?: Type; }"

public sealed class TypeDefinition
{
    public required string Name { get; init; }
    public required string Type { get; init; }  // строка
    public bool IsOptional { get; init; }
    public string? Description { get; init; }
    public string Format(int indentLevel = 0) → "${indent}{Name}{optional}: {Type};"
}

public sealed class TypeObjectDefinition
{
    public required IReadOnlyList<TypeDefinition> Properties { get; init; }
    public string Format(int indentLevel = 0) → многострочный объект
}
```

**Почему удалить:** Заменяется `InterfaceDeclarationNode` + `PropertySignatureNode` в AST, которые корректно рендерятся через `TypeScriptAstRenderer`.

---

**`ValIO.Generators.TypeScript/Models/CodeGeneration/TypeDefinitionBuilder.cs`**

Builder для `TypeDefinition`. Удаляется вместе с `TypeDefinition`.

---

**Текущее использование `Models/CodeGeneration/`:**

```bash
# Grep показывает:
grep -r "CodeBlock\|FunctionSignature\|OptionsDefinition\|TypeDefinition" --include="*.cs"
# → только TypeScriptAstBuilder.cs (дуальная поддержка) и сами файлы
```

`TypeScriptAstBuilder` имеет дуальную поддержку: старые методы через `FunctionSignature`/`OptionsDefinition` (legacy path) и новые методы через AST ноды. После удаления legacy методов из `TypeScriptAstBuilder` вся папка `Models/CodeGeneration/` становится мёртвым кодом.

**Итого — удалить 7 файлов:**
```
ValIO.Generators.TypeScript/Models/CodeGeneration/
  CodeBlock.cs
  FunctionParameter.cs
  FunctionParameterBuilder.cs
  FunctionSignature.cs
  OptionsDefinition.cs
  OptionsDefinitionBuilder.cs
  TypeDefinition.cs
  TypeDefinitionBuilder.cs
```

---

### 12.2 ApiTemplateData.cs и InterfaceTemplateData.cs — TS-специфичные TemplateData модели

Это TypeScript-специфичные TemplateData модели, аналогичные тем что в `ValIO.Core/Models/TemplateData/`, но для TypeScript-генератора.

#### `ValIO.Generators.TypeScript/Models/ApiTemplateData.cs`

```csharp
// Содержит 4 класса TemplateData для TypeScript HTTP клиентов:

public class TypeScriptClientTemplateData
{
    public string ClientName { get; set; }
    public IReadOnlyList<TypeScriptOperationTemplateData> Operations { get; set; }
    public TypeScriptHttpTransports HttpTransports { get; set; }  // ← legacy enum
    public IReadOnlyList<string> ImportedTypes { get; set; }
    public string? ModelsPath { get; set; }
    public AxiosOptions? AxiosOptions { get; set; }
}

public class TypeScriptOperationTemplateData
{
    public string MethodName { get; set; }
    public string HttpMethod { get; set; }
    public string Path { get; set; }
    public TypeInfo ReturnTypeInfo { get; set; }     // ← TypeInfo — legacy
    public TypeInfo? RequestBodyTypeInfo { get; set; }  // ← TypeInfo — legacy
    public IReadOnlyList<TypeScriptParameterTemplateData> PathParameters { get; set; }
    public IReadOnlyList<TypeScriptParameterTemplateData> QueryParameters { get; set; }
    public IReadOnlyList<TypeScriptParameterTemplateData> HeaderParameters { get; set; }
    // ... ещё ~20 свойств
}

public class TypeScriptParameterTemplateData
{
    public TypeInfo TypeInfo { get; set; }    // ← TypeInfo — legacy
    public string Type { get; set; }          // ← дублирование TypeInfo
}

public class TypeScriptFormDataPropertyTemplateData { ... }
```

**Почему удалить:** Все эти классы завязаны на `TypeInfo` (legacy) и `TypeScriptHttpTransports` (legacy enum). В новом пайплайне клиент строится через `IrClient` → `AxiosAstBuilder`/`FetchAstBuilder` → AST → рендеринг. Промежуточный TemplateData слой не нужен.

**Использование:** Класс `TypeScriptAstBuilder` имеет метод `BuildClientSourceFile(TypeScriptClientTemplateData)` — legacy перегрузка. Она удаляется в рамках очистки `TypeScriptAstBuilder`.

---

#### `ValIO.Generators.TypeScript/Models/InterfaceTemplateData.cs`

```csharp
// Содержит 4 класса TemplateData для TypeScript моделей и enum:

public class TypeScriptModelTemplateData
{
    public string InterfaceName { get; set; }
    public IReadOnlyList<TypeScriptPropertyTemplateData> Properties { get; set; }
    public bool UseStrictNullChecks { get; set; }
    public IReadOnlyList<string> ImportedTypes { get; set; }
    public string? BaseClassName { get; set; }
    public ValidationLibrary? ValidationLibrary { get; set; }   // ← legacy enum
}

public class TypeScriptPropertyTemplateData
{
    public TypeInfo TypeInfo { get; set; }    // ← TypeInfo — legacy
    public string Type { get; set; }          // ← строка-дублёр TypeInfo
    public bool IsOptional { get; set; }
}

public class TypeScriptEnumTemplateData
{
    public TypeScriptEnumFormat EnumFormat { get; set; }
    public ValidationLibrary? ValidationLibrary { get; set; }   // ← legacy enum
    public IReadOnlyList<TypeScriptEnumValueTemplateData> Values { get; set; }
}

public class TypeScriptEnumValueTemplateData
{
    public string Name { get; set; }
    public object? Value { get; set; }
    public string ValueString { get; set; }
}
```

**Почему удалить:** Аналогично `ApiTemplateData.cs` — завязаны на `TypeInfo` и `ValidationLibrary` enum. В новом пайплайне модели строятся через `IrModel` → `TypeScriptAstBuilder.BuildModelSourceFile(IrModel)` → AST → рендеринг.

---

#### TypeScript-специфичные `*TemplateDataOptions` файлы — удалить вместе с TemplateData

```
ValIO.Generators.TypeScript/Models/TypeScriptClientTemplateDataOptions.cs
ValIO.Generators.TypeScript/Models/TypeScriptModelTemplateDataOptions.cs
ValIO.Generators.TypeScript/Models/TypeScriptEnumTemplateDataOptions.cs
ValIO.Generators.TypeScript/Models/TypeScriptOperationTemplateDataOptions.cs
ValIO.Generators.TypeScript/Models/TypeScriptPropertyTemplateDataOptions.cs
```

Каждый из этих файлов наследует от соответствующего `*TemplateDataOptions` из `ValIO.Core/Models/TemplateData/Options/` и добавляет TS-специфичные поля (в основном `ValidationLibrary?`). После удаления TemplateData все эти файлы удаляются.

---

### 12.3 Renderers/ — TS-специфичные legacy рендереры

Три рендерера, реализующие `IModelFormatRenderer`/`IEnumFormatRenderer` — оба интерфейса идут под удаление.

#### `ValIO.Generators.TypeScript/Renderers/InterfaceModelRenderer.cs`

```csharp
public class InterfaceModelRenderer : IModelFormatRenderer
{
    public ModelFormat Format => ModelFormat.Interface;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    public string Render(IModelTemplateData data)
    {
        // 90 строк StringBuilder-based рендеринга TypeScript interface
        // Обрабатывает: imports, JSDoc, interface/type alias/intersection type
        // Логика определяет формат: useTypeAlias / useIntersectionType / обычный interface
    }
}
```

**Текущий статус:** Зарегистрирован в `ServiceCollectionExtensions.AddTypeScriptGenerators()`:
```csharp
services.AddSingleton<InterfaceModelRenderer>();
services.AddSingleton<IModelFormatRenderer>(sp => sp.GetRequiredService<InterfaceModelRenderer>());
```
Но `IModelFormatRenderer` используется только в `BaseCodeGenerator` (legacy) — который сам удаляется.

**Почему удалить:** Логика рендеринга TypeScript интерфейсов полностью реализована в `TypeScriptAstRenderer.VisitInterfaceDeclaration()`. `InterfaceModelRenderer` — дублёр на основе TemplateData.

---

#### `ValIO.Generators.TypeScript/Renderers/TypeScriptEnumRenderer.cs`

```csharp
public class TypeScriptEnumRenderer : IEnumFormatRenderer
{
    public EnumFormat Format => EnumFormat.Enum;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    public string Render(IEnumTemplateData data)
    {
        // Рендерит "export enum Name { Value = 'value', ... }"
        // Использует CommentGenerator.GenerateJSDocComment
        // Использует CodeFormatter.AppendLine
    }
}
```

**Текущий статус:** Зарегистрирован в DI. Не используется в активном пайплайне (только через `IEnumFormatRenderer` в legacy BaseCodeGenerator).

**Почему удалить:** Генерация TypeScript enum реализована в `TypeScriptAstBuilder.BuildEnumSourceFile(IrEnum)` + `TypeScriptAstRenderer`.

---

#### `ValIO.Generators.TypeScript/Renderers/TypeScriptUnionTypeRenderer.cs`

```csharp
public class TypeScriptUnionTypeRenderer : IEnumFormatRenderer
{
    public EnumFormat Format => EnumFormat.Union;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    public string Render(IEnumTemplateData data)
    {
        // Рендерит "export type Name = 'value1' | 'value2' | 'value3';"
    }
}
```

Аналогично `TypeScriptEnumRenderer` — дублёр. Удаляется.

---

### 12.4 Services/ — TypeScript-специфичные legacy сервисы

Четыре сервиса, унаследованных от `Base*` классов, которые сами под удалением.

#### `ValIO.Generators.TypeScript/Services/TypeScriptDependencyAnalyzer.cs`

```csharp
public class TypeScriptDependencyAnalyzer : BaseDependencyAnalyzer
{
    // Конструктор: base(namingConverter, GenerationLanguage.TypeScript)

    protected override string? ResolveImportPath(string typeName, string? modelsPath, string? schemasPath)
    {
        // Логика:
        // 1. IsBuiltInType → null (нет импорта)
        // 2. Имя заканчивается на "Schema" → schemasPath + camelCase имя
        // 3. Иначе → modelsPath + camelCase имя
    }

    public override bool IsBuiltInType(string typeName, GenerationLanguage language)
    {
        // Проверяет: any, unknown, object, string, number, boolean, void, null, undefined,
        //             Date, Blob, File, Array, Record, Promise, Response
        // Обрабатывает generic типы через разбор "<" и массивы через "[]"
    }
}
```

**Почему удалить:** `BaseDependencyAnalyzer` использует `GenerationLanguage` (legacy enum) и `TypeInfo` (legacy). В новом пайплайне зависимости между типами определяются через `IrTypeDependencyGraph` (в `ValIO.Core/IR/`) — граф строится `IrBuilder` на этапе анализа IR. Импорты разрешаются в `TypeScriptAstBuilder` на основе IR-информации, а не через `BaseDependencyAnalyzer`.

**Что сохранить:** Логику определения встроенных TypeScript типов (`IsBuiltInType`) перенести в новый `TypeScriptIrTypeMapper` как `private static bool IsBuiltInTsType(string typeName)`.

---

#### `ValIO.Generators.TypeScript/Services/TypeScriptEnumExtractor.cs`

```csharp
public class TypeScriptEnumExtractor : BaseEnumExtractor
{
    // base(schemaResolver, namingConverter, GenerationLanguage.TypeScript)

    protected override EnumFormat DetermineEnumFormat(string underlyingType)
    {
        // string → EnumFormat.Union
        // всё остальное → EnumFormat.Enum
    }
}
```

**Почему удалить:** `BaseEnumExtractor` работает с `IOpenApiSchema` напрямую — обходя IR. В новом пайплайне перечисления извлекаются в `IrBuilder.ExtractEnums()` в `IrEnum` записи. Логика определения формата (Union vs Enum для string) переходит в `IrEnum.IsStringEnum` флаг + `TypeScriptAstBuilder.BuildEnumSourceFile()`.

---

#### `ValIO.Generators.TypeScript/Services/TypeScriptOperationNamingStrategy.cs`

```csharp
public class TypeScriptOperationNamingStrategy : IOperationNamingStrategy
{
    public string ToVariableName(string parameterName, INamingConverter namingConverter)
        => namingConverter.ToCamelCase(parameterName);

    public string ToPropertyName(string parameterName, INamingConverter namingConverter)
        => namingConverter.ToPascalCase(parameterName);

    public string GetDefaultParameterType() => "string";
    public string GetDefaultRequestBodyType() => "any";
}
```

**Почему удалить:** `IOperationNamingStrategy` — legacy интерфейс. Именование параметров операций теперь часть `IrOperation.Parameters[].Name` (уже применён `INamingConverter` в `IrBuilder`). TypeScript-специфичные типы по умолчанию (`"string"`, `"any"`) — часть `TypeScriptIrTypeMapper`.

---

#### `ValIO.Generators.TypeScript/Services/TypeScriptTypeInfoBuilder.cs`

```csharp
public class TypeScriptTypeInfoBuilder : BaseTypeInfoBuilder
{
    // base(typeMapper, schemaResolver, GenerationLanguage.TypeScript)

    protected override TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable)
        => TypeInfo.Collection("Array", itemType, isNullable, Language);

    protected override TypeInfo CreateDictionaryTypeInfo(TypeInfo keyType, TypeInfo valueType, bool isNullable)
        => TypeInfo.Dictionary("Record", keyType, valueType, isNullable, Language);

    protected override TypeInfo CreateDefaultTypeInfo(bool isNullable)
    {
        var typeInfo = TypeInfo.Simple("any", language: Language);
        return isNullable ? TypeInfo.Nullable(typeInfo) : typeInfo;
    }
}
```

**Почему удалить:** `TypeInfo` удаляется полностью (заменяется `IrType`). `BaseTypeInfoBuilder` строит `TypeInfo` из `IOpenApiSchema` — вся эта цепочка заменяется `IrBuilder.BuildType(IOpenApiSchema) → IrType`. TypeScript-специфичная логика (`Array`, `Record`, `any`) — часть `TypeScriptIrTypeMapper.MapType(IrType)`.

---

### 12.5 TypeScriptTypeMapper.cs — legacy ITypeMapper реализация

```csharp
// ValIO.Generators.TypeScript/TypeScriptTypeMapper.cs

public class TypeScriptTypeMapper : BaseTypeMapper
{
    public TypeScriptTypeMapper(INamingConverter namingConverter)
        : base(namingConverter, GenerationLanguage.TypeScript) { }

    // Работает с IOpenApiSchema — обходит IR!
    public override string MapType(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        // Разрешает $ref → имя типа
        // Обрабатывает allOf → "TypeA & TypeB"
        // Обрабатывает oneOf/anyOf → "TypeA | TypeB"
        // Обрабатывает object с properties → рекурсивно
        // Обрабатывает array → "Array<ItemType>"
        // Обрабатывает string/number/boolean/integer → примитивные TS типы
    }

    public override string MapPropertyType(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    // + MapEnumType, MapTypeInternal, примитивный маппинг
}
```

**Почему удалить:** Этот класс реализует `ITypeMapper` — интерфейс, работающий с `IOpenApiSchema` напрямую, минуя IR. Это **нарушение архитектурного слоя** — TypeScript-генератор не должен знать об OpenAPI-схеме, он должен работать только с IR.

**Заменяется на:** `TypeScriptIrTypeMapper : IIrTypeMapper` — работает с `IrType`, возвращает `TypeNode` (AST нода), а не строку. Подробнее см. `07_IR_NODES_AND_BUILDERS.md`.

```csharp
// Новый интерфейс (ValIO.Core/Interfaces/IIrTypeMapper.cs):
public interface IIrTypeMapper
{
    TypeNode MapType(IrType irType);           // IrType → TypeNode (AST нода)
    bool IsNullable(IrType irType);            // Является ли тип nullable
    bool IsOptional(IrType irType);            // Является ли тип optional
}

// Новая реализация (ValIO.Generators.TypeScript/TypeScriptIrTypeMapper.cs):
public sealed class TypeScriptIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p    => MapPrimitive(p.Kind),
        IrModelReference ref => new SimpleTypeNode(ref.ModelName),
        IrArrayType arr      => new GenericTypeNode("Array", [MapType(arr.ElementType)]),
        IrDictionaryType d   => new GenericTypeNode("Record", [MapType(d.KeyType), MapType(d.ValueType)]),
        IrNullableType n     => MapType(n.InnerType),  // TS: nullable через | null в конце
        IrOptionalType o     => MapType(o.InnerType),  // TS: optional через "?" в property
        IrUnionType u        => new UnionTypeNode(u.Types.Select(MapType).ToList()),
        IrIntersectionType i => new IntersectionTypeNode(i.Types.Select(MapType).ToList()),
        _ => throw new ArgumentException($"Unknown IrType: {irType.GetType().Name}")
    };

    private static SimpleTypeNode MapPrimitive(IrPrimitiveKind kind) => kind switch
    {
        IrPrimitiveKind.String    => new SimpleTypeNode("string"),
        IrPrimitiveKind.Number    => new SimpleTypeNode("number"),
        IrPrimitiveKind.Integer   => new SimpleTypeNode("number"),
        IrPrimitiveKind.Long      => new SimpleTypeNode("number"),
        IrPrimitiveKind.Boolean   => new SimpleTypeNode("boolean"),
        IrPrimitiveKind.DateTime  => new SimpleTypeNode("string"),  // ISO-8601 строка
        IrPrimitiveKind.Date      => new SimpleTypeNode("string"),
        IrPrimitiveKind.Time      => new SimpleTypeNode("string"),
        IrPrimitiveKind.Binary    => new SimpleTypeNode("Blob"),
        IrPrimitiveKind.ByteArray => new SimpleTypeNode("string"),  // base64
        IrPrimitiveKind.Uuid      => new SimpleTypeNode("string"),
        IrPrimitiveKind.Any       => new SimpleTypeNode("unknown"),
        IrPrimitiveKind.Void      => new SimpleTypeNode("void"),
        IrPrimitiveKind.File      => new SimpleTypeNode("File"),
        _ => new SimpleTypeNode("unknown")
    };
}
```

---

### 12.6 ServiceCollectionExtensions — что убрать из DI регистрации

После удаления всех TS-специфичных legacy классов нужно убрать их регистрацию из `ValIO/ServiceRegistration/ServiceCollectionExtensions.cs`.

**Текущее состояние `AddTypeScriptGenerators()` — что содержит активные legacy регистрации:**

```csharp
// ValIO/ServiceRegistration/ServiceCollectionExtensions.cs

private static IServiceCollection AddTypeScriptGenerators(this IServiceCollection services)
{
    // ← УДАЛИТЬ: TypeScriptTypeMapper (legacy ITypeMapper реализация)
    services.AddSingleton<TypeScriptTypeMapper>();
    services.AddSingleton<ITypeMapper>(sp => sp.GetRequiredService<TypeScriptTypeMapper>());

    // ← УДАЛИТЬ: InterfaceModelRenderer (TemplateData рендерер)
    services.AddSingleton<InterfaceModelRenderer>();
    services.AddSingleton<IModelFormatRenderer>(sp => sp.GetRequiredService<InterfaceModelRenderer>());

    // ← УДАЛИТЬ: TypeScriptEnumRenderer и TypeScriptUnionTypeRenderer
    services.AddSingleton<TypeScriptEnumRenderer>();
    services.AddSingleton<TypeScriptUnionTypeRenderer>();
    services.AddSingleton<IEnumFormatRenderer>(sp => sp.GetRequiredService<TypeScriptEnumRenderer>());
    services.AddSingleton<IEnumFormatRenderer>(sp => sp.GetRequiredService<TypeScriptUnionTypeRenderer>());

    // ← УДАЛИТЬ: TypeScriptDependencyAnalyzer (legacy BaseDependencyAnalyzer)
    services.AddSingleton<TypeScriptDependencyAnalyzer>();
    services.AddSingleton<IDependencyAnalyzer>(sp => sp.GetRequiredService<TypeScriptDependencyAnalyzer>());

    // ← УДАЛИТЬ: TypeScriptEnumExtractor (legacy BaseEnumExtractor)
    services.AddSingleton<TypeScriptEnumExtractor>();
    services.AddSingleton<IEnumExtractor>(sp => sp.GetRequiredService<TypeScriptEnumExtractor>());

    // ← УДАЛИТЬ: TypeScriptOperationNamingStrategy (legacy IOperationNamingStrategy)
    services.AddSingleton<IOperationNamingStrategy, TypeScriptOperationNamingStrategy>();

    // ← УДАЛИТЬ: TypeScriptTypeInfoBuilder (legacy TypeInfo builder)
    services.AddSingleton<TypeScriptTypeInfoBuilder>();
    services.AddSingleton<ITypeInfoBuilder>(sp => sp.GetRequiredService<TypeScriptTypeInfoBuilder>());

    // ← ОСТАВИТЬ (активный пайплайн):
    services.AddSingleton<TypeScriptAstBuilder>();
    services.AddSingleton<TypeScriptAstRenderer>();
    services.AddSingleton<TypeScriptCodeGenerator>();
    services.AddSingleton<ICodeGenerator, TypeScriptCodeGenerator>();
    services.AddSingleton<ZodSchemaRenderer>();
    services.AddSingleton<TanStackQueryRenderer>();
    // ...
}
```

**После рефакторинга** `AddTypeScriptGenerators()` полностью заменяется плагинной регистрацией через `TypeScriptPlugin.ConfigureServices(services)`. Метод `AddTypeScriptGenerators` удаляется.

---

### 12.7 Также удалить: закомментированные регистрации в ServiceCollectionExtensions

В `ServiceCollectionExtensions.cs` есть большой закомментированный блок (~50 строк) с регистрациями старых TemplateData рендереров:

```csharp
// ЗАКОММЕНТИРОВАННЫЙ БЛОК — удалить вместе с файлом:
// services.AddSingleton<IModelFormatRenderer, ...>();
// services.AddSingleton<IEnumFormatRenderer, ...>();
// zodModelRenderer = ...
// zodEnumRenderer = ...
// services.AddSingleton<IQueryWrapperRenderer, ...>();
```

Этот блок — артефакт перехода с TemplateData на IR/AST. Его наличие вводит в заблуждение при чтении кода. Комментарий удаляется вместе с файлом (файл будет заменён плагинной регистрацией).

---

### 12.8 Сводная таблица — TypeScript legacy к удалению

| Файл | Категория | Заменяется |
|------|-----------|-----------|
| `Models/CodeGeneration/CodeBlock.cs` | Строковый генератор кода | `BlockStatementNode` + `TypeScriptAstRenderer` |
| `Models/CodeGeneration/FunctionParameter.cs` | Строковый генератор кода | `FunctionDeclarationNode` |
| `Models/CodeGeneration/FunctionParameterBuilder.cs` | Builder для legacy | Удаляется |
| `Models/CodeGeneration/FunctionSignature.cs` | Строковый генератор кода | `FunctionDeclarationNode` + `TypeScriptAstRenderer` |
| `Models/CodeGeneration/OptionsDefinition.cs` | Строковый генератор кода | `PropertySignatureNode` / `ObjectLiteralExpressionNode` |
| `Models/CodeGeneration/OptionsDefinitionBuilder.cs` | Builder для legacy | Удаляется |
| `Models/CodeGeneration/TypeDefinition.cs` | Строковый генератор кода | `InterfaceDeclarationNode` + `PropertySignatureNode` |
| `Models/CodeGeneration/TypeDefinitionBuilder.cs` | Builder для legacy | Удаляется |
| `Models/ApiTemplateData.cs` | TypeScript-специфичный TemplateData | `IrClient` → `AxiosAstBuilder`/`FetchAstBuilder` |
| `Models/InterfaceTemplateData.cs` | TypeScript-специфичный TemplateData | `IrModel` → `TypeScriptAstBuilder` |
| `Models/TypeScriptClientTemplateDataOptions.cs` | TemplateData Options | Удаляется вместе с TemplateData |
| `Models/TypeScriptModelTemplateDataOptions.cs` | TemplateData Options | Удаляется вместе с TemplateData |
| `Models/TypeScriptEnumTemplateDataOptions.cs` | TemplateData Options | Удаляется вместе с TemplateData |
| `Models/TypeScriptOperationTemplateDataOptions.cs` | TemplateData Options | Удаляется вместе с TemplateData |
| `Models/TypeScriptPropertyTemplateDataOptions.cs` | TemplateData Options | Удаляется вместе с TemplateData |
| `Renderers/InterfaceModelRenderer.cs` | TemplateData рендерер | `TypeScriptAstRenderer.VisitInterfaceDeclaration()` |
| `Renderers/TypeScriptEnumRenderer.cs` | TemplateData рендерер | `TypeScriptAstRenderer` для enum |
| `Renderers/TypeScriptUnionTypeRenderer.cs` | TemplateData рендерер | `TypeScriptAstRenderer` для union type |
| `Services/TypeScriptDependencyAnalyzer.cs` | Legacy сервис | `IrTypeDependencyGraph` в IR |
| `Services/TypeScriptEnumExtractor.cs` | Legacy сервис | `IrBuilder.ExtractEnums()` |
| `Services/TypeScriptOperationNamingStrategy.cs` | Legacy сервис | `IrBuilder` + `INamingConverter` |
| `Services/TypeScriptTypeInfoBuilder.cs` | Legacy сервис | `IrBuilder.BuildType()` + `IrType` |
| `TypeScriptTypeMapper.cs` | Legacy ITypeMapper | `TypeScriptIrTypeMapper : IIrTypeMapper` |

**Итого: 23 файла к удалению** в TypeScript-генераторе (в дополнение к 30+ файлам в Core и других проектах из секций 2-10).
