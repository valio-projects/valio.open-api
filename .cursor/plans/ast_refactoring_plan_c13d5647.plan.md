---
name: AST Refactoring Plan
overview: ""
todos: []
---

# План рефакторинга кодогенерации на AST-ориентированную архитектуру

## 1. Анализ текущей реализации

### 1.1 Текущая архитектура

#### 1.1.1 C# генерация

**Файлы:**

- `ValIO.Generators.CSharp/CSharpCodeGenerator.cs` - основной генератор
- `ValIO.Generators.CSharp/CSharpCodeRenderer.cs` - рендерер через StringBuilder (1161 строка)
- `ValIO.Generators.CSharp/CSharpTypeMapper.cs` - маппинг OpenAPI → C# типы
- `ValIO.Generators.CSharp/Renderers/ClassModelRenderer.cs` - рендеринг классов
- `ValIO.Generators.CSharp/Renderers/RecordModelRenderer.cs` - рендеринг records
- `ValIO.Generators.CSharp/Renderers/CSharpEnumRenderer.cs` - рендеринг enums

**Проблемы:**

1. **StringBuilder-ориентированная генерация** - весь код генерируется через конкатенацию строк
2. **Смешение логики и форматирования** - в `CSharpCodeRenderer.cs` методы типа `GenerateProperty`, `GenerateValidationAttributes` содержат и бизнес-логику, и форматирование
3. **Дублирование кода** - похожая логика для классов, records, structs дублируется
4. **Слабая типизация** - работа с типами через строки (`FullTypeName`)
5. **Сложность тестирования** - невозможно протестировать структуру кода без парсинга строк
6. **Проблемы с отступами** - ручное управление через `CodeFormatter.AppendLine(sb, indentLevel, ...)`
7. **Нет возможности трансформаций** - нельзя легко модифицировать структуру кода до рендеринга

**Текущий процесс:**

```
OpenAPI Document → TypeMapper → TemplateData → CodeRenderer (StringBuilder) → String
```

#### 1.1.2 TypeScript генерация

**Файлы:**

- `ValIO.Generators.TypeScript/TypeScriptCodeGenerator.cs` - основной генератор
- `ValIO.Generators.TypeScript/TypeScriptCodeRenderer.cs` - рендерер через StringBuilder (953 строки)
- `ValIO.Generators.TypeScript/TypeScriptTypeMapper.cs` - маппинг OpenAPI → TypeScript типы
- `ValIO.Generators.TypeScript.TanStackQuery/TanStackQueryRenderer.cs` - генерация хуков (1551 строка)
- `ValIO.Generators.TypeScript.Zod/ZodSchemaRenderer.cs` - генерация Zod схем

**Проблемы:**

1. **Дублирование логики** - `RenderFetchOperation` и `RenderAxiosOperation` содержат похожую логику
2. **Сложность композиции** - TanStack Query хуки генерируются отдельно, но зависят от axios/fetch методов
3. **Нет единой модели** - интерфейсы, типы, enum'ы генерируются по-разному
4. **Проблемы с импортами** - ручное управление импортами через строки
5. **Слабая поддержка union/intersection** - все через строки

**Текущий процесс:**

```
OpenAPI Document → TypeMapper → TemplateData → CodeRenderer (StringBuilder) → String
                                                      ↓
                                        TanStackQueryRenderer (StringBuilder) → String
                                                      ↓
                                        ZodSchemaRenderer (StringBuilder) → String
```

### 1.2 Domain Models (TemplateData)

**Текущая структура:**

- `ModelTemplateData<TOptions, TPropertyOptions>` - данные для модели
- `PropertyTemplateData<TOptions>` - данные для свойства
- `OperationTemplateData<TOptions>` - данные для операции
- `EnumTemplateData<TOptions>` - данные для enum
- `ClientTemplateData<TOptions, TOperationOptions>` - данные для клиента
- `TypeInfo` - информация о типе (базовый тип, generic аргументы, nullable, etc.)

**Проблемы:**

1. **TemplateData не является AST** - это данные для шаблона, а не структура кода
2. **Смешение уровней абстракции** - TemplateData содержит и domain информацию, и информацию о форматировании
3. **Нет иерархии** - все TemplateData плоские, нет композиции

### 1.3 Рендереры

**Текущие рендереры:**

- `ICodeRenderer` - базовый интерфейс (RenderModel, RenderEnum, RenderClient, RenderOperation)
- `IModelFormatRenderer` - для разных форматов моделей (Class, Record, Interface, Zod Schema)
- `IQueryWrapperRenderer` - для TanStack Query
- `IEnumFormatRenderer` - для разных форматов enum'ов

**Проблемы:**

1. **Нет разделения ответственности** - рендереры знают и о структуре, и о синтаксисе
2. **Нет возможности трансформаций** - нельзя модифицировать AST перед рендерингом
3. **Сложность добавления новых языков** - нужно переписывать все рендереры

## 2. Целевая AST-ориентированная архитектура

### 2.1 Принципы

1. **AST First** - код представляется как дерево узлов, рендеринг - отдельный шаг
2. **Immutable AST** - все узлы неизменяемые
3. **Language-agnostic IR** - промежуточное представление не зависит от языка
4. **Separation of Concerns** - структура кода, синтаксис и форматирование разделены
5. **Extensibility** - новые языки и стеки добавляются через новые узлы/рендереры

### 2.2 Pipeline

```
OpenAPI Document 
  → TypeMapper (OpenAPI → TypeInfo)
  → TemplateData (Domain Model)
  → AST Builder (TemplateData → AST)
  → AST Transformer (optional, AST → AST)
  → AST Renderer (AST → String)
  → Generated Code
```

### 2.3 Иерархия AST

```
AstNode (abstract, immutable)
  ├── DeclarationNode (abstract)
  │   ├── TypeDeclarationNode (abstract)
  │   │   ├── ClassDeclarationNode
  │   │   ├── RecordDeclarationNode
  │   │   ├── InterfaceDeclarationNode
  │   │   ├── EnumDeclarationNode
  │   │   └── TypeAliasDeclarationNode
  │   ├── MethodDeclarationNode
  │   ├── PropertyDeclarationNode
  │   └── ParameterDeclarationNode
  ├── StatementNode (abstract)
  │   ├── ReturnStatementNode
  │   ├── IfStatementNode
  │   ├── ForEachStatementNode
  │   └── ExpressionStatementNode
  ├── ExpressionNode (abstract)
  │   ├── LiteralExpressionNode
  │   ├── IdentifierExpressionNode
  │   ├── CallExpressionNode
  │   ├── MemberAccessExpressionNode
  │   └── BinaryExpressionNode
  ├── TypeNode (abstract)
  │   ├── SimpleTypeNode
  │   ├── GenericTypeNode
  │   ├── NullableTypeNode
  │   ├── ArrayTypeNode
  │   └── UnionTypeNode
  └── ImportNode / ExportNode / CommentNode / AttributeNode
```

## 3. Список AST узлов

### 3.1 Общие (Language-agnostic) узлы

#### 3.1.1 Базовые узлы

**AstNode (abstract)**

- `IReadOnlyList<AstNode> Children` - дочерние узлы
- `IDictionary<string, object> Metadata` - метаданные
- `AstNodeKind Kind` - тип узла
- `Accept<TResult>(IAstVisitor<TResult> visitor)` - Visitor pattern

**DeclarationNode (abstract)**

- `IdentifierNode Name` - имя
- `CommentNode? Documentation` - документация
- `IReadOnlyList<AttributeNode> Attributes` - атрибуты/декораторы

**TypeDeclarationNode (abstract) : DeclarationNode**

- `AccessModifier AccessModifier` - модификатор доступа
- `IReadOnlyList<TypeNode> BaseTypes` - базовые типы/интерфейсы
- `IReadOnlyList<MemberDeclarationNode> Members` - члены

**StatementNode (abstract) : AstNode**

- `CommentNode? Documentation` - документация

**ExpressionNode (abstract) : AstNode**

- `TypeNode? Type` - тип выражения (опционально, для type inference)

**TypeNode (abstract) : AstNode**

- `bool IsNullable` - nullable
- `bool IsOptional` - optional (для TypeScript)

#### 3.1.2 Узлы типов

**SimpleTypeNode : TypeNode**

- `IdentifierNode Name` - имя типа
- `bool IsPrimitive` - примитивный тип

**GenericTypeNode : TypeNode**

- `IdentifierNode Name` - имя generic типа
- `IReadOnlyList<TypeNode> TypeArguments` - аргументы типа

**NullableTypeNode : TypeNode**

- `TypeNode InnerType` - внутренний тип

**OptionalTypeNode : TypeNode**

- `TypeNode InnerType` - внутренний тип

**ArrayTypeNode : TypeNode**

- `TypeNode ElementType` - тип элементов
- `bool IsReadOnly` - readonly (для TypeScript)

**UnionTypeNode : TypeNode**

- `IReadOnlyList<TypeNode> Types` - типы в union

**IntersectionTypeNode : TypeNode**

- `IReadOnlyList<TypeNode> Types` - типы в intersection

**DictionaryTypeNode : TypeNode**

- `TypeNode KeyType` - тип ключа
- `TypeNode ValueType` - тип значения

#### 3.1.3 Узлы выражений

**LiteralExpressionNode : ExpressionNode**

- `object Value` - значение
- `LiteralKind Kind` - тип литерала (String, Number, Boolean, Null)

**IdentifierExpressionNode : ExpressionNode**

- `IdentifierNode Identifier` - идентификатор

**CallExpressionNode : ExpressionNode**

- `ExpressionNode Target` - вызываемый объект/функция
- `IReadOnlyList<ExpressionNode> Arguments` - аргументы

**MemberAccessExpressionNode : ExpressionNode**

- `ExpressionNode Object` - объект
- `IdentifierNode Member` - член

**BinaryExpressionNode : ExpressionNode**

- `ExpressionNode Left` - левый операнд
- `BinaryOperator Operator` - оператор
- `ExpressionNode Right` - правый операнд

**ConditionalExpressionNode : ExpressionNode**

- `ExpressionNode Condition` - условие
- `ExpressionNode TrueExpression` - true ветка
- `ExpressionNode FalseExpression` - false ветка

#### 3.1.4 Узлы объявлений

**PropertyDeclarationNode : DeclarationNode**

- `TypeNode Type` - тип свойства
- `ExpressionNode? DefaultValue` - значение по умолчанию
- `bool IsRequired` - required
- `PropertyAccessorKind Accessor` - get/set/init (для C#)

**MethodDeclarationNode : DeclarationNode**

- `TypeNode? ReturnType` - возвращаемый тип
- `IReadOnlyList<ParameterDeclarationNode> Parameters` - параметры
- `BlockNode? Body` - тело метода
- `bool IsAsync` - async метод
- `AccessModifier AccessModifier` - модификатор доступа

**ParameterDeclarationNode : DeclarationNode**

- `TypeNode Type` - тип параметра
- `ExpressionNode? DefaultValue` - значение по умолчанию
- `bool IsRequired` - required
- `ParameterKind Kind` - обычный/ref/out/in (для C#)

**EnumMemberDeclarationNode : DeclarationNode**

- `ExpressionNode? Value` - значение enum
- `CommentNode? Documentation` - документация

#### 3.1.5 Узлы операторов

**ReturnStatementNode : StatementNode**

- `ExpressionNode? Value` - возвращаемое значение

**IfStatementNode : StatementNode**

- `ExpressionNode Condition` - условие
- `BlockNode ThenBlock` - then блок
- `BlockNode? ElseBlock` - else блок

**ForEachStatementNode : StatementNode**

- `IdentifierNode Variable` - переменная итератора
- `ExpressionNode Collection` - коллекция
- `BlockNode Body` - тело цикла

**ExpressionStatementNode : StatementNode**

- `ExpressionNode Expression` - выражение

#### 3.1.6 Вспомогательные узлы

**IdentifierNode : AstNode**

- `string Name` - имя

**CommentNode : AstNode**

- `string Text` - текст комментария
- `CommentKind Kind` - XML/JSDoc/SingleLine/MultiLine

**AttributeNode : AstNode**

- `IdentifierNode Name` - имя атрибута
- `IReadOnlyList<ExpressionNode> Arguments` - аргументы

**BlockNode : AstNode**

- `IReadOnlyList<StatementNode> Statements` - statements

**ImportNode : AstNode**

- `IReadOnlyList<IdentifierNode> Items` - импортируемые элементы
- `string? From` - путь/модуль
- `ImportKind Kind` - default/namespace/named

**ExportNode : AstNode**

- `DeclarationNode Declaration` - экспортируемое объявление
- `ExportKind Kind` - default/named

### 3.2 C# специфичные узлы

**NamespaceDeclarationNode : DeclarationNode**

- `IdentifierNode Name` - имя namespace
- `IReadOnlyList<DeclarationNode> Declarations` - объявления в namespace

**UsingDirectiveNode : AstNode**

- `IdentifierNode Namespace` - namespace
- `IdentifierNode? Alias` - алиас

**ClassDeclarationNode : TypeDeclarationNode**

- `bool IsSealed` - sealed
- `bool IsStatic` - static
- `bool IsPartial` - partial

**RecordDeclarationNode : TypeDeclarationNode**

- `bool IsRecordStruct` - record struct
- `IReadOnlyList<ParameterDeclarationNode> PrimaryConstructorParameters` - primary constructor

**StructDeclarationNode : TypeDeclarationNode**

- `bool IsReadOnly` - readonly struct

**InterfaceDeclarationNode : TypeDeclarationNode**

- `IReadOnlyList<TypeNode> BaseInterfaces` - базовые интерфейсы

**FieldDeclarationNode : DeclarationNode**

- `TypeNode Type` - тип поля
- `ExpressionNode? DefaultValue` - значение по умолчанию
- `AccessModifier AccessModifier` - модификатор доступа
- `bool IsReadOnly` - readonly
- `bool IsStatic` - static

**ConstructorDeclarationNode : DeclarationNode**

- `IReadOnlyList<ParameterDeclarationNode> Parameters` - параметры
- `BlockNode Body` - тело конструктора
- `AccessModifier AccessModifier` - модификатор доступа
- `IReadOnlyList<ExpressionNode> BaseConstructorArguments` - аргументы базового конструктора

**CompilationUnitNode : AstNode**

- `IReadOnlyList<UsingDirectiveNode> Usings` - using директивы
- `IReadOnlyList<NamespaceDeclarationNode> Namespaces` - namespaces
- `IReadOnlyList<DeclarationNode> Declarations` - объявления верхнего уровня

### 3.3 TypeScript специфичные узлы

**SourceFileNode : AstNode**

- `IReadOnlyList<ImportNode> Imports` - импорты
- `IReadOnlyList<ExportNode> Exports` - экспорты
- `IReadOnlyList<DeclarationNode> Declarations` - объявления

**TypeAliasDeclarationNode : DeclarationNode**

- `TypeNode Type` - тип алиаса

**FunctionDeclarationNode : DeclarationNode**

- `TypeNode? ReturnType` - возвращаемый тип
- `IReadOnlyList<ParameterDeclarationNode> Parameters` - параметры
- `BlockNode? Body` - тело функции (null для type-only)
- `bool IsArrowFunction` - arrow function
- `bool IsAsync` - async

**VariableDeclarationNode : DeclarationNode**

- `TypeNode? Type` - тип переменной
- `ExpressionNode? Initializer` - инициализатор
- `VariableDeclarationKind Kind` - let/const/var

**ObjectLiteralExpressionNode : ExpressionNode**

- `IReadOnlyList<PropertyAssignmentNode> Properties` - свойства

**PropertyAssignmentNode : AstNode**

- `IdentifierNode Name` - имя свойства
- `ExpressionNode Value` - значение
- `bool IsShorthand` - shorthand (для { name } синтаксиса)

**TemplateLiteralExpressionNode : ExpressionNode**

- `IReadOnlyList<TemplateLiteralPart> Parts` - части template literal

### 3.4 Стек-специфичные узлы (HTTP)

**HttpMethodNode : AstNode**

- `HttpMethodKind Method` - GET/POST/PUT/DELETE/etc.

**RouteNode : AstNode**

- `string Path` - путь
- `IReadOnlyList<PathParameterNode> PathParameters` - path параметры

**PathParameterNode : AstNode**

- `IdentifierNode Name` - имя параметра
- `TypeNode Type` - тип

**QueryParameterNode : AstNode**

- `IdentifierNode Name` - имя параметра
- `TypeNode Type` - тип
- `bool IsRequired` - required

**HeaderParameterNode : AstNode**

- `IdentifierNode Name` - имя параметра
- `TypeNode Type` - тип
- `bool IsRequired` - required

**RequestBodyNode : AstNode**

- `TypeNode Type` - тип body
- `string? ContentType` - content type
- `bool IsRequired` - required
- `RequestBodyKind Kind` - JSON/FormData/MultipartFormData

**ResponseTypeNode : AstNode**

- `TypeNode Type` - тип ответа
- `int StatusCode` - HTTP статус код
- `bool IsSuccess` - успешный ответ

### 3.5 Стек-специфичные узлы (Axios)

**AxiosCallExpressionNode : ExpressionNode**

- `ExpressionNode Instance` - axios instance
- `AxiosConfigNode Config` - конфигурация

**AxiosConfigNode : AstNode**

- `HttpMethodNode Method` - HTTP метод
- `ExpressionNode Url` - URL
- `IReadOnlyList<QueryParameterNode> QueryParameters` - query параметры
- `IReadOnlyList<HeaderParameterNode> Headers` - headers
- `RequestBodyNode? Body` - body
- `ExpressionNode? Signal` - AbortSignal
- `ExpressionNode? Timeout` - timeout

### 3.6 Стек-специфичные узлы (Zod)

**ZodSchemaNode : AstNode**

- `ZodSchemaKind Kind` - object/array/union/etc.
- `IReadOnlyList<ZodPropertyNode> Properties` - свойства (для object)
- `TypeNode? ElementType` - тип элементов (для array)
- `IReadOnlyList<ZodSchemaNode>? Variants` - варианты (для union)

**ZodPropertyNode : AstNode**

- `IdentifierNode Name` - имя свойства
- `ZodSchemaNode Schema` - схема
- `bool IsRequired` - required
- `bool IsNullable` - nullable
- `bool IsOptional` - optional

**ZodObjectNode : ZodSchemaNode**

- `IReadOnlyList<ZodPropertyNode> Properties` - свойства

**ZodArrayNode : ZodSchemaNode**

- `ZodSchemaNode ElementSchema` - схема элементов

**ZodUnionNode : ZodSchemaNode**

- `IReadOnlyList<ZodSchemaNode> Variants` - варианты

**ZodEnumNode : ZodSchemaNode**

- `IReadOnlyList<ZodEnumValueNode> Values` - значения

**ZodEnumValueNode : AstNode**

- `IdentifierNode Name` - имя значения
- `LiteralExpressionNode Value` - значение

### 3.7 Стек-специфичные узлы (TanStack Query)

**QueryKeyNode : ExpressionNode**

- `IReadOnlyList<ExpressionNode> Parts` - части ключа

**UseQueryNode : ExpressionNode**

- `ExpressionNode QueryKey` - query key
- `ExpressionNode QueryFn` - query function
- `QueryOptionsNode? Options` - опции

**UseMutationNode : ExpressionNode**

- `ExpressionNode MutationFn` - mutation function
- `MutationOptionsNode? Options` - опции

**QueryOptionsNode : AstNode**

- `ExpressionNode? Enabled` - enabled
- `ExpressionNode? StaleTime` - stale time
- `ExpressionNode? CacheTime` - cache time
- `ExpressionNode? RefetchOnMount` - refetch on mount

**MutationOptionsNode : AstNode**

- `ExpressionNode? OnSuccess` - onSuccess callback
- `ExpressionNode? OnError` - onError callback
- `ExpressionNode? OnSettled` - onSettled callback

## 4. Паттерны проектирования

### 4.1 Builder Pattern

**Назначение:** Построение сложных AST деревьев

**Классы:**

- `IAstBuilder` - базовый интерфейс
- `CSharpAstBuilder : IAstBuilder` - построитель для C#
- `TypeScriptAstBuilder : IAstBuilder` - построитель для TypeScript
- `HttpClientAstBuilder` - построитель для HTTP клиентов (language-agnostic)
- `AxiosClientAstBuilder : HttpClientAstBuilder` - построитель для Axios
- `TanStackQueryAstBuilder` - построитель для TanStack Query хуков

**Методы Builder'ов:**

```csharp
// CSharpAstBuilder
CompilationUnitNode BuildCompilationUnit(ModelTemplateData data);
NamespaceDeclarationNode BuildNamespace(string namespaceName, IReadOnlyList<DeclarationNode> declarations);
ClassDeclarationNode BuildClass(ModelTemplateData data);
RecordDeclarationNode BuildRecord(ModelTemplateData data);
PropertyDeclarationNode BuildProperty(PropertyTemplateData data);
MethodDeclarationNode BuildMethod(OperationTemplateData data);

// TypeScriptAstBuilder
SourceFileNode BuildSourceFile(ClientTemplateData data);
InterfaceDeclarationNode BuildInterface(ModelTemplateData data);
TypeAliasDeclarationNode BuildTypeAlias(string name, TypeNode type);
FunctionDeclarationNode BuildFunction(OperationTemplateData data);
```

### 4.2 Visitor Pattern

**Назначение:** Обход и рендеринг AST

**Классы:**

- `IAstVisitor<TResult>` - базовый интерфейс посетителя
- `IAstRenderer : IAstVisitor<string>` - интерфейс рендерера
- `CSharpAstRenderer : IAstRenderer` - рендерер для C#
- `TypeScriptAstRenderer : IAstRenderer` - рендерер для TypeScript
- `AstFormatter` - форматирование (отступы, переносы строк)
- `AstValidator` - валидация AST

**Методы Visitor'ов:**

```csharp
// IAstVisitor<TResult>
TResult VisitCompilationUnit(CompilationUnitNode node);
TResult VisitNamespace(NamespaceDeclarationNode node);
TResult VisitClass(ClassDeclarationNode node);
TResult VisitMethod(MethodDeclarationNode node);
TResult VisitProperty(PropertyDeclarationNode node);
// ... и т.д. для каждого типа узла
```

### 4.3 Factory Pattern

**Назначение:** Создание правильных Builder'ов и Renderer'ов

**Классы:**

- `IAstBuilderFactory` - фабрика Builder'ов
- `IAstRendererFactory` - фабрика Renderer'ов
- `CSharpAstBuilderFactory : IAstBuilderFactory`
- `TypeScriptAstBuilderFactory : IAstBuilderFactory`

**Методы Factory:**

```csharp
IAstBuilder CreateBuilder(GenerationLanguage language);
IAstRenderer CreateRenderer(GenerationLanguage language);
HttpClientAstBuilder CreateHttpClientBuilder(HttpClientLibrary library);
```

### 4.4 Strategy Pattern

**Назначение:** Разные стратегии именования, форматирования, сериализации

**Классы:**

- `INamingStrategy` - стратегия именования (уже есть INamingConverter)
- `IFormattingStrategy` - стратегия форматирования (отступы, стиль)
- `ITypeSerializationStrategy` - стратегия сериализации типов

### 4.5 Prototype Pattern

**Назначение:** Клонирование AST узлов для повторного использования

**Классы:**

- `IAstNodeCloner` - клонирование узлов
- `AstNodeCloner : IAstNodeCloner` - реализация

**Использование:**

- Шаблонные узлы (например, стандартная структура HTTP метода)
- Копирование узлов при трансформациях

## 5. Пошаговый план рефакторинга

### Этап 1: Подготовка инфраструктуры AST (Week 1)

#### Шаг 1.1: Создание базовых AST узлов

**Файлы для создания:**

- `ValIO.Core/Ast/Nodes/AstNode.cs` - базовый класс
- `ValIO.Core/Ast/Nodes/DeclarationNode.cs` - базовый класс объявлений
- `ValIO.Core/Ast/Nodes/TypeNode.cs` - базовый класс типов
- `ValIO.Core/Ast/Nodes/ExpressionNode.cs` - базовый класс выражений
- `ValIO.Core/Ast/Nodes/StatementNode.cs` - базовый класс statements
- `ValIO.Core/Ast/Enums/AstNodeKind.cs` - enum типов узлов
- `ValIO.Core/Ast/Enums/AccessModifier.cs` - enum модификаторов доступа

**Классы и методы:**

**AstNode.cs:**

```csharp
public abstract class AstNode
{
    public abstract AstNodeKind Kind { get; }
    public IReadOnlyList<AstNode> Children { get; }
    public IDictionary<string, object> Metadata { get; }
    
    public abstract TResult Accept<TResult>(IAstVisitor<TResult> visitor);
    public abstract AstNode Clone();
}
```

**DeclarationNode.cs:**

```csharp
public abstract class DeclarationNode : AstNode
{
    public IdentifierNode Name { get; }
    public CommentNode? Documentation { get; }
    public IReadOnlyList<AttributeNode> Attributes { get; }
}
```

#### Шаг 1.2: Создание общих узлов типов

**Файлы:**

- `ValIO.Core/Ast/Nodes/Types/SimpleTypeNode.cs`
- `ValIO.Core/Ast/Nodes/Types/GenericTypeNode.cs`
- `ValIO.Core/Ast/Nodes/Types/NullableTypeNode.cs`
- `ValIO.Core/Ast/Nodes/Types/ArrayTypeNode.cs`
- `ValIO.Core/Ast/Nodes/Types/UnionTypeNode.cs`
- `ValIO.Core/Ast/Nodes/Types/IntersectionTypeNode.cs`
- `ValIO.Core/Ast/Nodes/Types/DictionaryTypeNode.cs`

**Методы для каждого узла:**

- Конструктор с параметрами
- `Accept<TResult>(IAstVisitor<TResult> visitor)`
- `Clone()`
- Валидация (в конструкторе)

#### Шаг 1.3: Создание общих узлов выражений

**Файлы:**

- `ValIO.Core/Ast/Nodes/Expressions/LiteralExpressionNode.cs`
- `ValIO.Core/Ast/Nodes/Expressions/IdentifierExpressionNode.cs`
- `ValIO.Core/Ast/Nodes/Expressions/CallExpressionNode.cs`
- `ValIO.Core/Ast/Nodes/Expressions/MemberAccessExpressionNode.cs`
- `ValIO.Core/Ast/Nodes/Expressions/BinaryExpressionNode.cs`

#### Шаг 1.4: Создание вспомогательных узлов

**Файлы:**

- `ValIO.Core/Ast/Nodes/IdentifierNode.cs`
- `ValIO.Core/Ast/Nodes/CommentNode.cs`
- `ValIO.Core/Ast/Nodes/AttributeNode.cs`
- `ValIO.Core/Ast/Nodes/BlockNode.cs`
- `ValIO.Core/Ast/Nodes/ImportNode.cs`
- `ValIO.Core/Ast/Nodes/ExportNode.cs`

#### Шаг 1.5: Создание Visitor интерфейса

**Файлы:**

- `ValIO.Core/Ast/Visitors/IAstVisitor.cs` - базовый интерфейс
- `ValIO.Core/Ast/Visitors/IAstRenderer.cs` - интерфейс рендерера

**IAstVisitor.cs:**

```csharp
public interface IAstVisitor<TResult>
{
    TResult Visit(AstNode node);
    TResult VisitDeclaration(DeclarationNode node);
    TResult VisitType(TypeNode node);
    TResult VisitExpression(ExpressionNode node);
    TResult VisitStatement(StatementNode node);
    // ... методы для каждого конкретного типа узла
}
```

### Этап 2: C# специфичные AST узлы (Week 2)

#### Шаг 2.1: Создание C# узлов объявлений

**Файлы:**

- `ValIO.Generators.CSharp/Ast/Nodes/CompilationUnitNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/NamespaceDeclarationNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/UsingDirectiveNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/ClassDeclarationNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/RecordDeclarationNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/StructDeclarationNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/InterfaceDeclarationNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/EnumDeclarationNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/PropertyDeclarationNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/FieldDeclarationNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/MethodDeclarationNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/ConstructorDeclarationNode.cs`
- `ValIO.Generators.CSharp/Ast/Nodes/ParameterDeclarationNode.cs`

#### Шаг 2.2: Создание C# Builder'а

**Файлы:**

- `ValIO.Generators.CSharp/Ast/Builders/ICSharpAstBuilder.cs`
- `ValIO.Generators.CSharp/Ast/Builders/CSharpAstBuilder.cs`

**Методы CSharpAstBuilder:**

```csharp
public class CSharpAstBuilder : IAstBuilder
{
    CompilationUnitNode BuildCompilationUnit(ModelTemplateData data);
    CompilationUnitNode BuildCompilationUnit(ClientTemplateData data);
    CompilationUnitNode BuildCompilationUnit(EnumTemplateData data);
    
    NamespaceDeclarationNode BuildNamespace(string name, IReadOnlyList<DeclarationNode> declarations);
    
    ClassDeclarationNode BuildClass(ModelTemplateData data);
    RecordDeclarationNode BuildRecord(ModelTemplateData data);
    StructDeclarationNode BuildStruct(ModelTemplateData data);
    
    PropertyDeclarationNode BuildProperty(PropertyTemplateData data);
    MethodDeclarationNode BuildMethod(OperationTemplateData data);
    ConstructorDeclarationNode BuildConstructor(/* параметры */);
    
    TypeNode BuildType(TypeInfo typeInfo);
    ExpressionNode BuildLiteral(object value, TypeInfo typeInfo);
    // ... и т.д.
}
```

**Процесс построения модели:**

1. `BuildCompilationUnit(ModelTemplateData)` вызывает:

   - `BuildUsingDirectives(List<string> dependencies)` - using директивы
   - `BuildNamespace(string name)` - namespace
   - `BuildClass/Record/Struct(ModelTemplateData)` - тип

2. `BuildClass(ModelTemplateData)` вызывает:

   - `BuildProperties(List<PropertyTemplateData>)` - свойства
   - `BuildBaseClass(string baseClassName)` - базовый класс (если есть)

3. `BuildProperty(PropertyTemplateData)` вызывает:

   - `BuildType(TypeInfo)` - тип свойства
   - `BuildAttributes(PropertyTemplateData)` - атрибуты валидации
   - `BuildComment(string description)` - XML комментарий

#### Шаг 2.3: Создание C# Renderer'а

**Файлы:**

- `ValIO.Generators.CSharp/Ast/Renderers/ICSharpAstRenderer.cs`
- `ValIO.Generators.CSharp/Ast/Renderers/CSharpAstRenderer.cs`
- `ValIO.Generators.CSharp/Ast/Renderers/CSharpFormatter.cs` - форматирование

**Методы CSharpAstRenderer:**

```csharp
public class CSharpAstRenderer : IAstRenderer
{
    string Render(CompilationUnitNode node);
    string Render(NamespaceDeclarationNode node);
    string Render(ClassDeclarationNode node);
    string Render(RecordDeclarationNode node);
    string Render(PropertyDeclarationNode node);
    string Render(MethodDeclarationNode node);
    string Render(TypeNode node);
    string Render(ExpressionNode node);
    string Render(StatementNode node);
    // ... методы для каждого типа узла
}
```

**Процесс рендеринга:**

1. `Render(CompilationUnitNode)` вызывает:

   - `RenderUsingDirectives(List<UsingDirectiveNode>)` - using директивы
   - `RenderNamespace(NamespaceDeclarationNode)` - namespace

2. `RenderNamespace(NamespaceDeclarationNode)` вызывает:

   - `Render(DeclarationNode)` для каждого объявления

3. `Render(ClassDeclarationNode)` вызывает:

   - `RenderComment(CommentNode?)` - XML комментарий
   - `RenderAttributes(List<AttributeNode>)` - атрибуты
   - `RenderClassDeclaration(ClassDeclarationNode)` - объявление класса
   - `RenderProperties(List<PropertyDeclarationNode>)` - свойства
   - `RenderMethods(List<MethodDeclarationNode>)` - методы

#### Шаг 2.4: Рефакторинг CSharpCodeGenerator

**Файл:** `ValIO.Generators.CSharp/CSharpCodeGenerator.cs`

**Изменения:**

1. Заменить `CSharpCodeRenderer` на `CSharpAstBuilder` + `CSharpAstRenderer`
2. В методе `GenerateModels`:
   ```csharp
   // Было:
   var modelCode = modelRenderer.Render(typedModelData);
   
   // Станет:
   var astBuilder = new CSharpAstBuilder(namingConverter, typeMapper);
   var compilationUnit = astBuilder.BuildCompilationUnit(typedModelData);
   var astRenderer = new CSharpAstRenderer();
   var modelCode = astRenderer.Render(compilationUnit);
   ```

3. В методе `GenerateClients` - аналогично
4. В методе `GenerateEnums` - аналогично

### Этап 3: TypeScript специфичные AST узлы (Week 3)

#### Шаг 3.1: Создание TypeScript узлов

**Файлы:**

- `ValIO.Generators.TypeScript/Ast/Nodes/SourceFileNode.cs`
- `ValIO.Generators.TypeScript/Ast/Nodes/InterfaceDeclarationNode.cs`
- `ValIO.Generators.TypeScript/Ast/Nodes/TypeAliasDeclarationNode.cs`
- `ValIO.Generators.TypeScript/Ast/Node