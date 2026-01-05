---
name: ""
overview: ""
todos: []
---

# Максимально детальный план рефакторинга AST-архитектуры

## Часть 1: Глубокий анализ текущего состояния

### 1.1 Текущая реализация: что уже есть

#### 1.1.1 AST инфраструктура (частично реализована)

**Существующие базовые узлы:**

- [`ValIO.Core/Ast/Nodes/AstNode.cs`](ValIO.Core/Ast/Nodes/AstNode.cs) - абстрактный базовый класс, immutable
- [`ValIO.Core/Ast/Nodes/DeclarationNode.cs`](ValIO.Core/Ast/Nodes/DeclarationNode.cs) - базовый класс объявлений
- [`ValIO.Core/Ast/Nodes/TypeNode.cs`](ValIO.Core/Ast/Nodes/TypeNode.cs) - базовый класс типов
- [`ValIO.Core/Ast/Nodes/ExpressionNode.cs`](ValIO.Core/Ast/Nodes/ExpressionNode.cs) - базовый класс выражений
- [`ValIO.Core/Ast/Nodes/StatementNode.cs`](ValIO.Core/Ast/Nodes/StatementNode.cs) - базовый класс statements

**Существующие язык-специфичные узлы:**

- C#: CompilationUnitNode, NamespaceDeclarationNode, UsingDirectiveNode, ClassDeclarationNode, RecordDeclarationNode, StructDeclarationNode, InterfaceDeclarationNode, EnumDeclarationNode, FieldDeclarationNode, ConstructorDeclarationNode
- TypeScript: SourceFileNode, TypeAliasDeclarationNode, FunctionDeclarationNode, VariableDeclarationNode, ObjectLiteralExpressionNode, PropertyAssignmentNode, TemplateLiteralExpressionNode, GenericParameterNode, PropertySignatureNode

**Существующие стек-специфичные узлы:**

- HTTP: HttpMethodNode, RouteNode, PathParameterNode, QueryParameterNode, HeaderParameterNode, RequestBodyNode, ResponseTypeNode
- Axios: AxiosCallExpressionNode, AxiosConfigNode (в [`ValIO.Generators.TypeScript.Axios/Ast/Nodes/`](ValIO.Generators.TypeScript.Axios/Ast/Nodes/))
- Zod: ZodSchemaNode, ZodObjectNode, ZodArrayNode, ZodUnionNode, ZodIntersectionNode, ZodEnumNode, ZodEnumValueNode, ZodPropertyNode, ZodEffectsNode, ZodPrimitiveSchemaNode (в [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/))
- TanStack Query: QueryKeyNode, UseQueryNode, UseMutationNode, QueryOptionsNode, MutationOptionsNode (в [`ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/`](ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/))

**Существующие Builders:**

- [`ValIO.Generators.CSharp/Ast/Builders/CSharpAstBuilder.cs`](ValIO.Generators.CSharp/Ast/Builders/CSharpAstBuilder.cs)
- [`ValIO.Generators.TypeScript/Ast/Builders/TypeScriptAstBuilder.cs`](ValIO.Generators.TypeScript/Ast/Builders/TypeScriptAstBuilder.cs)
- [`ValIO.Generators.TypeScript.Axios/Ast/Builders/AxiosAstBuilder.cs`](ValIO.Generators.TypeScript.Axios/Ast/Builders/AxiosAstBuilder.cs)
- [`ValIO.Generators.TypeScript.Zod/Ast/Builders/ZodAstBuilder.cs`](ValIO.Generators.TypeScript.Zod/Ast/Builders/ZodAstBuilder.cs)
- [`ValIO.Generators.TypeScript.TanStackQuery/Ast/Builders/TanStackQueryAstBuilder.cs`](ValIO.Generators.TypeScript.TanStackQuery/Ast/Builders/TanStackQueryAstBuilder.cs)
- [`ValIO.Core/Ast/Builders/HttpAstBuilder.cs`](ValIO.Core/Ast/Builders/HttpAstBuilder.cs)

**Существующие Renderers (AST-based, новые):**

- [`ValIO.Generators.CSharp/Ast/Renderers/CSharpAstRenderer.cs`](ValIO.Generators.CSharp/Ast/Renderers/CSharpAstRenderer.cs)
- [`ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs`](ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs)
- [`ValIO.Generators.TypeScript.Axios/Ast/Renderers/AxiosAstRenderer.cs`](ValIO.Generators.TypeScript.Axios/Ast/Renderers/AxiosAstRenderer.cs)
- [`ValIO.Generators.TypeScript.Zod/Ast/Renderers/ZodAstRenderer.cs`](ValIO.Generators.TypeScript.Zod/Ast/Renderers/ZodAstRenderer.cs)
- [`ValIO.Generators.TypeScript.TanStackQuery/Ast/Renderers/TanStackQueryAstRenderer.cs`](ValIO.Generators.TypeScript.TanStackQuery/Ast/Renderers/TanStackQueryAstRenderer.cs)

**Существующие Renderers (StringBuilder-based, старые, подлежат удалению):**

- [`ValIO.Generators.CSharp/CSharpCodeRenderer.cs`](ValIO.Generators.CSharp/CSharpCodeRenderer.cs) (1161 строка)
- [`ValIO.Generators.TypeScript/TypeScriptCodeRenderer.cs`](ValIO.Generators.TypeScript/TypeScriptCodeRenderer.cs) (953 строки)
- [`ValIO.Generators.TypeScript.Zod/ZodSchemaRenderer.cs`](ValIO.Generators.TypeScript.Zod/ZodSchemaRenderer.cs) (1010 строк)
- [`ValIO.Generators.TypeScript.TanStackQuery/TanStackQueryRenderer.cs`](ValIO.Generators.TypeScript.TanStackQuery/TanStackQueryRenderer.cs) (1551 строка)

#### 1.1.2 Промежуточные представления (частично реализованы)

**TypeInfo** ([`ValIO.Core/Models/TypeInfo.cs`](ValIO.Core/Models/TypeInfo.cs)):

- Содержит мета-информацию о типе: `BaseType`, `IsCollection`, `IsDictionary`, `IsNullable`, `IsOptional`, `IsFile`, `IsPrimitive`, `GenericArguments`, `FullTypeName`, `Language`, `Dependencies`, `Format`
- Используется как промежуточное представление между OpenAPI и AST
- **Проблема**: Недостаточно структурирован, содержит смешение уровней (FullTypeName как строка)

**TemplateData классы:**

- `ModelTemplateData<TOptions, TPropertyOptions>` - данные для модели
- `PropertyTemplateData<TOptions>` - данные для свойства
- `OperationTemplateData<TOptions>` - данные для операции
- `EnumTemplateData<TOptions>` - данные для enum
- `ClientTemplateData<TOptions, TOperationOptions>` - данные для клиента
- **Проблема**: Это данные для шаблонов, а не полноценный IR. Не являются AST.

### 1.2 Критические проблемы и нарушения SOLID

#### 1.2.1 Нарушение Interface Segregation Principle (ISP)

**Проблема в [`ValIO.Core/Ast/Visitors/IAstVisitor.cs`](ValIO.Core/Ast/Visitors/IAstVisitor.cs):**

```csharp
public interface IAstVisitor<TResult>
{
    // Базовые узлы (20+ методов)
    TResult VisitIdentifier(IdentifierNode node);
    TResult VisitComment(CommentNode node);
    // ...
    
    // C# специфичные узлы (10+ методов)
    TResult VisitCompilationUnit(CompilationUnitNode node);
    TResult VisitNamespaceDeclaration(NamespaceDeclarationNode node);
    TResult VisitClassDeclaration(ClassDeclarationNode node);
    // ...
    
    // TypeScript специфичные узлы (9+ методов)
    TResult VisitSourceFile(SourceFileNode node);
    TResult VisitTypeAliasDeclaration(TypeAliasDeclarationNode node);
    TResult VisitFunctionDeclaration(FunctionDeclarationNode node);
    // ...
    
    // HTTP специфичные узлы (7+ методов)
    TResult VisitHttpMethod(HttpMethodNode node);
    TResult VisitRoute(RouteNode node);
    // ...
}
```

**Последствия:**

1. [`CSharpAstRenderer`](ValIO.Generators.CSharp/Ast/Renderers/CSharpAstRenderer.cs) должен реализовывать методы для TypeScript узлов
2. [`TypeScriptAstRenderer`](ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs) должен реализовывать методы для C# узлов
3. Оба должны реализовывать методы для стек-специфичных узлов (Axios, Zod, TanStack Query)
4. Приходится писать пустые реализации или выбрасывать NotSupportedException
5. Нарушается принцип "клиенты не должны зависеть от интерфейсов, которые они не используют"

**Пример из [`AstVisitorBase.cs`](ValIO.Core/Ast/Visitors/AstVisitorBase.cs):**

```csharp
public abstract class AstVisitorBase<TResult> : IAstVisitor<TResult>
{
    // 40+ абстрактных методов, которые ДОЛЖНЫ быть реализованы
    public abstract TResult VisitCompilationUnit(CompilationUnitNode node); // C# only
    public abstract TResult VisitSourceFile(SourceFileNode node); // TypeScript only
    public abstract TResult VisitHttpMethod(HttpMethodNode node); // HTTP only
    // ...
}
```

#### 1.2.2 Отсутствие полноценного Language-agnostic IR

**Проблема:**

- `TypeInfo` содержит `FullTypeName` как строку (`"List<User>"`, `"Dictionary<string, int>"`)
- Нет единого промежуточного представления между OpenAPI и AST
- `TemplateData` классы не являются IR, они содержат данные для рендеринга

**Что нужно:**

- Полноценный Language-agnostic IR, независимый от синтаксиса языка
- IR должен быть сериализуемым (JSON/YAML)
- IR должен содержать только структуру, без синтаксиса и форматирования

#### 1.2.3 Дублирование кода в рендерерах

**Проблема в старых рендерерах:**

- [`TypeScriptCodeRenderer`](ValIO.Generators.TypeScript/TypeScriptCodeRenderer.cs) содержит `RenderFetchOperation()` и `RenderAxiosOperation()` с похожей логикой
- [`ZodSchemaRenderer`](ValIO.Generators.TypeScript.Zod/ZodSchemaRenderer.cs) и [`TanStackQueryRenderer`](ValIO.Generators.TypeScript.TanStackQuery/TanStackQueryRenderer.cs) дублируют логику обработки параметров

#### 1.2.4 Смешение ответственности

**Проблема в старых рендерерах:**

- Методы знают и о структуре данных (`PropertyTemplateData`), и о синтаксисе (`StringBuilder.AppendLine("public ...")`)
- Невозможно протестировать структуру кода без парсинга строк
- Невозможно модифицировать структуру кода до рендеринга

---

## Часть 2: Решение проблемы SOLID через композицию визиторов

### 2.1 Разделение IAstVisitor на композицию интерфейсов

**Принцип:** Каждый язык/стек имеет свой интерфейс визитора, который знает только о своих узлах.

#### 2.1.1 Базовый интерфейс: IBaseAstVisitor

**Файл:** `ValIO.Core/Ast/Visitors/IBaseAstVisitor.cs`

```csharp
namespace ValIO.Core.Ast.Visitors;

/// <summary>
/// Базовый интерфейс посетителя для language-agnostic AST узлов
/// </summary>
public interface IBaseAstVisitor<TResult>
{
    /// <summary>
    /// Посещает узел (диспетчеризация по типу)
    /// </summary>
    TResult Visit(AstNode node);
    
    // Базовые узлы (общие для всех языков)
    TResult VisitIdentifier(IdentifierNode node);
    TResult VisitComment(CommentNode node);
    TResult VisitAttribute(AttributeNode node);
    TResult VisitBlock(BlockNode node);
    TResult VisitImport(ImportNode node);
    TResult VisitExport(ExportNode node);
    
    // Типы (общие)
    TResult VisitSimpleType(SimpleTypeNode node);
    TResult VisitGenericType(GenericTypeNode node);
    TResult VisitNullableType(NullableTypeNode node);
    TResult VisitOptionalType(OptionalTypeNode node);
    TResult VisitArrayType(ArrayTypeNode node);
    TResult VisitUnionType(UnionTypeNode node);
    TResult VisitIntersectionType(IntersectionTypeNode node);
    TResult VisitDictionaryType(DictionaryTypeNode node);
    
    // Выражения (общие)
    TResult VisitLiteralExpression(LiteralExpressionNode node);
    TResult VisitIdentifierExpression(IdentifierExpressionNode node);
    TResult VisitCallExpression(CallExpressionNode node);
    TResult VisitMemberAccessExpression(MemberAccessExpressionNode node);
    TResult VisitBinaryExpression(BinaryExpressionNode node);
    TResult VisitConditionalExpression(ConditionalExpressionNode node);
    
    // Statements (общие)
    TResult VisitReturnStatement(ReturnStatementNode node);
    TResult VisitIfStatement(IfStatementNode node);
    TResult VisitForEachStatement(ForEachStatementNode node);
    TResult VisitExpressionStatement(ExpressionStatementNode node);
    
    // Объявления (общие)
    TResult VisitPropertyDeclaration(PropertyDeclarationNode node);
    TResult VisitMethodDeclaration(MethodDeclarationNode node);
    TResult VisitParameterDeclaration(ParameterDeclarationNode node);
    TResult VisitEnumMemberDeclaration(EnumMemberDeclarationNode node);
}
```

#### 2.1.2 C# визитор: ICSharpAstVisitor

**Файл:** `ValIO.Generators.CSharp/Ast/Visitors/ICSharpAstVisitor.cs`

```csharp
namespace ValIO.Generators.CSharp.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для C# специфичных AST узлов
/// </summary>
public interface ICSharpAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    // C# специфичные узлы
    TResult VisitCompilationUnit(CompilationUnitNode node);
    TResult VisitNamespaceDeclaration(NamespaceDeclarationNode node);
    TResult VisitUsingDirective(UsingDirectiveNode node);
    TResult VisitClassDeclaration(ClassDeclarationNode node);
    TResult VisitRecordDeclaration(RecordDeclarationNode node);
    TResult VisitStructDeclaration(StructDeclarationNode node);
    TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);
    TResult VisitEnumDeclaration(EnumDeclarationNode node);
    TResult VisitFieldDeclaration(FieldDeclarationNode node);
    TResult VisitConstructorDeclaration(ConstructorDeclarationNode node);
}
```

#### 2.1.3 TypeScript визитор: ITypeScriptAstVisitor

**Файл:** `ValIO.Generators.TypeScript/Ast/Visitors/ITypeScriptAstVisitor.cs`

```csharp
namespace ValIO.Generators.TypeScript.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для TypeScript специфичных AST узлов
/// </summary>
public interface ITypeScriptAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    // TypeScript специфичные узлы
    TResult VisitSourceFile(SourceFileNode node);
    TResult VisitTypeAliasDeclaration(TypeAliasDeclarationNode node);
    TResult VisitFunctionDeclaration(FunctionDeclarationNode node);
    TResult VisitVariableDeclaration(VariableDeclarationNode node);
    TResult VisitObjectLiteralExpression(ObjectLiteralExpressionNode node);
    TResult VisitPropertyAssignment(PropertyAssignmentNode node);
    TResult VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node);
    TResult VisitGenericParameter(GenericParameterNode node);
    TResult VisitPropertySignature(PropertySignatureNode node);
}
```

#### 2.1.4 HTTP визитор: IHttpAstVisitor

**Файл:** `ValIO.Core/Ast/Visitors/IHttpAstVisitor.cs`

```csharp
namespace ValIO.Core.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для HTTP специфичных AST узлов
/// </summary>
public interface IHttpAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    // HTTP специфичные узлы
    TResult VisitHttpMethod(HttpMethodNode node);
    TResult VisitRoute(RouteNode node);
    TResult VisitPathParameter(PathParameterNode node);
    TResult VisitQueryParameter(QueryParameterNode node);
    TResult VisitHeaderParameter(HeaderParameterNode node);
    TResult VisitRequestBody(RequestBodyNode node);
    TResult VisitResponseType(ResponseTypeNode node);
}
```

#### 2.1.5 Стек-специфичные визиторы (уже существуют)

- [`IAxiosAstVisitor<TResult>`](ValIO.Generators.TypeScript.Axios/Ast/Visitors/IAxiosAstVisitor.cs) - уже реализован
- [`IZodAstVisitor<TResult>`](ValIO.Generators.TypeScript.Zod/Ast/Visitors/IZodAstVisitor.cs) - уже реализован
- [`ITanStackQueryAstVisitor<TResult>`](ValIO.Generators.TypeScript.TanStackQuery/Ast/Visitors/ITanStackQueryAstVisitor.cs) - уже реализован

### 2.2 Базовые классы с default implementations

#### 2.2.1 BaseAstVisitor

**Файл:** `ValIO.Core/Ast/Visitors/BaseAstVisitor.cs`

```csharp
namespace ValIO.Core.Ast.Visitors;

/// <summary>
/// Базовый абстрактный класс для посетителя AST с default implementations
/// </summary>
public abstract class BaseAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    public virtual TResult Visit(AstNode node)
    {
        return node.Accept(this);
    }
    
    // Default implementations с диспетчеризацией
    public virtual TResult VisitDeclaration(DeclarationNode node)
    {
        return node.Kind switch
        {
            AstNodeKind.PropertyDeclaration => VisitPropertyDeclaration((PropertyDeclarationNode)node),
            AstNodeKind.MethodDeclaration => VisitMethodDeclaration((MethodDeclarationNode)node),
            AstNodeKind.ParameterDeclaration => VisitParameterDeclaration((ParameterDeclarationNode)node),
            AstNodeKind.EnumMemberDeclaration => VisitEnumMemberDeclaration((EnumMemberDeclarationNode)node),
            _ => VisitDefault(node)
        };
    }
    
    // Абстрактные методы для базовых узлов
    public abstract TResult VisitIdentifier(IdentifierNode node);
    public abstract TResult VisitComment(CommentNode node);
    // ... остальные базовые методы
    
    /// <summary>
    /// Метод по умолчанию для неизвестных узлов
    /// </summary>
    protected virtual TResult VisitDefault(AstNode node)
    {
        throw new NotSupportedException($"Visit for node kind {node.Kind} is not supported");
    }
}
```

#### 2.2.2 CSharpAstVisitorBase

**Файл:** `ValIO.Generators.CSharp/Ast/Visitors/CSharpAstVisitorBase.cs`

```csharp
namespace ValIO.Generators.CSharp.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя C# AST узлов
/// </summary>
public abstract class CSharpAstVisitorBase<TResult> : BaseAstVisitor<TResult>, ICSharpAstVisitor<TResult>
{
    // Абстрактные методы только для C# узлов
    public abstract TResult VisitCompilationUnit(CompilationUnitNode node);
    public abstract TResult VisitNamespaceDeclaration(NamespaceDeclarationNode node);
    public abstract TResult VisitUsingDirective(UsingDirectiveNode node);
    public abstract TResult VisitClassDeclaration(ClassDeclarationNode node);
    public abstract TResult VisitRecordDeclaration(RecordDeclarationNode node);
    public abstract TResult VisitStructDeclaration(StructDeclarationNode node);
    public abstract TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);
    public abstract TResult VisitEnumDeclaration(EnumDeclarationNode node);
    public abstract TResult VisitFieldDeclaration(FieldDeclarationNode node);
    public abstract TResult VisitConstructorDeclaration(ConstructorDeclarationNode node);
}
```

#### 2.2.3 TypeScriptAstVisitorBase

**Файл:** `ValIO.Generators.TypeScript/Ast/Visitors/TypeScriptAstVisitorBase.cs`

```csharp
namespace ValIO.Generators.TypeScript.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя TypeScript AST узлов
/// </summary>
public abstract class TypeScriptAstVisitorBase<TResult> : BaseAstVisitor<TResult>, ITypeScriptAstVisitor<TResult>
{
    // Абстрактные методы только для TypeScript узлов
    public abstract TResult VisitSourceFile(SourceFileNode node);
    public abstract TResult VisitTypeAliasDeclaration(TypeAliasDeclarationNode node);
    public abstract TResult VisitFunctionDeclaration(FunctionDeclarationNode node);
    public abstract TResult VisitVariableDeclaration(VariableDeclarationNode node);
    public abstract TResult VisitObjectLiteralExpression(ObjectLiteralExpressionNode node);
    public abstract TResult VisitPropertyAssignment(PropertyAssignmentNode node);
    public abstract TResult VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node);
    public abstract TResult VisitGenericParameter(GenericParameterNode node);
    public abstract TResult VisitPropertySignature(PropertySignatureNode node);
}
```

### 2.3 Обновление существующих рендереров

#### 2.3.1 CSharpAstRenderer

**Изменения в [`ValIO.Generators.CSharp/Ast/Renderers/CSharpAstRenderer.cs`](ValIO.Generators.CSharp/Ast/Renderers/CSharpAstRenderer.cs):**

```csharp
// Было:
public class CSharpAstRenderer : AstVisitorBase<string>, ICSharpAstRenderer

// Станет:
public class CSharpAstRenderer : CSharpAstVisitorBase<string>, ICSharpAstRenderer
```

**Результат:**

- Теперь реализует только C# специфичные методы
- Не нужно реализовывать методы для TypeScript узлов
- Базовые узлы реализуются через `BaseAstVisitor<TResult>`

#### 2.3.2 TypeScriptAstRenderer

**Изменения в [`ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs`](ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs):**

```csharp
// Было:
public class TypeScriptAstRenderer : AstVisitorBase<string>, ITypeScriptAstRenderer

// Станет:
public class TypeScriptAstRenderer : TypeScriptAstVisitorBase<string>, ITypeScriptAstRenderer
```

**Результат:**

- Теперь реализует только TypeScript специфичные методы
- Не нужно реализовывать методы для C# узлов

---

## Часть 3: Language-agnostic IR (Intermediate Representation)

### 3.1 Принципы IR

**IR должен быть:**

1. **Immutable** - неизменяемый после создания
2. **Serializable** - сериализуемый в JSON/YAML
3. **Syntax-free** - не содержит синтаксиса языка
4. **Formatting-free** - не содержит форматирования
5. **Framework-free** - не содержит framework-специфичной логики

### 3.2 Структура IR

#### 3.2.1 IrDocument (корневой узел)

**Файл:** `ValIO.Core/IR/IrDocument.cs`

```csharp
namespace ValIO.Core.IR;

/// <summary>
/// Корневой узел IR представления OpenAPI схемы
/// </summary>
public sealed record IrDocument
{
    /// <summary>
    /// Метаданные документа
    /// </summary>
    public required IrDocumentMetadata Metadata { get; init; }
    
    /// <summary>
    /// Модели (схемы)
    /// </summary>
    public required IReadOnlyList<IrModel> Models { get; init; }
    
    /// <summary>
    /// Enum'ы
    /// </summary>
    public required IReadOnlyList<IrEnum> Enums { get; init; }
    
    /// <summary>
    /// HTTP клиенты
    /// </summary>
    public required IReadOnlyList<IrClient> Clients { get; init; }
    
    /// <summary>
    /// Граф зависимостей типов
    /// </summary>
    public required IrTypeDependencyGraph TypeDependencies { get; init; }
}
```

#### 3.2.2 IrModel (модель)

**Файл:** `ValIO.Core/IR/IrModel.cs`

```csharp
namespace ValIO.Core.IR;

/// <summary>
/// IR представление модели (schema)
/// </summary>
public sealed record IrModel
{
    /// <summary>
    /// Имя модели (sanitized)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Свойства модели
    /// </summary>
    public required IReadOnlyList<IrProperty> Properties { get; init; }
    
    /// <summary>
    /// Композиция (allOf, oneOf, anyOf)
    /// </summary>
    public IrComposition? Composition { get; init; }
    
    /// <summary>
    /// Оригинальная OpenAPI схема (для отладки)
    /// </summary>
    public IOpenApiSchema? OriginalSchema { get; init; }
}
```

#### 3.2.3 IrType (тип)

**Файл:** `ValIO.Core/IR/IrType.cs`

```csharp
namespace ValIO.Core.IR;

/// <summary>
/// IR представление типа (language-agnostic)
/// </summary>
public abstract record IrType
{
    /// <summary>
    /// Тип узла
    /// </summary>
    public abstract IrTypeKind Kind { get; }
}

/// <summary>
/// Примитивный тип
/// </summary>
public sealed record IrPrimitiveType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Primitive;
    
    /// <summary>
    /// Примитивный тип (string, number, integer, boolean)
    /// </summary>
    public required IrPrimitiveKind PrimitiveKind { get; init; }
}

/// <summary>
/// Ссылка на модель
/// </summary>
public sealed record IrModelReference : IrType
{
    public override IrTypeKind Kind => IrTypeKind.ModelReference;
    
    /// <summary>
    /// Имя модели
    /// </summary>
    public required string ModelName { get; init; }
}

/// <summary>
/// Массив
/// </summary>
public sealed record IrArrayType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Array;
    
    /// <summary>
    /// Тип элементов
    /// </summary>
    public required IrType ElementType { get; init; }
}

/// <summary>
/// Словарь
/// </summary>
public sealed record IrDictionaryType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Dictionary;
    
    /// <summary>
    /// Тип ключа
    /// </summary>
    public required IrType KeyType { get; init; }
    
    /// <summary>
    /// Тип значения
    /// </summary>
    public required IrType ValueType { get; init; }
}

/// <summary>
/// Union (oneOf, anyOf)
/// </summary>
public sealed record IrUnionType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Union;
    
    /// <summary>
    /// Типы в union
    /// </summary>
    public required IReadOnlyList<IrType> Types { get; init; }
}

/// <summary>
/// Intersection (allOf)
/// </summary>
public sealed record IrIntersectionType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Intersection;
    
    /// <summary>
    /// Типы в intersection
    /// </summary>
    public required IReadOnlyList<IrType> Types { get; init; }
}

/// <summary>
/// Nullable обертка
/// </summary>
public sealed record IrNullableType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Nullable;
    
    /// <summary>
    /// Внутренний тип
    /// </summary>
    public required IrType InnerType { get; init; }
}

/// <summary>
/// Optional обертка (для TypeScript)
/// </summary>
public sealed record IrOptionalType : IrType
{
    public override IrTypeKind Kind => IrTypeKind.Optional;
    
    /// <summary>
    /// Внутренний тип
    /// </summary>
    public required IrType InnerType { get; init; }
}
```

### 3.3 IrBuilder - построитель IR

**Файл:** `ValIO.Core/IR/Builders/IrBuilder.cs`

```csharp
namespace ValIO.Core.IR.Builders;

/// <summary>
/// Построитель Language-agnostic IR из OpenAPI документа
/// </summary>
public class IrBuilder
{
    private readonly ISchemaResolver _schemaResolver;
    private readonly INamingConverter _namingConverter;
    
    public IrBuilder(ISchemaResolver schemaResolver, INamingConverter namingConverter)
    {
        _schemaResolver = schemaResolver;
        _namingConverter = namingConverter;
    }
    
    /// <summary>
    /// Строит IR из OpenAPI документа
    /// </summary>
    public IrDocument BuildFromOpenApi(OpenApiDocument document)
    {
        var models = ExtractModels(document);
        var enums = ExtractEnums(document);
        var clients = ExtractClients(document);
        var typeDependencies = BuildTypeDependencyGraph(models, enums, clients);
        
        return new IrDocument
        {
            Metadata = ExtractMetadata(document),
            Models = models,
            Enums = enums,
            Clients = clients,
            TypeDependencies = typeDependencies
        };
    }
    
    /// <summary>
    /// Извлекает модели из компонентов
    /// </summary>
    private IReadOnlyList<IrModel> ExtractModels(OpenApiDocument document)
    {
        var models = new List<IrModel>();
        
        if (document.Components?.Schemas == null)
            return models;
        
        foreach (var (schemaName, schema) in document.Components.Schemas)
        {
            var sanitizedName = _namingConverter.SanitizeIdentifier(schemaName);
            var model = BuildModel(sanitizedName, schema, document);
            models.Add(model);
        }
        
        return models;
    }
    
    /// <summary>
    /// Строит модель из схемы
    /// </summary>
    private IrModel BuildModel(string name, IOpenApiSchema schema, OpenApiDocument document)
    {
        var properties = ExtractProperties(schema, document);
        var composition = ExtractComposition(schema, document);
        
        return new IrModel
        {
            Name = name,
            Description = schema.Description,
            Properties = properties,
            Composition = composition,
            OriginalSchema = schema
        };
    }
    
    /// <summary>
    /// Извлекает свойства из схемы
    /// </summary>
    private IReadOnlyList<IrProperty> ExtractProperties(IOpenApiSchema schema, OpenApiDocument document)
    {
        var properties = new List<IrProperty>();
        
        if (schema.Properties == null)
            return properties;
        
        foreach (var (propertyName, propertySchema) in schema.Properties)
        {
            var sanitizedName = _namingConverter.SanitizeIdentifier(propertyName);
            var propertyType = BuildType(propertySchema, document);
            var isRequired = schema.Required?.Contains(propertyName) ?? false;
            
            properties.Add(new IrProperty
            {
                Name = sanitizedName,
                Type = propertyType,
                IsRequired = isRequired,
                Description = propertySchema.Description,
                Constraints = ExtractConstraints(propertySchema)
            });
        }
        
        return properties;
    }
    
    /// <summary>
    /// Строит тип из схемы (рекурсивно)
    /// </summary>
    private IrType BuildType(IOpenApiSchema schema, OpenApiDocument document)
    {
        // Разрешаем ссылки
        var resolvedSchema = _schemaResolver.ResolveSchema(schema, document);
        
        // Примитивные типы
        if (IsPrimitive(resolvedSchema))
        {
            return new IrPrimitiveType
            {
                PrimitiveKind = MapPrimitive(resolvedSchema.Type, resolvedSchema.Format)
            };
        }
        
        // Массив
        if (resolvedSchema.Type == "array" && resolvedSchema.Items != null)
        {
            var elementType = BuildType(resolvedSchema.Items, document);
            return new IrArrayType { ElementType = elementType };
        }
        
        // Словарь
        if (resolvedSchema.Type == "object" && resolvedSchema.AdditionalProperties != null)
        {
            var keyType = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String };
            var valueType = BuildType(resolvedSchema.AdditionalProperties, document);
            return new IrDictionaryType { KeyType = keyType, ValueType = valueType };
        }
        
        // OneOf, AnyOf -> Union
        if (resolvedSchema.OneOf?.Count > 0 || resolvedSchema.AnyOf?.Count > 0)
        {
            var variants = (resolvedSchema.OneOf ?? resolvedSchema.AnyOf)!;
            var types = variants.Select(v => BuildType(v, document)).ToList();
            return new IrUnionType { Types = types };
        }
        
        // AllOf -> Intersection
        if (resolvedSchema.AllOf?.Count > 0)
        {
            var types = resolvedSchema.AllOf.Select(s => BuildType(s, document)).ToList();
            return new IrIntersectionType { Types = types };
        }
        
        // Ссылка на модель
        if (resolvedSchema.Reference != null)
        {
            var modelName = _namingConverter.SanitizeIdentifier(resolvedSchema.Reference.Id);
            return new IrModelReference { ModelName = modelName };
        }
        
        // Object (inline)
        if (resolvedSchema.Type == "object")
        {
            // Создаем анонимную модель или возвращаем reference
            throw new NotImplementedException("Inline objects not yet supported");
        }
        
        throw new InvalidOperationException($"Unknown schema type: {resolvedSchema.Type}");
    }
}
```

### 3.4 Преобразование IR → AST

**Принцип:** IR → AST Builder → AST

**Пример для C#:**

```csharp
// 1. OpenAPI → IR
var irBuilder = new IrBuilder(schemaResolver, namingConverter);
var irDocument = irBuilder.BuildFromOpenApi(openApiDocument);

// 2. IR → AST
var csharpAstBuilder = new CSharpAstBuilder(namingConverter);
var compilationUnit = csharpAstBuilder.BuildFromIr(irDocument.Models[0]);

// 3. AST → Code
var csharpRenderer = new CSharpAstRenderer();
var code = csharpRenderer.Render(compilationUnit);
```

---

## Часть 4: Полный список AST узлов

### 4.1 Общие (Language-agnostic) узлы

#### 4.1.1 Базовые узлы

| Узел | Файл | Описание | Статус |

|------|------|----------|--------|

| `AstNode` | [`ValIO.Core/Ast/Nodes/AstNode.cs`](ValIO.Core/Ast/Nodes/AstNode.cs) | Абстрактный базовый класс | ✅ Реализован |

| `DeclarationNode` | [`ValIO.Core/Ast/Nodes/DeclarationNode.cs`](ValIO.Core/Ast/Nodes/DeclarationNode.cs) | Базовый класс объявлений | ✅ Реализован |

| `TypeNode` | [`ValIO.Core/Ast/Nodes/TypeNode.cs`](ValIO.Core/Ast/Nodes/TypeNode.cs) | Базовый класс типов | ✅ Реализован |

| `ExpressionNode` | [`ValIO.Core/Ast/Nodes/ExpressionNode.cs`](ValIO.Core/Ast/Nodes/ExpressionNode.cs) | Базовый класс выражений | ✅ Реализован |

| `StatementNode` | [`ValIO.Core/Ast/Nodes/StatementNode.cs`](ValIO.Core/Ast/Nodes/StatementNode.cs) | Базовый класс statements | ✅ Реализован |

| `IdentifierNode` | [`ValIO.Core/Ast/Nodes/IdentifierNode.cs`](ValIO.Core/Ast/Nodes/IdentifierNode.cs) | Идентификатор | ✅ Реализован |

| `CommentNode` | [`ValIO.Core/Ast/Nodes/CommentNode.cs`](ValIO.Core/Ast/Nodes/CommentNode.cs) | Комментарий | ✅ Реализован |

| `AttributeNode` | [`ValIO.Core/Ast/Nodes/AttributeNode.cs`](ValIO.Core/Ast/Nodes/AttributeNode.cs) | Атрибут/декоратор | ✅ Реализован |

| `BlockNode` | [`ValIO.Core/Ast/Nodes/BlockNode.cs`](ValIO.Core/Ast/Nodes/BlockNode.cs) | Блок кода | ✅ Реализован |

| `ImportNode` | [`ValIO.Core/Ast/Nodes/ImportNode.cs`](ValIO.Core/Ast/Nodes/ImportNode.cs) | Import директива | ✅ Реализован |

| `ExportNode` | [`ValIO.Core/Ast/Nodes/ExportNode.cs`](ValIO.Core/Ast/Nodes/ExportNode.cs) | Export директива | ✅ Реализован |

#### 4.1.2 Узлы типов

| Узел | Файл | Описание | Статус |

|------|------|----------|--------|

| `SimpleTypeNode` | `ValIO.Core/Ast/Nodes/Types/SimpleTypeNode.cs` | Простой тип | ✅ Реализован |

| `GenericTypeNode` | `ValIO.Core/Ast/Nodes/Types/GenericTypeNode.cs` | Generic тип | ✅ Реализован |

| `NullableTypeNode` | `ValIO.Core/Ast/Nodes/Types/NullableTypeNode.cs` | Nullable тип | ✅ Реализован |

| `OptionalTypeNode` | `ValIO.Core/Ast/Nodes/Types/OptionalTypeNode.cs` | Optional тип | ✅ Реализован |

| `ArrayTypeNode` | `ValIO.Core/Ast/Nodes/Types/ArrayTypeNode.cs` | Массив | ✅ Реализован |

| `UnionTypeNode` | `ValIO.Core/Ast/Nodes/Types/UnionTypeNode.cs` | Union тип | ✅ Реализован |

| `IntersectionTypeNode` | `ValIO.Core/Ast/Nodes/Types/IntersectionTypeNode.cs` | Intersection тип | ✅ Реализован |

| `DictionaryTypeNode` | `ValIO.Core/Ast/Nodes/Types/DictionaryTypeNode.cs` | Словарь | ✅ Реализован |

#### 4.1.3 Узлы выражений

| Узел | Файл | Описание | Статус |

|------|------|----------|--------|

| `LiteralExpressionNode` | `ValIO.Core/Ast/Nodes/Expressions/LiteralExpressionNode.cs` | Литерал | ✅ Реализован |

| `IdentifierExpressionNode` | `ValIO.Core/Ast/Nodes/Expressions/IdentifierExpressionNode.cs` | Идентификатор | ✅ Реализован |

| `CallExpressionNode` | `ValIO.Core/Ast/Nodes/Expressions/CallExpressionNode.cs` | Вызов функции | ✅ Реализован |

| `MemberAccessExpressionNode` | `ValIO.Core/Ast/Nodes/Expressions/MemberAccessExpressionNode.cs` | Доступ к члену | ✅ Реализован |

| `BinaryExpressionNode` | `ValIO.Core/Ast/Nodes/Expressions/BinaryExpressionNode.cs` | Бинарная операция | ✅ Реализован |

| `ConditionalExpressionNode` | `ValIO.Core/Ast/Nodes/Expressions/ConditionalExpressionNode.cs` | Тернарный оператор | ✅ Реализован |

#### 4.1.4 Узлы statements

| Узел | Файл | Описание | Статус |

|------|------|----------|--------|

| `ReturnStatementNode` | `ValIO.Core/Ast/Nodes/Statements/ReturnStatementNode.cs` | Return | ✅ Реализован |

| `IfStatementNode` | `ValIO.Core/Ast/Nodes/Statements/IfStatementNode.cs` | If/Else | ✅ Реализован |

| `ForEachStatementNode` | `ValIO.Core/Ast/Nodes/Statements/ForEachStatementNode.cs` | ForEach | ✅ Реализован |

| `ExpressionStatementNode` | `ValIO.Core/Ast/Nodes/Statements/ExpressionStatementNode.cs` | Expression statement | ✅ Реализован |

### 4.2 C# специфичные узлы

| Узел | Файл | Описание | Статус |

|------|------|----------|--------|

| `CompilationUnitNode` | [`ValIO.Core/Ast/Nodes/CompilationUnitNode.cs`](ValIO.Core/Ast/Nodes/CompilationUnitNode.cs) | Compilation unit | ✅ Реализован |

| `NamespaceDeclarationNode` | [`ValIO.Core/Ast/Nodes/NamespaceDeclarationNode.cs`](ValIO.Core/Ast/Nodes/NamespaceDeclarationNode.cs) | Namespace | ✅ Реализован |

| `UsingDirectiveNode` | [`ValIO.Core/Ast/Nodes/UsingDirectiveNode.cs`](ValIO.Core/Ast/Nodes/UsingDirectiveNode.cs) | Using директива | ✅ Реализован |

| `ClassDeclarationNode` | [`ValIO.Core/Ast/Nodes/ClassDeclarationNode.cs`](ValIO.Core/Ast/Nodes/ClassDeclarationNode.cs) | Class | ✅ Реализован |

| `RecordDeclarationNode` | [`ValIO.Core/Ast/Nodes/RecordDeclarationNode.cs`](ValIO.Core/Ast/Nodes/RecordDeclarationNode.cs) | Record | ✅ Реализован |

| `StructDeclarationNode` | [`ValIO.Core/Ast/Nodes/StructDeclarationNode.cs`](ValIO.Core/Ast/Nodes/StructDeclarationNode.cs) | Struct | ✅ Реализован |

| `InterfaceDeclarationNode` | [`ValIO.Core/Ast/Nodes/InterfaceDeclarationNode.cs`](ValIO.Core/Ast/Nodes/InterfaceDeclarationNode.cs) | Interface | ✅ Реализован |

| `EnumDeclarationNode` | [`ValIO.Core/Ast/Nodes/EnumDeclarationNode.cs`](ValIO.Core/Ast/Nodes/EnumDeclarationNode.cs) | Enum | ✅ Реализован |

| `FieldDeclarationNode` | [`ValIO.Core/Ast/Nodes/FieldDeclarationNode.cs`](ValIO.Core/Ast/Nodes/FieldDeclarationNode.cs) | Field | ✅ Реализован |

| `ConstructorDeclarationNode` | [`ValIO.Core/Ast/Nodes/ConstructorDeclarationNode.cs`](ValIO.Core/Ast/Nodes/ConstructorDeclarationNode.cs) | Constructor | ✅ Реализован |

| `PropertyDeclarationNode` | [`ValIO.Core/Ast/Nodes/PropertyDeclarationNode.cs`](ValIO.Core/Ast/Nodes/PropertyDeclarationNode.cs) | Property | ✅ Реализован |

| `MethodDeclarationNode` | [`ValIO.Core/Ast/Nodes/MethodDeclarationNode.cs`](ValIO.Core/Ast/Nodes/MethodDeclarationNode.cs) | Method | ✅ Реализован |

| `ParameterDeclarationNode` | [`ValIO.Core/Ast/Nodes/ParameterDeclarationNode.cs`](ValIO.Core/Ast/Nodes/ParameterDeclarationNode.cs) | Parameter | ✅ Реализован |

| `EnumMemberDeclarationNode` | [`ValIO.Core/Ast/Nodes/EnumMemberDeclarationNode.cs`](ValIO.Core/Ast/Nodes/EnumMemberDeclarationNode.cs) | Enum member | ✅ Реализован |

### 4.3 TypeScript специфичные узлы

| Узел | Файл | Описание | Статус |

|------|------|----------|--------|

| `SourceFileNode` | [`ValIO.Core/Ast/Nodes/SourceFileNode.cs`](ValIO.Core/Ast/Nodes/SourceFileNode.cs) | Source file | ✅ Реализован |

| `TypeAliasDeclarationNode` | [`ValIO.Core/Ast/Nodes/TypeAliasDeclarationNode.cs`](ValIO.Core/Ast/Nodes/TypeAliasDeclarationNode.cs) | Type alias | ✅ Реализован |

| `FunctionDeclarationNode` | [`ValIO.Core/Ast/Nodes/FunctionDeclarationNode.cs`](ValIO.Core/Ast/Nodes/FunctionDeclarationNode.cs) | Function | ✅ Реализован |

| `VariableDeclarationNode` | [`ValIO.Core/Ast/Nodes/VariableDeclarationNode.cs`](ValIO.Core/Ast/Nodes/VariableDeclarationNode.cs) | Variable | ✅ Реализован |

| `ObjectLiteralExpressionNode` | [`ValIO.Core/Ast/Nodes/Expressions/ObjectLiteralExpressionNode.cs`](ValIO.Core/Ast/Nodes/Expressions/ObjectLiteralExpressionNode.cs) | Object literal | ✅ Реализован |

| `PropertyAssignmentNode` | [`ValIO.Core/Ast/Nodes/PropertyAssignmentNode.cs`](ValIO.Core/Ast/Nodes/PropertyAssignmentNode.cs) | Property assignment | ✅ Реализован |

| `TemplateLiteralExpressionNode` | [`ValIO.Core/Ast/Nodes/Expressions/TemplateLiteralExpressionNode.cs`](ValIO.Core/Ast/Nodes/Expressions/TemplateLiteralExpressionNode.cs) | Template literal | ✅ Реализован |

| `GenericParameterNode` | [`ValIO.Core/Ast/Nodes/GenericParameterNode.cs`](ValIO.Core/Ast/Nodes/GenericParameterNode.cs) | Generic parameter | ✅ Реализован |

| `PropertySignatureNode` | [`ValIO.Core/Ast/Nodes/PropertySignatureNode.cs`](ValIO.Core/Ast/Nodes/PropertySignatureNode.cs) | Property signature | ✅ Реализован |

### 4.4 HTTP специфичные узлы

| Узел | Файл | Описание | Статус |

|------|------|----------|--------|

| `HttpMethodNode` | [`ValIO.Core/Ast/Nodes/Http/HttpMethodNode.cs`](ValIO.Core/Ast/Nodes/Http/HttpMethodNode.cs) | HTTP метод | ✅ Реализован |

| `RouteNode` | [`ValIO.Core/Ast/Nodes/Http/RouteNode.cs`](ValIO.Core/Ast/Nodes/Http/RouteNode.cs) | Route | ✅ Реализован |

| `PathParameterNode` | [`ValIO.Core/Ast/Nodes/Http/PathParameterNode.cs`](ValIO.Core/Ast/Nodes/Http/PathParameterNode.cs) | Path parameter | ✅ Реализован |

| `QueryParameterNode` | [`ValIO.Core/Ast/Nodes/Http/QueryParameterNode.cs`](ValIO.Core/Ast/Nodes/Http/QueryParameterNode.cs) | Query parameter | ✅ Реализован |

| `HeaderParameterNode` | [`ValIO.Core/Ast/Nodes/Http/HeaderParameterNode.cs`](ValIO.Core/Ast/Nodes/Http/HeaderParameterNode.cs) | Header parameter | ✅ Реализован |

| `RequestBodyNode` | [`ValIO.Core/Ast/Nodes/Http/RequestBodyNode.cs`](ValIO.Core/Ast/Nodes/Http/RequestBodyNode.cs) | Request body | ✅ Реализован |

| `ResponseTypeNode` | [`ValIO.Core/Ast/Nodes/Http/ResponseTypeNode.cs`](ValIO.Core/Ast/Nodes/Http/ResponseTypeNode.cs) | Response type | ✅ Реализован |

### 4.5 Axios специфичные узлы

| Узел | Файл | Описание | Статус |

|------|------|----------|--------|

| `AxiosCallExpressionNode` | [`ValIO.Generators.TypeScript.Axios/Ast/Nodes/AxiosCallExpressionNode.cs`](ValIO.Generators.TypeScript.Axios/Ast/Nodes/AxiosCallExpressionNode.cs) | Axios вызов | ✅ Реализован |

| `AxiosConfigNode` | [`ValIO.Generators.TypeScript.Axios/Ast/Nodes/AxiosConfigNode.cs`](ValIO.Generators.TypeScript.Axios/Ast/Nodes/AxiosConfigNode.cs) | Axios config | ✅ Реализован |

### 4.6 Zod специфичные узлы

| Узел | Файл | Описание | Статус |

|------|------|----------|--------|

| `ZodSchemaNode` | [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodSchemaNode.cs`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodSchemaNode.cs) | Zod schema (base) | ✅ Реализован |

| `ZodObjectNode` | [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodObjectNode.cs`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodObjectNode.cs) | Zod object | ✅ Реализован |

| `ZodArrayNode` | [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodArrayNode.cs`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodArrayNode.cs) | Zod array | ✅ Реализован |

| `ZodUnionNode` | [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodUnionNode.cs`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodUnionNode.cs) | Zod union | ✅ Реализован |

| `ZodIntersectionNode` | [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodIntersectionNode.cs`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodIntersectionNode.cs) | Zod intersection | ✅ Реализован |

| `ZodEnumNode` | [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEnumNode.cs`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEnumNode.cs) | Zod enum | ✅ Реализован |

| `ZodEnumValueNode` | [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEnumValueNode.cs`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEnumValueNode.cs) | Zod enum value | ✅ Реализован |

| `ZodPropertyNode` | [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodPropertyNode.cs`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodPropertyNode.cs) | Zod property | ✅ Реализован |

| `ZodEffectsNode` | [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEffectsNode.cs`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEffectsNode.cs) | Zod effects | ✅ Реализован |

| `ZodPrimitiveSchemaNode` | [`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodPrimitiveSchemaNode.cs`](ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodPrimitiveSchemaNode.cs) | Zod primitive | ✅ Реализован |

### 4.7 TanStack Query специфичные узлы

| Узел | Файл | Описание | Статус |

|------|------|----------|--------|

| `QueryKeyNode` | [`ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/QueryKeyNode.cs`](ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/QueryKeyNode.cs) | Query key | ✅ Реализован |

| `UseQueryNode` | [`ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/UseQueryNode.cs`](ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/UseQueryNode.cs) | useQuery хук | ✅ Реализован |

| `UseMutationNode` | [`ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/UseMutationNode.cs`](ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/UseMutationNode.cs) | useMutation хук | ✅ Реализован |

| `QueryOptionsNode` | [`ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/QueryOptionsNode.cs`](ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/QueryOptionsNode.cs) | Query options | ✅ Реализован |

| `MutationOptionsNode` | [`ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/MutationOptionsNode.cs`](ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/MutationOptionsNode.cs) | Mutation options | ✅ Реализован |

---

## Часть 5: Пошаговый план рефакторинга

### Этап 1: Рефакторинг Visitor интерфейсов (Week 1, Days 1-3)

#### Шаг 1.1: Создание базового IBaseAstVisitor

**Файлы для создания:**

1. `ValIO.Core/Ast/Visitors/IBaseAstVisitor.cs` - базовый интерфейс
2. `ValIO.Core/Ast/Visitors/BaseAstVisitor.cs` - базовый класс с диспетчеризацией

**Методы для IBaseAstVisitor:**

- `TResult Visit(AstNode node)`
- Все методы для базовых узлов (типы, выражения, statements, общие объявления)

**Тестирование:** Создать тесты для диспетчеризации

#### Шаг 1.2: Создание C# визитора

**Файлы для создания:**

1. `ValIO.Generators.CSharp/Ast/Visitors/ICSharpAstVisitor.cs` - интерфейс
2. `ValIO.Generators.CSharp/Ast/Visitors/CSharpAstVisitorBase.cs` - базовый класс

**Методы для ICSharpAstVisitor:**

- Все методы для C# специфичных узлов

#### Шаг 1.3: Создание TypeScript визитора

**Файлы для создания:**

1. `ValIO.Generators.TypeScript/Ast/Visitors/ITypeScriptAstVisitor.cs` - интерфейс
2. `ValIO.Generators.TypeScript/Ast/Visitors/TypeScriptAstVisitorBase.cs` - базовый класс

**Методы для ITypeScriptAstVisitor:**

- Все методы для TypeScript специфичных узлов

#### Шаг 1.4: Создание HTTP визитора

**Файлы для создания:**

1. `ValIO.Core/Ast/Visitors/IHttpAstVisitor.cs` - интерфейс
2. `ValIO.Core/Ast/Visitors/HttpAstVisitorBase.cs` - базовый класс (опционально)

**Методы для IHttpAstVisitor:**

- Все методы для HTTP специфичных узлов

#### Шаг 1.5: Обновление существующих рендереров

**Файлы для изменения:**

1. [`ValIO.Generators.CSharp/Ast/Renderers/CSharpAstRenderer.cs`](ValIO.Generators.CSharp/Ast/Renderers/CSharpAstRenderer.cs)

   - Изменить базовый класс: `AstVisitorBase<string>` → `CSharpAstVisitorBase<string>`
   - Удалить пустые реализации методов для TypeScript узлов

2. [`ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs`](ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs)(ValIO.Generators.TypeScript/Ast/Renderers/