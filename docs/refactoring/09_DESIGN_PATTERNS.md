# 09 — Применение паттернов проектирования (GoF)

> **Этап:** Сквозной (применяется на всех этапах рефакторинга)  
> **Приоритет:** Архитектурный  
> **Зависит от:** 01_AST_REFACTORING, 02_PLUGIN_ARCHITECTURE, 07_IR_NODES_AND_BUILDERS, 08_AST_BUILDERS_DEEP

---

## Содержание

1. [Visitor — AST dispatch и рендеринг](#1-visitor--ast-dispatch-и-рендеринг)
2. [Chain of Responsibility — расширяемый рендерер](#2-chain-of-responsibility--расширяемый-рендерер)
3. [Strategy — IIrTypeMapper, IGeneratorPlugin](#3-strategy--iiirtypemapper-igeneratorplugin)
4. [Builder — AstBuilder и IrBuilder](#4-builder--astbuilder-и-irbuilder)
5. [Template Method — базовые классы шагов и билдеров](#5-template-method--базовые-классы-шагов-и-билдеров)
6. [Command — ICodeGenerationStep](#6-command--icodeGenerationstep)
7. [Composite — AstNode дерево и SourceFileNode](#7-composite--astnode-дерево-и-sourcefilenode)
8. [Prototype — AstNode.Clone()](#8-prototype--astnodeclone)
9. [Iterator — обход AST дерева](#9-iterator--обход-ast-дерева)
10. [Facade — GenerationOrchestrator](#10-facade--generationorchestrator)
11. [State — GenerationContext и накопление состояния](#11-state--generationcontext-и-накопление-состояния)
12. [Proxy — кэширующий ISchemaResolver](#12-proxy--кэширующий-ischemaresolver)
13. [Сводная таблица паттернов](#13-сводная-таблица-паттернов)

---

## 1. Visitor — AST dispatch и рендеринг

### Где применяется

Visitor — **фундаментальный паттерн** ValIO. Весь рендеринг кода, вся трансформация AST, весь анализ зависимостей — через Visitor.

**Файлы:**
- `ValIO.Core/Ast/Visitors/BaseAstVisitor.cs` — базовый visitor
- `ValIO.Generators.TypeScript/Ast/Visitors/TypeScriptAstVisitorBase.cs`
- `ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs`
- `ValIO.Generators.TypeScript/Zod/Ast/Visitors/ZodAstVisitorBase.cs`

### Как должно работать (GoF double-dispatch)

```csharp
// Нода САМА решает, какой метод Visitor'а вызвать:
public sealed class SimpleTypeNode : TypeNode
{
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitSimpleType(this);   // ← конкретный Visit метод
}

public sealed class UnionTypeNode : TypeNode
{
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitUnionType(this);
}

// Visitor не нужен switch — dispatch автоматический:
public class TypeScriptAstRenderer : BaseAstVisitor<string>
{
    // Рендерим простой тип
    public override string VisitSimpleType(SimpleTypeNode node)
        => node.Name;  // "string", "number", "UserDto"

    // Рендерим union
    public override string VisitUnionType(UnionTypeNode node)
        => string.Join(" | ", node.Types.Select(t => t.Accept(this)));

    // Рендерим nullable
    public override string VisitNullableType(NullableTypeNode node)
        => $"{node.InnerType.Accept(this)} | null";

    // Рендерим generic
    public override string VisitGenericType(GenericTypeNode node)
    {
        var args = string.Join(", ", node.TypeArguments.Select(a => a.Accept(this)));
        return $"{node.Name}<{args}>";  // "Array<UserDto>", "Record<string, number>"
    }
}
```

### Расширение Visitor для стек-специфичных нод

Ключевой вопрос: как Zod-специфичная нода (`ZodSchemaNode`) попадает в Zod-специфичный Visitor, если `Accept` принимает `IAstVisitor<TResult>`?

**Решение через interface type check в Accept:**

```csharp
// ZodSchemaNode — специфична для Zod, но Accept принимает любой visitor
public sealed class ZodSchemaNode : ExpressionNode
{
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        // Если visitor знает о Zod — вызываем Zod-метод (double-dispatch)
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
            return zodVisitor.VisitZodSchema(this);

        // Иначе — базовое поведение (DefaultVisit или игнор)
        return visitor.DefaultVisit(this);
    }
}
```

**Иерархия Visitor интерфейсов:**

```
IAstVisitor<TResult>               — базовые ноды (Core)
    ↑ extends
ITypeScriptAstVisitor<TResult>     — TypeScript-специфичные ноды
    ↑ extends
IZodAstVisitor<TResult>            — Zod-специфичные ноды
ITanStackQueryAstVisitor<TResult>  — TanStack-специфичные ноды
IAxiosAstVisitor<TResult>          — Axios-специфичные ноды
ICSharpAstVisitor<TResult>         — C#-специфичные ноды
```

Конкретная нода `ZodObjectNode` проверяет `visitor is IZodAstVisitor<TResult>`. Если `TypeScriptAstRenderer` не реализует `IZodAstVisitor` — вызывается `DefaultVisit`, и Chain of Responsibility в `DefaultVisit` передаёт управление `ZodNodeRenderer`.

### BaseAstVisitor — реализация с DefaultVisit

```csharp
// ValIO.Core/Ast/Visitors/BaseAstVisitor.cs — после рефакторинга

public abstract class BaseAstVisitor<TResult>
{
    // Точка входа для обхода
    public virtual TResult Visit(AstNode node) => node.Accept(this);

    // По умолчанию — вызов DefaultVisit для неизвестных нод
    // Переопределяется в рендерерах для Chain of Responsibility
    protected virtual TResult DefaultVisit(AstNode node)
        => throw new NotSupportedException(
            $"{GetType().Name} does not know how to visit {node.GetType().Name}");

    // Все Visit методы — virtual (не abstract), чтобы не заставлять
    // подклассы реализовывать методы для нод, с которыми они не работают
    public virtual TResult VisitSimpleType(SimpleTypeNode node)       => DefaultVisit(node);
    public virtual TResult VisitGenericType(GenericTypeNode node)     => DefaultVisit(node);
    public virtual TResult VisitUnionType(UnionTypeNode node)         => DefaultVisit(node);
    public virtual TResult VisitNullableType(NullableTypeNode node)   => DefaultVisit(node);
    public virtual TResult VisitArrayType(ArrayTypeNode node)         => DefaultVisit(node);
    public virtual TResult VisitCallExpression(CallExpressionNode n)  => DefaultVisit(n);
    public virtual TResult VisitIdentifier(IdentifierNode node)       => DefaultVisit(node);
    public virtual TResult VisitReturnStatement(ReturnStatementNode n) => DefaultVisit(n);
    public virtual TResult VisitIfStatement(IfStatementNode node)     => DefaultVisit(node);
    // ... все остальные Visit методы
}
```

**Преимущества подхода:**
- Добавление новой ноды = добавить метод в интерфейс + `Accept` в ноде (без изменения `BaseAstVisitor`)
- Visitor не обязан реализовывать все методы — только те, которые ему нужны
- `DefaultVisit` позволяет Chain of Responsibility подхватить необработанные ноды

---

## 2. Chain of Responsibility — расширяемый рендерер

### Где применяется

`TypeScriptAstRenderer` использует Chain of Responsibility для обработки стек-специфичных нод (Zod, Axios, TanStack) без знания о них напрямую.

**Файлы:**
- `ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs` — корень цепи
- `ValIO.Generators.TypeScript/Zod/Ast/Renderers/ZodNodeRenderer.cs` — обработчик Zod
- `ValIO.Generators.TypeScript/Axios/Ast/Renderers/AxiosNodeRenderer.cs` — обработчик Axios
- `ValIO.Generators.TypeScript/TanStackQuery/Ast/Renderers/TanStackNodeRenderer.cs` — обработчик TanStack

### Контракт и реализация

```csharp
// Контракт звена цепи:
// ValIO.Generators.TypeScript/Ast/Renderers/ITypeScriptNodeRenderer.cs

public interface ITypeScriptNodeRenderer
{
    /// <summary>
    /// Приоритет в цепи. Меньше = раньше вызывается.
    /// Позволяет переопределять стандартные рендереры.
    /// </summary>
    int Priority { get; }

    /// <summary>Может ли этот рендерер обработать ноду</summary>
    bool CanRender(AstNode node);

    /// <summary>
    /// Рендерит ноду. Может рекурсивно вызывать baseRenderer для дочерних нод.
    /// </summary>
    string Render(AstNode node, TypeScriptAstRenderer baseRenderer);
}
```

```csharp
// Корень цепи — TypeScriptAstRenderer:

public sealed class TypeScriptAstRenderer : BaseAstVisitor<string>
{
    // Отсортированные по Priority обработчики (ImmutableArray для thread safety)
    private readonly ImmutableArray<ITypeScriptNodeRenderer> _chain;

    public TypeScriptAstRenderer(IEnumerable<ITypeScriptNodeRenderer>? extensions = null)
    {
        _chain = (extensions ?? [])
            .OrderBy(e => e.Priority)
            .ToImmutableArray();
    }

    // Все стандартные TypeScript ноды — через Visitor (без цепи):
    public override string VisitSimpleType(SimpleTypeNode node) => node.Name;
    public override string VisitUnionType(UnionTypeNode node)
        => string.Join(" | ", node.Types.Select(t => Visit(t)));
    // ...

    // DefaultVisit = передать в цепь
    protected override string DefaultVisit(AstNode node)
    {
        // Пройти по цепи — первый кто может обработать — обрабатывает
        foreach (var handler in _chain)
        {
            if (handler.CanRender(node))
                return handler.Render(node, this);
        }

        throw new NotSupportedException(
            $"TypeScriptAstRenderer: no handler for {node.GetType().Name}. " +
            $"Register an ITypeScriptNodeRenderer for this node type.");
    }
}
```

```csharp
// Звено цепи для Zod нод:

public sealed class ZodNodeRenderer : ITypeScriptNodeRenderer
{
    public int Priority => 100;  // Стандартный приоритет

    public bool CanRender(AstNode node) => node is ZodSchemaNode;

    public string Render(AstNode node, TypeScriptAstRenderer baseRenderer)
        => node switch
        {
            ZodObjectNode obj       => RenderObject(obj, baseRenderer),
            ZodArrayNode arr        => $"z.array({baseRenderer.Visit(arr.ElementSchema)})",
            ZodUnionNode u          => $"z.union([{string.Join(", ", u.Schemas.Select(s => baseRenderer.Visit(s)))}])",
            ZodIntersectionNode i   => $"z.intersection({string.Join(", ", i.Schemas.Select(s => baseRenderer.Visit(s)))})",
            ZodPrimitiveSchemaNode p => RenderPrimitive(p),
            ZodNullableNode n       => $"{baseRenderer.Visit(n.InnerSchema)}.nullable()",
            ZodOptionalNode o       => $"{baseRenderer.Visit(o.InnerSchema)}.optional()",
            ZodEffectsNode e        => $"{baseRenderer.Visit(e.Schema)}.transform({baseRenderer.Visit(e.Transform)})",
            _ => throw new NotSupportedException($"ZodNodeRenderer: {node.GetType().Name}")
        };

    private string RenderObject(ZodObjectNode node, TypeScriptAstRenderer renderer)
    {
        if (node.Properties.Count == 0) return "z.object({})";

        var props = node.Properties
            .Select(p => $"  {p.Name}: {renderer.Visit(p.Schema)}")
            .ToList();

        return $"z.object({{\n{string.Join(",\n", props)}\n}})";
    }

    private static string RenderPrimitive(ZodPrimitiveSchemaNode node) => node.Kind switch
    {
        ZodSchemaKind.String  => "z.string()",
        ZodSchemaKind.Number  => "z.number()",
        ZodSchemaKind.Boolean => "z.boolean()",
        ZodSchemaKind.Date    => "z.coerce.date()",
        ZodSchemaKind.Any     => "z.unknown()",
        ZodSchemaKind.Null    => "z.null()",
        _ => "z.unknown()"
    };
}
```

**DI регистрация:**

```csharp
// TypeScriptGeneratorPlugin.ConfigureServices():

services.AddSingleton<ITypeScriptNodeRenderer, ZodNodeRenderer>();
services.AddSingleton<ITypeScriptNodeRenderer, AxiosNodeRenderer>();
services.AddSingleton<ITypeScriptNodeRenderer, TanStackNodeRenderer>();
services.AddSingleton<ITypeScriptNodeRenderer, MswNodeRenderer>();

// При регистрации TypeScriptAstRenderer:
services.AddSingleton<TypeScriptAstRenderer>(sp =>
    new TypeScriptAstRenderer(sp.GetServices<ITypeScriptNodeRenderer>()));
```

**Добавление нового стека (например Ky):**

```csharp
// Новый обработчик:
public sealed class KyNodeRenderer : ITypeScriptNodeRenderer
{
    public int Priority => 100;
    public bool CanRender(AstNode node) => node is KyCallNode;
    public string Render(AstNode node, TypeScriptAstRenderer baseRenderer) => ...;
}

// Регистрация — единственное изменение:
services.AddSingleton<ITypeScriptNodeRenderer, KyNodeRenderer>();
// TypeScriptAstRenderer, BaseAstVisitor, Core — не меняются
```

---

## 3. Strategy — IIrTypeMapper, IGeneratorPlugin

### 3.1 Strategy: IIrTypeMapper

Каждый язык имеет свою стратегию маппинга IR типов в AST ноды.

```csharp
// Контракт стратегии:
public interface IIrTypeMapper
{
    TypeNode MapType(IrType irType);
    bool IsOptionalInLanguage(IrProperty property);
    string? GetImportTarget(IrType irType);
}

// Стратегия TypeScript:
public sealed class TypeScriptIrTypeMapper : IIrTypeMapper { ... }

// Стратегия C#:
public sealed class CSharpIrTypeMapper : IIrTypeMapper { ... }

// Стратегия PHP (для будущего плагина):
public sealed class PhpIrTypeMapper : IIrTypeMapper { ... }

// Контекст использования — TypeScriptAstBuilder:
public sealed class TypeScriptModelAstBuilder
{
    private readonly IIrTypeMapper _typeMapper;  // ← инжектируется стратегия

    public TypeScriptModelAstBuilder(IIrTypeMapper typeMapper) // ← DI
        => _typeMapper = typeMapper;

    public InterfaceDeclarationNode BuildInterface(IrModel model)
    {
        var properties = model.Properties
            .Select(p => new PropertySignatureNode(
                name:       p.Name,
                type:       _typeMapper.MapType(p.Type),  // ← вызов стратегии
                isOptional: _typeMapper.IsOptionalInLanguage(p)))
            .ToList();

        return new InterfaceDeclarationNode(model.Name, properties);
    }
}
```

### 3.2 Strategy: IGeneratorPlugin как стратегия выбора поведения

```csharp
// IGeneratorPlugin — стратегия генерации для конкретного языка/стека
public interface IGeneratorPlugin
{
    string LanguageId { get; }
    ICodeGenerationPipeline CreatePipeline(GenerationContext ctx, IServiceProvider services);
    void ConfigureServices(IServiceCollection services);
}

// Контекст — GeneratorRegistry (реестр стратегий):
public sealed class GeneratorRegistry : IGeneratorRegistry
{
    private readonly IReadOnlyDictionary<string, IGeneratorPlugin> _byId;

    // Выбрать стратегию по идентификатору языка:
    public IGeneratorPlugin? FindPlugin(string languageId)
        => _byId.GetValueOrDefault(languageId.ToLowerInvariant());
}

// Использование — в GenerateCommand:
var plugin = _registry.FindPlugin(config.LanguageId)
    ?? throw new NotSupportedException($"No plugin for: {config.LanguageId}");

var pipeline = plugin.CreatePipeline(context, _services); // ← выбор стратегии
await pipeline.ExecuteAsync(context, ct);
```

### 3.3 Strategy: шаги как стратегии генерации отдельных артефактов

```csharp
// Каждый Step — стратегия для своего типа артефактов
public interface ICodeGenerationStep
{
    bool ShouldExecute(GenerationContext ctx);
    Task<IReadOnlyDictionary<string, string>> ExecuteAsync(GenerationContext ctx, CancellationToken ct);
}

// Разные стратегии для разных транспортов:
ICodeGenerationStep fetchStep  = new FetchClientStep(...);
ICodeGenerationStep axiosStep  = new AxiosClientStep(...);
ICodeGenerationStep kyStep     = new KyClientStep(...);
// Все реализуют один интерфейс — взаимозаменяемы
```

---

## 4. Builder — AstBuilder и IrBuilder

### 4.1 Builder: IrBuilder строит сложный IrDocument

`IrBuilder` — классический Builder для сложного объекта `IrDocument`. Построение разбито на независимые этапы.

```csharp
// ValIO.Core/IR/Builders/IrBuilder.cs

public sealed class IrBuilder
{
    // Состояние в процессе построения
    private readonly Dictionary<string, IrModel>  _models   = new();
    private readonly Dictionary<string, IrEnum>   _enums    = new();
    private readonly Dictionary<string, IrClient> _clients  = new();
    private readonly List<IrModel>                _inline   = new();

    // Director вызывает метод-построитель:
    public IrDocument BuildFromOpenApi(OpenApiDocument document)
    {
        // Шаг 1: Извлечь и построить модели
        foreach (var (name, schema) in document.Components?.Schemas ?? [])
        {
            if (schema.IsEnum())
                _enums[name] = BuildEnum(name, schema);
            else
                _models[name] = BuildModel(name, schema);
        }

        // Шаг 2: Обработать inline модели (созданные при рекурсивном обходе)
        foreach (var inline in _inline)
            _models[inline.Name] = inline;

        // Шаг 3: Извлечь клиентов из paths
        foreach (var (path, item) in document.Paths ?? [])
            ExtractOperations(path, item);

        // Шаг 4: Построить граф зависимостей
        var graph = BuildDependencyGraph(_models.Values);

        // Шаг 5: Собрать финальный IrDocument
        return new IrDocument
        {
            Models           = _models.Values.ToImmutableList(),
            Enums            = _enums.Values.ToImmutableList(),
            Clients          = _clients.Values.ToImmutableList(),
            DependencyGraph  = graph
        };
    }

    // Частные построители для подобъектов:
    private IrModel BuildModel(string name, IOpenApiSchema schema) { ... }
    private IrEnum  BuildEnum(string name, IOpenApiSchema schema)  { ... }
    private IrType  BuildType(IOpenApiSchema schema)               { ... }
    private IrTypeDependencyGraph BuildDependencyGraph(...)        { ... }
}
```

### 4.2 Builder: Fluent Builder для AST нод

Для сложных AST нод использовать Fluent Builder вместо конструктора с 10+ параметрами:

```csharp
// БЫЛО (неудобно):
new FunctionDeclarationNode(
    name: "getUser",
    parameters: [new ParameterNode("id", new SimpleTypeNode("string"))],
    returnType: new GenericTypeNode("Promise", [new SimpleTypeNode("User")]),
    isAsync: true,
    isExported: true,
    body: [returnStmt],
    typeParameters: null,
    jsDoc: null,
    metadata: ImmutableDictionary<string, object>.Empty);

// СТАЛО (Fluent Builder):
FunctionDeclarationNode.Builder("getUser")
    .WithParameter("id", "string")
    .WithReturnType("Promise<User>")
    .WithAsync()
    .WithExport()
    .WithBody(returnStmt)
    .Build();
```

```csharp
// Реализация Fluent Builder:

public sealed class FunctionDeclarationNode : DeclarationNode
{
    // Вложенный Builder-класс
    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<ParameterNode> _params = [];
        private TypeNode? _returnType;
        private bool _isAsync;
        private bool _isExported;
        private readonly List<StatementNode> _body = [];
        private string? _jsDoc;

        public Builder(string name) => _name = name;

        public Builder WithParameter(string name, TypeNode type, bool optional = false)
        {
            _params.Add(new ParameterNode(name, type, optional));
            return this;
        }

        public Builder WithParameter(string name, string typeName, bool optional = false)
            => WithParameter(name, new SimpleTypeNode(typeName), optional);

        public Builder WithReturnType(TypeNode type)  { _returnType = type; return this; }
        public Builder WithReturnType(string typeName) => WithReturnType(new SimpleTypeNode(typeName));

        public Builder WithAsync()   { _isAsync    = true; return this; }
        public Builder WithExport()  { _isExported = true; return this; }

        public Builder WithBody(params StatementNode[] stmts)
        {
            _body.AddRange(stmts);
            return this;
        }

        public Builder WithJsDoc(string doc) { _jsDoc = doc; return this; }

        public FunctionDeclarationNode Build() => new(
            name:       new IdentifierNode(_name),
            parameters: _params.ToImmutableList(),
            returnType: _returnType,
            isAsync:    _isAsync,
            isExported: _isExported,
            body:       _body.ToImmutableList(),
            jsDoc:      _jsDoc);
    }

    public static Builder Create(string name) => new(name);
}

// Использование в TypeScriptAstBuilder:
var fn = FunctionDeclarationNode.Create("getUser")
    .WithParameter("id", "string")
    .WithReturnType(new GenericTypeNode("Promise", [new SimpleTypeNode("User")]))
    .WithAsync()
    .WithExport()
    .WithBody(new ReturnStatementNode(callExpression))
    .WithJsDoc("Fetch user by ID")
    .Build();
```

### 4.3 Builder: SourceFileBuilder для сборки файла

```csharp
// Утилитный Builder для построения SourceFileNode:

public sealed class SourceFileBuilder
{
    private readonly List<ImportNode> _imports = [];
    private readonly List<AstNode>   _body    = [];
    private readonly HashSet<string> _importedNames = [];  // дедупликация

    public SourceFileBuilder AddImport(string from, params string[] names)
    {
        foreach (var name in names)
            _importedNames.Add(name);
        _imports.Add(new ImportNode(from, names.ToImmutableList()));
        return this;
    }

    // Умный импорт: добавляет только если имя ещё не импортировано
    public SourceFileBuilder EnsureImport(string from, string name)
    {
        if (!_importedNames.Contains(name))
            AddImport(from, name);
        return this;
    }

    public SourceFileBuilder Add(AstNode node)
    {
        _body.Add(node);
        return this;
    }

    public SourceFileNode Build()
    {
        // Объединить импорты по пути:
        var mergedImports = _imports
            .GroupBy(i => i.From)
            .Select(g => new ImportNode(
                from:  g.Key,
                names: g.SelectMany(i => i.Names).Distinct().ToImmutableList()))
            .OrderBy(i => i.From)
            .ToImmutableList();

        return new SourceFileNode(mergedImports, _body.ToImmutableList());
    }
}

// Использование в TypeScriptModelAstBuilder:
var file = new SourceFileBuilder()
    .EnsureImport("../models/user", "User")
    .EnsureImport("../models/address", "Address")
    .Add(interfaceNode)
    .Build();
```

---

## 5. Template Method — базовые классы шагов и билдеров

### 5.1 Template Method: ZodSchemasStepBase

Базовый класс определяет **скелет алгоритма**. Подклассы переопределяют только версию Zod.

```csharp
// Абстрактный базовый шаг (Template Method):
public abstract class ZodSchemasStepBase : ICodeGenerationStep
{
    public int Order => 30;

    // Шаблонный метод — алгоритм фиксирован, варьируется только ZodVersion
    public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
        GenerationContext ctx,
        CancellationToken ct = default)
    {
        var files = new Dictionary<string, string>();

        // Шаг 1: отсортировать по зависимостям (фиксирован)
        var models = OrderByDependencies(ctx.IrDocument.Models, ctx.IrDocument.DependencyGraph);

        foreach (var model in models)
        {
            ct.ThrowIfCancellationRequested();

            // Шаг 2: построить схему (ВАРЬИРУЕТСЯ — abstract)
            var schemaNode = BuildSchema(model, ctx);

            // Шаг 3: рендеринг (фиксирован)
            var code = _renderer.Render(schemaNode);

            // Шаг 4: путь к файлу (ВАРЬИРУЕТСЯ — виртуальный)
            var path = GetFilePath(model.Name, ctx.Configuration.OutputPath);
            files[path] = code;
        }

        // Шаг 5: index файл (ВАРЬИРУЕТСЯ — виртуальный)
        var indexPath = GetIndexPath(ctx.Configuration.OutputPath);
        files[indexPath] = BuildIndex(models);

        return files;
    }

    // Примитивные операции (abstract/virtual) — переопределяются подклассами:
    protected abstract AstNode BuildSchema(IrModel model, GenerationContext ctx);
    protected abstract string ValidationLibraryId { get; }

    protected virtual string GetFilePath(string modelName, string outputPath)
        => Path.Combine(outputPath, "schemas", $"{ToKebabCase(modelName)}.schema.ts");

    protected virtual string GetIndexPath(string outputPath)
        => Path.Combine(outputPath, "schemas", "index.ts");

    // Hook method — подкласс может расширить (по умолчанию пустой):
    protected virtual void OnSchemaBuilt(AstNode schema, IrModel model) { }
}

// Конкретные подклассы — только переопределяют варьируемые части:
public sealed class Zod3SchemasStep : ZodSchemasStepBase
{
    protected override string ValidationLibraryId => "zod3";
    public override string Name => "Zod v3 Schemas";

    protected override AstNode BuildSchema(IrModel model, GenerationContext ctx)
        => _zodBuilder.BuildSchema(model, ZodVersion.V3);
}

public sealed class Zod4SchemasStep : ZodSchemasStepBase
{
    protected override string ValidationLibraryId => "zod4";
    public override string Name => "Zod v4 Schemas";

    protected override AstNode BuildSchema(IrModel model, GenerationContext ctx)
        => _zodBuilder.BuildSchema(model, ZodVersion.V4);

    // Zod v4 использует другой путь к файлу (.v4.schema.ts):
    protected override string GetFilePath(string modelName, string outputPath)
        => Path.Combine(outputPath, "schemas", $"{ToKebabCase(modelName)}.v4.schema.ts");
}
```

### 5.2 Template Method: BaseClientAstBuilder

```csharp
// Базовый класс для всех HTTP транспортов:
public abstract class BaseClientAstBuilder
{
    // Template Method: фиксированный скелет построения файла клиента
    public SourceFileNode BuildClientSourceFile(IrClient client, TypeScriptGenerationOptions opts)
    {
        var file = new SourceFileBuilder();

        // Шаг 1: общие импорты (фиксирован)
        AddCommonImports(file, client, opts);

        // Шаг 2: транспорт-специфичные импорты (ВАРЬИРУЕТСЯ)
        AddTransportImports(file, opts);

        // Шаг 3: построить функции для каждой операции
        foreach (var op in client.Operations)
        {
            // Шаг 3а: общая сигнатура (фиксирована)
            var signature = BuildOperationSignature(op);

            // Шаг 3б: тело операции (ВАРЬИРУЕТСЯ)
            var body = BuildOperationBody(op, opts);

            file.Add(new FunctionDeclarationNode(signature, body));
        }

        // Шаг 4: хук после построения (пустой по умолчанию)
        OnFileBuilt(file, client);

        return file.Build();
    }

    // Примитивные операции:
    protected abstract void AddTransportImports(SourceFileBuilder file, TypeScriptGenerationOptions opts);
    protected abstract IReadOnlyList<StatementNode> BuildOperationBody(IrOperation op, TypeScriptGenerationOptions opts);
    protected virtual void OnFileBuilt(SourceFileBuilder file, IrClient client) { }

    // Общий код — не переопределяется:
    private void AddCommonImports(SourceFileBuilder file, IrClient client, TypeScriptGenerationOptions opts)
    {
        // Импорты типов ответа
        foreach (var op in client.Operations)
        {
            var responseType = _typeMapper.GetImportTarget(op.Response?.BodyType);
            if (responseType != null)
                file.EnsureImport($"../models/{ToKebabCase(responseType)}", responseType);
        }
    }
}

// Конкретные транспорты — только специфическое поведение:
public sealed class AxiosAstBuilder : BaseClientAstBuilder
{
    protected override void AddTransportImports(SourceFileBuilder file, TypeScriptGenerationOptions opts)
    {
        file.AddImport("axios", "axios", "AxiosResponse", "AxiosRequestConfig");
        if (opts.Axios?.CustomInstancePath != null)
            file.AddImport(opts.Axios.CustomInstancePath, "axiosInstance");
    }

    protected override IReadOnlyList<StatementNode> BuildOperationBody(IrOperation op, TypeScriptGenerationOptions opts)
    {
        // Строим тело Axios запроса:
        // const response = await axios.get<ResponseType>('/path', config)
        // return response.data
        return BuildAxiosCall(op, opts);
    }
}

public sealed class FetchAstBuilder : BaseClientAstBuilder
{
    protected override void AddTransportImports(SourceFileBuilder file, TypeScriptGenerationOptions opts)
    {
        if (opts.Fetch?.CustomFetcherPath != null)
            file.AddImport(opts.Fetch.CustomFetcherPath, "fetcher");
    }

    protected override IReadOnlyList<StatementNode> BuildOperationBody(IrOperation op, TypeScriptGenerationOptions opts)
    {
        // Строим тело fetch запроса:
        // const response = await fetch('/path', { method: 'GET', ... })
        // return response.json() as ResponseType
        return BuildFetchCall(op, opts);
    }
}
```

---

## 6. Command — ICodeGenerationStep

### Паттерн Command в контексте шагов генерации

`ICodeGenerationStep` — классический Command:
- **Command**: `ICodeGenerationStep`
- **ConcreteCommand**: `TypeScriptModelsStep`, `AxiosClientStep`, etc.
- **Invoker**: `CodeGenerationPipeline`
- **Receiver**: `IrDocument`, `TypeScriptAstBuilder`, `TypeScriptAstRenderer`

```csharp
// Command интерфейс:
public interface ICodeGenerationStep
{
    string Name { get; }
    int Order { get; }
    bool ShouldExecute(GenerationContext ctx);
    Task<IReadOnlyDictionary<string, string>> ExecuteAsync(GenerationContext ctx, CancellationToken ct);
}

// Invoker — Pipeline:
public sealed class CodeGenerationPipeline : ICodeGenerationPipeline
{
    private readonly IReadOnlyList<ICodeGenerationStep> _steps;

    public async Task<GenerationResult> ExecuteAsync(GenerationContext ctx, CancellationToken ct)
    {
        var allFiles = new Dictionary<string, string>();

        // Invoker выполняет команды последовательно (по Order):
        foreach (var step in _steps.OrderBy(s => s.Order))
        {
            if (!step.ShouldExecute(ctx)) continue;
            var files = await step.ExecuteAsync(ctx, ct);
            foreach (var (path, content) in files)
                allFiles[path] = content;
        }

        return new GenerationResult { Files = allFiles };
    }
}
```

### Отмена выполнения и undo

Command Pattern также позволяет добавить отмену (`Undo`) — если шаг записал файл, его можно удалить при ошибке:

```csharp
// Расширенный контракт с undo поддержкой:
public interface IUndoableGenerationStep : ICodeGenerationStep
{
    /// <summary>Отменяет изменения сделанные этим шагом</summary>
    Task UndoAsync(IReadOnlyList<string> generatedFilePaths, CancellationToken ct);
}

// Pipeline с транзакционным поведением:
public sealed class TransactionalPipeline : ICodeGenerationPipeline
{
    private readonly Stack<(IUndoableGenerationStep step, IReadOnlyList<string> files)> _history = new();

    public async Task<GenerationResult> ExecuteAsync(GenerationContext ctx, CancellationToken ct)
    {
        try
        {
            foreach (var step in _steps.OrderBy(s => s.Order))
            {
                var files = await step.ExecuteAsync(ctx, ct);
                if (step is IUndoableGenerationStep undoable)
                    _history.Push((undoable, files.Keys.ToList()));
            }
            return Success();
        }
        catch
        {
            // Откат в обратном порядке
            while (_history.TryPop(out var entry))
                await entry.step.UndoAsync(entry.files, ct);
            throw;
        }
    }
}
```

---

## 7. Composite — AstNode дерево и SourceFileNode

### AstNode как Composite

AST — классический пример Composite паттерна: и листовые, и составные узлы реализуют один интерфейс.

```csharp
// Компонент (Component):
public abstract class AstNode
{
    // Дочерние узлы — у листьев пустой список
    public abstract IReadOnlyList<AstNode> Children { get; }
    public abstract TResult Accept<TResult>(IAstVisitor<TResult> visitor);
}

// Лист (Leaf) — нет детей:
public sealed class SimpleTypeNode : TypeNode
{
    public string Name { get; }
    public override IReadOnlyList<AstNode> Children => [];   // ← пусто
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitSimpleType(this);
}

// Составной (Composite) — имеет детей:
public sealed class UnionTypeNode : TypeNode
{
    public IReadOnlyList<TypeNode> Types { get; }

    // Children включает все типы union
    public override IReadOnlyList<AstNode> Children => Types.Cast<AstNode>().ToList();

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitUnionType(this);
}

// Корень дерева файла:
public sealed class SourceFileNode : AstNode
{
    public IReadOnlyList<ImportNode> Imports { get; }
    public IReadOnlyList<AstNode> Body { get; }

    public override IReadOnlyList<AstNode> Children
        => Imports.Cast<AstNode>().Concat(Body).ToList();
}
```

### Обход дерева через Children (для анализа)

Composite позволяет обходить дерево унифицированно — без различия листов и узлов:

```csharp
// Собрать все идентификаторы из дерева:
public static IEnumerable<string> CollectAllIdentifiers(AstNode root)
{
    if (root is IdentifierNode id)
        yield return id.Name;

    foreach (var child in root.Children)
        foreach (var name in CollectAllIdentifiers(child))
            yield return name;
}

// Собрать все импорты (для анализа зависимостей):
public static IReadOnlyList<string> CollectImports(SourceFileNode file)
    => file.Children
        .OfType<ImportNode>()
        .SelectMany(i => i.Names)
        .Distinct()
        .ToList();
```

---

## 8. Prototype — AstNode.Clone()

### Зачем Prototype в AST

AST ноды — immutable records. Для создания модифицированных копий нужен Clone.

Пример: при генерации TanStack Query нужно взять ноду операции из Axios-клиента и адаптировать для useQuery hook — без изменения оригинала.

```csharp
// Контракт на клонирование:
public abstract class AstNode
{
    // Prototype: создаёт копию с опциональными изменениями
    public abstract AstNode Clone();
}

// Для immutable records — Clone через with expression:
public sealed record SimpleTypeNode : TypeNode
{
    public string Name { get; init; }
    public bool IsNullable { get; init; }
    public bool IsOptional { get; init; }

    public override SimpleTypeNode Clone()
        => this with { };  // Shallow copy — для immutable достаточно

    // Параметрический Clone — создать копию с другим именем:
    public SimpleTypeNode WithName(string newName)
        => this with { Name = newName };

    // Создать nullable версию:
    public NullableTypeNode AsNullable()
        => new NullableTypeNode(this);
}

// Глубокое клонирование для составных нод:
public sealed record UnionTypeNode : TypeNode
{
    public IReadOnlyList<TypeNode> Types { get; init; }

    public override UnionTypeNode Clone()
        => this with { Types = Types.Select(t => (TypeNode)t.Clone()).ToImmutableList() };

    // Добавить тип в union:
    public UnionTypeNode WithAdditionalType(TypeNode type)
        => this with { Types = Types.Append(type).ToImmutableList() };
}
```

### Использование в TanStack Query Builder

```csharp
// TanStackQueryAstBuilder строит useQuery на основе существующей операции:
public FunctionDeclarationNode BuildQueryHook(
    FunctionDeclarationNode axiosOperation,
    IrOperation irOp)
{
    // 1. Клонируем параметры операции (Prototype)
    var hookParams = axiosOperation.Parameters
        .Select(p => (ParameterNode)p.Clone())
        .ToList();

    // 2. Модифицируем возвращаемый тип:
    //    Promise<User> → UseQueryResult<User>
    var returnType = WrapInUseQueryResult(axiosOperation.ReturnType!.Clone());

    // 3. Строим новый хук с клонированными параметрами
    return FunctionDeclarationNode.Create($"use{ToPascalCase(irOp.Name)}")
        .WithParameters(hookParams)    // ← клонированные параметры
        .WithReturnType(returnType)    // ← модифицированный тип
        .WithBody(BuildQueryBody(axiosOperation, irOp))
        .WithExport()
        .Build();
}
```

---

## 9. Iterator — обход AST дерева

### Встроенный Iterator через Children

Composite + Iterator позволяют обходить AST без знания о структуре.

```csharp
// Рекурсивный Iterator для обхода AST:
public static class AstNodeExtensions
{
    /// <summary>
    /// Обход всех нод в глубину (Depth-First, pre-order).
    /// Паттерн: Iterator (внешний, через IEnumerable)
    /// </summary>
    public static IEnumerable<AstNode> DescendantsAndSelf(this AstNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var desc in child.DescendantsAndSelf())
                yield return desc;
    }

    /// <summary>
    /// Найти все ноды заданного типа в дереве.
    /// </summary>
    public static IEnumerable<T> FindAll<T>(this AstNode root) where T : AstNode
        => root.DescendantsAndSelf().OfType<T>();

    /// <summary>
    /// Найти первую ноду по предикату.
    /// </summary>
    public static T? Find<T>(this AstNode root, Func<T, bool> predicate) where T : AstNode
        => root.DescendantsAndSelf().OfType<T>().FirstOrDefault(predicate);
}
```

### Применение в импорт-анализаторе

```csharp
// Вместо ручного рекурсивного обхода — Iterator:
public sealed class TypeScriptImportResolver
{
    public IReadOnlyList<ImportNode> ResolveImports(SourceFileNode file)
    {
        // Найти все ссылки на типы через Iterator
        var typeReferences = file
            .FindAll<SimpleTypeNode>()                    // все SimpleTypeNode в дереве
            .Where(t => !IsBuiltInType(t.Name))           // только не встроенные
            .Select(t => t.Name)
            .Distinct();

        return typeReferences
            .Select(name => new ImportNode(
                from:  $"../models/{ToKebabCase(name)}",
                names: [name]))
            .ToList();
    }
}
```

### IrDocument Iterator — обход всех типов в IR

```csharp
// IrDocument.AllTypes — Iterator по всем типам:
public sealed record IrDocument
{
    public IReadOnlyList<IrModel>  Models  { get; init; } = [];
    public IReadOnlyList<IrEnum>   Enums   { get; init; } = [];
    public IReadOnlyList<IrClient> Clients { get; init; } = [];

    // Iterator: все типы в документе (модели + inline типы из операций)
    public IEnumerable<string> AllTypeNames()
    {
        foreach (var m in Models) yield return m.Name;
        foreach (var e in Enums)  yield return e.Name;

        // Inline типы из клиентов:
        foreach (var c in Clients)
            foreach (var op in c.Operations)
            {
                if (op.Response?.BodyType is IrModelReference r)
                    yield return r.ModelName;
                foreach (var p in op.Parameters)
                    if (p.Type is IrModelReference pr)
                        yield return pr.ModelName;
            }
    }
}
```

---

## 10. Facade — GenerationOrchestrator

### Зачем Facade

CLI (`GenerateCommand`) не должен знать о деталях: IrBuilder, registry, pipeline, file writer. `GenerationOrchestrator` — Facade, скрывающий всю сложность.

```csharp
// ValIO/Services/GenerationOrchestrator.cs

/// <summary>
/// Facade: единая точка входа для запуска генерации.
/// Скрывает сложность взаимодействия IrBuilder, GeneratorRegistry, Pipeline, FileWriter.
/// </summary>
public sealed class GenerationOrchestrator
{
    private readonly IOpenApiDocumentParser _parser;
    private readonly IrBuilder             _irBuilder;
    private readonly IGeneratorRegistry    _registry;
    private readonly IConfigurationLoader  _configLoader;
    private readonly ILogger<GenerationOrchestrator> _logger;

    public GenerationOrchestrator(
        IOpenApiDocumentParser parser,
        IrBuilder irBuilder,
        IGeneratorRegistry registry,
        IConfigurationLoader configLoader,
        ILogger<GenerationOrchestrator> logger)
    {
        _parser = parser;
        _irBuilder = irBuilder;
        _registry = registry;
        _configLoader = configLoader;
        _logger = logger;
    }

    /// <summary>
    /// Полный цикл генерации для одного проекта.
    /// Caller не знает о внутренних шагах.
    /// </summary>
    public async Task<GenerationResult> GenerateAsync(
        string schemaPath,
        ProjectConfig projectConfig,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting generation for {Language}", projectConfig.Language);

        // Шаг 1: Загрузить OpenAPI документ
        var document = await _parser.ParseAsync(schemaPath, ct);

        // Шаг 2: Построить IR
        var irDocument = _irBuilder.BuildFromOpenApi(document);
        _logger.LogDebug("IR built: {Models} models, {Enums} enums, {Clients} clients",
            irDocument.Models.Count, irDocument.Enums.Count, irDocument.Clients.Count);

        // Шаг 3: Найти плагин
        var plugin = _registry.FindPlugin(projectConfig.Language)
            ?? throw new NotSupportedException($"No plugin for: {projectConfig.Language}");

        // Шаг 4: Загрузить конфиг
        var config = _configLoader.MapToGenerationConfiguration(projectConfig);

        // Шаг 5: Создать контекст
        var context = new GenerationContext
        {
            IrDocument    = irDocument,
            Configuration = config,
            PluginOptions = config.PluginOptions
        };

        // Шаг 6: Создать и запустить pipeline
        var pipeline = plugin.CreatePipeline(context, _serviceProvider);
        var result   = await pipeline.ExecuteAsync(context, ct);

        _logger.LogInformation("Generation complete: {Files} files generated",
            result.Files.Count);

        return result;
    }

    /// <summary>
    /// Генерация для нескольких проектов параллельно.
    /// </summary>
    public async Task<IReadOnlyList<GenerationResult>> GenerateAllAsync(
        ProjectsConfig projectsConfig,
        CancellationToken ct = default)
    {
        var tasks = projectsConfig.Projects
            .Select(p => GenerateAsync(p.Schema, p, ct))
            .ToList();

        return await Task.WhenAll(tasks);
    }
}

// CLI использует только Facade:
public class GenerateCommand : Command
{
    private readonly GenerationOrchestrator _orchestrator;  // Только Facade

    // Не знает об IrBuilder, IGeneratorRegistry, ICodeGenerationPipeline...
    private async Task Execute(string schema, ProjectConfig config, CancellationToken ct)
        => await _orchestrator.GenerateAsync(schema, config, ct);
}
```

---

## 11. State — GenerationContext и накопление состояния

### State для накопления результатов в Pipeline

`GenerationContext` ведёт себя как State — объект, чьё состояние изменяется по мере выполнения шагов.

```csharp
// ValIO.Core/Plugins/GenerationContext.cs

public sealed class GenerationContext
{
    // Иммутабельная часть (задаётся при создании):
    public required IrDocument IrDocument { get; init; }
    public required GenerationConfiguration Configuration { get; init; }
    public required object? PluginOptions { get; init; }
    public required IServiceProvider Services { get; init; }

    // Мутабельная часть — накапливается по мере выполнения шагов:
    private readonly Dictionary<string, string> _generatedFiles = new();
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors   = [];

    // Доступ только для чтения извне:
    public IReadOnlyDictionary<string, string> GeneratedFiles => _generatedFiles;
    public IReadOnlyList<string> Warnings => _warnings;
    public IReadOnlyList<string> Errors   => _errors;

    // Методы для изменения состояния (вызываются из Steps):
    internal void AddFile(string path, string content)
        => _generatedFiles[path] = content;

    internal void AddWarning(string message)
        => _warnings.Add(message);

    internal void AddError(string message)
        => _errors.Add(message);

    // State-специфичное состояние для шагов:
    // Например, Zod Step может записать сгенерированные имена схем
    // для использования в Client Step (импорты)
    private readonly Dictionary<string, object> _stepData = new();

    public void SetStepData<T>(string key, T value)
        => _stepData[key] = value!;

    public T? GetStepData<T>(string key)
        => _stepData.TryGetValue(key, out var v) ? (T)v : default;

    // Преобразование в результат:
    public GenerationResult ToResult() => new()
    {
        Success  = _errors.Count == 0,
        Files    = _generatedFiles,
        Warnings = _warnings,
        Errors   = _errors
    };
}
```

**Передача данных между шагами через StepData:**

```csharp
// ZodSchemasStep записывает информацию о схемах:
public sealed class Zod3SchemasStep : ZodSchemasStepBase
{
    public override async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
        GenerationContext ctx, CancellationToken ct)
    {
        var files = await base.ExecuteAsync(ctx, ct);

        // Записываем State для следующих шагов:
        var schemaNames = ctx.IrDocument.Models
            .ToDictionary(m => m.Name, m => $"{ToCamelCase(m.Name)}Schema");
        ctx.SetStepData("zod.schemaNames", schemaNames);  // ← State

        return files;
    }
}

// AxiosClientStep читает State от ZodSchemasStep:
public sealed class AxiosClientStep : ICodeGenerationStep
{
    public override async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
        GenerationContext ctx, CancellationToken ct)
    {
        // Читаем State от ZodSchemasStep (если был запущен):
        var zodSchemas = ctx.GetStepData<Dictionary<string, string>>("zod.schemaNames");

        // Если Zod схемы были сгенерированы — добавить импорты и вызовы parse()
        if (zodSchemas != null)
            return BuildWithZodValidation(ctx, zodSchemas);
        else
            return BuildWithoutValidation(ctx);
    }
}
```

---

## 12. Proxy — кэширующий ISchemaResolver

### Proxy для кэширования OpenAPI $ref разрешений

`ISchemaResolver` разрешает `$ref` в OpenAPI документе. При большом документе (500+ схем) разрешение одной схемы может происходить тысячи раз. Proxy кэширует результаты.

```csharp
// Реальный субъект (Subject):
public interface ISchemaResolver
{
    IOpenApiSchema ResolveSchema(IOpenApiSchema schema);
    string? GetSchemaName(IOpenApiSchema schema, string fallback);
}

// Реальная реализация:
public sealed class DefaultSchemaResolver : ISchemaResolver
{
    private readonly OpenApiDocument _document;

    public IOpenApiSchema ResolveSchema(IOpenApiSchema schema)
    {
        // Тяжёлая операция: пройти по $ref цепочке
        if (schema.Reference == null) return schema;
        return _document.Components.Schemas[schema.Reference.Id];
    }
}

// Proxy с кэшированием:
public sealed class CachingSchemaResolver : ISchemaResolver
{
    private readonly ISchemaResolver _inner;
    private readonly Dictionary<string, IOpenApiSchema> _cache = new();

    public CachingSchemaResolver(ISchemaResolver inner) => _inner = inner;

    public IOpenApiSchema ResolveSchema(IOpenApiSchema schema)
    {
        // Для $ref — кэшировать по Id
        if (schema.Reference?.Id is string refId)
        {
            if (_cache.TryGetValue(refId, out var cached))
                return cached;

            var resolved = _inner.ResolveSchema(schema);
            _cache[refId] = resolved;
            return resolved;
        }

        // Для inline схем — напрямую
        return _inner.ResolveSchema(schema);
    }

    public string? GetSchemaName(IOpenApiSchema schema, string fallback)
        => _inner.GetSchemaName(schema, fallback);
}
```

**Регистрация Proxy в DI:**

```csharp
// Прозрачно для потребителей:
services.AddSingleton<DefaultSchemaResolver>();
services.AddSingleton<ISchemaResolver>(sp =>
    new CachingSchemaResolver(sp.GetRequiredService<DefaultSchemaResolver>()));
```

### Proxy для логирования шагов генерации

```csharp
// Logging Proxy для ICodeGenerationStep:
public sealed class LoggingStepProxy : ICodeGenerationStep
{
    private readonly ICodeGenerationStep _inner;
    private readonly ILogger _logger;

    public LoggingStepProxy(ICodeGenerationStep inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public string Name => _inner.Name;
    public int Order  => _inner.Order;

    public bool ShouldExecute(GenerationContext ctx)
        => _inner.ShouldExecute(ctx);

    public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
        GenerationContext ctx,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("▶ Starting step: {StepName}", _inner.Name);

        try
        {
            var result = await _inner.ExecuteAsync(ctx, ct);
            sw.Stop();
            _logger.LogInformation("✓ Step {StepName} completed in {Elapsed}ms, {Files} files",
                _inner.Name, sw.ElapsedMilliseconds, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "✗ Step {StepName} failed after {Elapsed}ms",
                _inner.Name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
```

---

## 13. Сводная таблица паттернов

| Паттерн | Где применяется | Файлы / Компоненты | Ценность |
|---------|----------------|--------------------|----------|
| **Visitor** | Обход и рендеринг AST | `BaseAstVisitor`, `TypeScriptAstRenderer`, `ZodAstVisitorBase` | Устраняет switch(Kind), добавление нод без изменения рендереров |
| **Chain of Responsibility** | Расширяемый рендерер для стеков | `ITypeScriptNodeRenderer`, `ZodNodeRenderer`, `AxiosNodeRenderer` | Добавление нового стека = один новый класс, Core не меняется |
| **Strategy** | Маппинг типов, выбор генератора | `IIrTypeMapper`, `TypeScriptIrTypeMapper`, `CSharpIrTypeMapper`, `IGeneratorPlugin` | Каждый язык = своя стратегия маппинга |
| **Builder** | Построение IR и AST | `IrBuilder`, `FunctionDeclarationNode.Builder`, `SourceFileBuilder` | Пошаговое построение сложных объектов; Fluent API |
| **Template Method** | Шаги с общим алгоритмом | `ZodSchemasStepBase`, `BaseClientAstBuilder` | Вариации без дублирования кода алгоритма |
| **Command** | Шаги генерации | `ICodeGenerationStep`, `CodeGenerationPipeline` | Изоляция, отмена, логирование, переупорядочивание |
| **Composite** | AST дерево | `AstNode.Children`, `SourceFileNode`, `UnionTypeNode` | Унифицированный обход листьев и узлов |
| **Prototype** | Клонирование AST нод | `AstNode.Clone()`, `SimpleTypeNode.WithName()` | Immutable трансформации без side effects |
| **Iterator** | Обход AST, IR коллекций | `AstNodeExtensions.DescendantsAndSelf()`, `IrDocument.AllTypeNames()` | Ленивый обход без раскрытия структуры |
| **Facade** | CLI → Pipeline | `GenerationOrchestrator` | CLI не знает об IrBuilder, Registry, Pipeline |
| **State** | Накопление результатов | `GenerationContext` (GeneratedFiles, StepData) | Передача данных между шагами без глобальных переменных |
| **Proxy** | Кэш и логирование | `CachingSchemaResolver`, `LoggingStepProxy` | Прозрачное расширение без изменения субъекта |

### Карта взаимодействия паттернов

```
GenerateCommand
    → Facade (GenerationOrchestrator)
        → IrBuilder (Builder)
            → ISchemaResolver (Proxy: CachingSchemaResolver)
        → IGeneratorRegistry (Strategy: выбор плагина)
        → IGeneratorPlugin (Strategy: конкретный язык)
            → CodeGenerationPipeline
                → ICodeGenerationStep[] (Command) — порядок через Order
                    → TypeScriptModelsStep (Command)
                        → IrBuilder.Models (Iterator: OrderByDependencies)
                        → TypeScriptModelAstBuilder (Builder: строит AST)
                            → IIrTypeMapper (Strategy: маппинг типов)
                        → TypeScriptAstRenderer (Visitor: рендеринг AST)
                            → ITypeScriptNodeRenderer[] (Chain of Responsibility)
                    → ZodSchemasStep (Command, Template Method)
                        → GenerationContext.SetStepData (State)
                    → AxiosClientStep (Command)
                        → GenerationContext.GetStepData (State)
                        → BaseClientAstBuilder (Template Method)
                    → ...
                → GenerationContext (State: накапливает файлы)
            → GenerationResult
```

### Правила применения

**Visitor обязателен** для всех операций над AST деревом. Никогда не использовать `switch(nodeType)` или `is` проверки снаружи `Accept`.

**Chain of Responsibility** только для стек-специфичных нод в рамках одного языка. Не использовать для кросс-языковых операций.

**Strategy через DI** — каждый плагин регистрирует свою стратегию маппинга. Никогда не создавать стратегию через `new` в потребителе.

**Builder для нод с 4+ параметрами** — при более чем 4 конструкторных параметрах использовать Fluent Builder.

**Template Method только внутри одного плагина** — не создавать кросс-плагинные базовые классы. Иначе нарушается изоляция плагинов.

**Proxy прозрачно** — потребитель не знает о Proxy. Регистрация через DI как обёртка.

**Composite через Children** — все составные ноды обязаны корректно заполнять `Children`. Это инвариант дерева.
