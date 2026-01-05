---
name: AST Refactoring Plan - TypeScript and Stack-Specific Nodes
overview: ""
todos: []
---

# План рефакторинга кодогенерации на AST-ориентированную архитектуру

## Продолжение с Этапа 3: TypeScript специфичные AST узлы

---

## Часть 1: Детальный анализ текущей реализации TypeScript генерации

### 1.1 Анализ TypeScriptCodeRenderer

**Файл:** `ValIO.Generators.TypeScript/TypeScriptCodeRenderer.cs` (953 строки)

#### 1.1.1 Текущая структура методов

**Публичные методы:**

- `RenderModel(IModelTemplateData)` - генерация интерфейсов/типов через StringBuilder
- `RenderEnum(EnumTemplateData)` - генерация enum/union types через StringBuilder
- `RenderClient(IClientTemplateData)` - генерация HTTP клиента (Fetch или Axios)
- `RenderOperation(IOperationTemplateData)` - генерация метода HTTP клиента
- `RenderImports(IReadOnlyList<string>, string?)` - генерация import директив

**Приватные методы:**

- `RenderFetchClient(StringBuilder, IClientTemplateData)` - генерация Fetch клиента
- `RenderAxiosClient(StringBuilder, IClientTemplateData)` - генерация Axios клиента
- `RenderFetchOperation(StringBuilder, IOperationTemplateData)` - генерация Fetch метода
- `RenderAxiosOperation(StringBuilder, IOperationTemplateData, ...)` - генерация Axios метода
- `GenerateProperty(StringBuilder, IPropertyTemplateData, int)` - генерация свойства интерфейса
- `BuildParameterList(IOperationTemplateData)` - построение списка параметров
- `BuildPath(string, IReadOnlyList<ParameterTemplateData>)` - построение пути с подстановкой параметров
- `GenerateQueryParameters(StringBuilder, IOperationTemplateData, int)` - генерация query параметров
- `GenerateHeaders(StringBuilder, IOperationTemplateData, int)` - генерация headers
- `GenerateRequestBody(StringBuilder, IOperationTemplateData, int, bool)` - генерация request body
- `GenerateResponseHandling(StringBuilder, IOperationTemplateData, int, bool, bool)` - генерация обработки ответа

#### 1.1.2 Проблемы текущей реализации

1. **Дублирование логики между Fetch и Axios:**

   - `RenderFetchOperation` и `RenderAxiosOperation` содержат похожую логику построения URL, обработки параметров, обработки body
   - Различается только синтаксис вызова (`fetch(...)` vs `axios.request({...})`)
   - Невозможно переиспользовать общие части

2. **Смешение структуры и синтаксиса:**

   - Методы знают и о структуре данных (`OperationTemplateData`), и о синтаксисе TypeScript (`StringBuilder.AppendLine("export async function ...")`)
   - Невозможно проверить структуру кода без парсинга строк

3. **Слабая типизация:**

   - Работа с типами через строки (`FullTypeName`)
   - Невозможно проверить корректность типа на этапе компиляции

4. **Нет возможности трансформаций:**

   - Нельзя модифицировать структуру кода до рендеринга
   - Нельзя добавить дополнительные методы, изменить порядок параметров

### 1.2 Анализ TanStackQueryRenderer

**Файл:** `ValIO.Generators.TypeScript.TanStackQuery/TanStackQueryRenderer.cs` (1551 строка)

#### 1.2.1 Текущая структура методов

**Публичные методы:**

- `RenderQueryHook(OperationTemplateData, string?, object?)` - генерация `useQuery` хука
- `RenderMutationHook(OperationTemplateData, string?, object?)` - генерация `useMutation` хука

**Приватные методы:**

- `RenderQueryKeyFunction(StringBuilder, OperationTemplateData, string)` - генерация функции для query key
- `RenderQueryHookFunction(StringBuilder, OperationTemplateData, string, string, string, TypeScriptGenerationOptions)` - генерация самого хука
- `RenderMutationHookFunction(StringBuilder, OperationTemplateData, string, string, string, TypeScriptGenerationOptions)` - генерация mutation хука
- `RenderClientFunction(StringBuilder, OperationTemplateData, string, TypeScriptGenerationOptions)` - генерация функции клиента
- `RenderTypeExports(StringBuilder, OperationTemplateData, string)` - генерация type exports (`ResultType`, `AwaitedResultType`)
- `RenderQueryOptionsHelper(StringBuilder, OperationTemplateData, string, string, string, TypeScriptGenerationOptions)` - генерация helper для query options
- `RenderInfiniteQuerySupport(StringBuilder, OperationTemplateData, string, string, TypeScriptGenerationOptions)` - генерация infinite query support
- `RenderPrefetchSupport(StringBuilder, OperationTemplateData, string, string, string, TypeScriptGenerationOptions)` - генерация prefetch support
- `RenderMutationOptionsHelper(StringBuilder, OperationTemplateData, string, string, TypeScriptGenerationOptions)` - генерация mutation options helper
- `BuildQueryKeyParameters(OperationTemplateData)` - построение параметров для query key
- `GenerateHookName(string, bool)` - генерация имени хука

#### 1.2.2 Проблемы текущей реализации

1. **Огромный файл (1551 строка):**

   - Вся логика через StringBuilder
   - Сложно тестировать отдельные части
   - Невозможно переиспользовать части между query и mutation

2. **Слабая композиция:**

   - TanStack Query хуки зависят от axios/fetch методов, но генерируются отдельно
   - Невозможно переиспользовать AST узлы

3. **Дублирование логики:**

   - `RenderQueryHookFunction` и `RenderMutationHookFunction` содержат похожую логику
   - Различается только тип хука (`useQuery` vs `useMutation`)

### 1.3 Анализ ZodSchemaRenderer

**Файл:** `ValIO.Generators.TypeScript.Zod/ZodSchemaRenderer.cs` (1010 строк)

#### 1.3.1 Текущая структура методов

**Публичные методы:**

- `Render(IModelTemplateData)` - базовый метод (выбрасывает исключение)
- `Render(IModelTemplateData, OpenApiDocument, ISchemaResolver)` - генерация Zod схемы

**Приватные методы:**

- `RenderExpression(ZodSchemaExpression)` - рендерит ZodSchemaExpression в строку
- `CreateZodProperty(PropertyTemplateData, string)` - создает ZodPropertyTemplateData
- `FindSchemaInDocument(string, OpenApiDocument)` - находит схему в OpenAPI документе
- `CreateFallbackZodModel(IModelTemplateData)` - создает fallback Zod модель
- `CreateZodModelTemplateData(IModelTemplateData, IOpenApiSchema, OpenApiDocument, ISchemaResolver)` - создает ModelTemplateData с Zod опциями

#### 1.3.2 Проблемы текущей реализации

1. **Сложная логика обработки allOf/oneOf/anyOf:**

   - Работа через строки (`ZodSchemaExpression`)
   - Невозможно проверить корректность схемы до рендеринга

2. **Слабая типизация:**

   - Работа с типами через строки
   - Невозможно проверить корректность типа на этапе компиляции

---

## Часть 2: Детальный список TypeScript специфичных AST узлов

### 2.1 Базовые TypeScript узлы

#### 2.1.1 SourceFileNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/SourceFileNode.cs`

**Свойства:**

- `IReadOnlyList<ImportNode> Imports { get; }` - импорты
- `IReadOnlyList<ExportNode> Exports { get; }` - экспорты
- `IReadOnlyList<DeclarationNode> Declarations { get; }` - объявления (interfaces, types, functions, etc.)
- `IReadOnlyList<CommentNode>? FileHeaderComments { get; }` - комментарии в начале файла

**Использование:** Представляет TypeScript файл целиком

**Методы:**

- `Accept<TResult>(IAstVisitor<TResult> visitor)` - Visitor pattern
- `Clone()` - клонирование узла

#### 2.1.2 TypeAliasDeclarationNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/TypeAliasDeclarationNode.cs`

**Наследуется от:** `DeclarationNode`

**Свойства:**

- `TypeNode Type { get; }` - тип алиаса
- `IReadOnlyList<GenericParameterNode>? GenericParameters { get; }` - generic параметры (опционально)

**Использование:** Представляет `export type Name = Type;`

**Пример:**

```typescript
export type User = BaseUser & { email: string };
```

#### 2.1.3 InterfaceDeclarationNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/InterfaceDeclarationNode.cs`

**Наследуется от:** `TypeDeclarationNode`

**Свойства:**

- `IReadOnlyList<PropertySignatureNode> Properties { get; }` - свойства интерфейса
- `IReadOnlyList<TypeNode>? Extends { get; }` - базовые интерфейсы (для `extends`)
- `IReadOnlyList<GenericParameterNode>? GenericParameters { get; }` - generic параметры

**Использование:** Представляет `export interface Name { ... }`

#### 2.1.4 PropertySignatureNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/PropertySignatureNode.cs`

**Наследуется от:** `DeclarationNode`

**Свойства:**

- `TypeNode Type { get; }` - тип свойства
- `bool IsRequired { get; }` - required (не optional)
- `bool IsReadonly { get; }` - readonly модификатор

**Использование:** Представляет свойство интерфейса `name: Type;` или `name?: Type;`

#### 2.1.5 FunctionDeclarationNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/FunctionDeclarationNode.cs`

**Наследуется от:** `DeclarationNode`

**Свойства:**

- `TypeNode? ReturnType { get; }` - возвращаемый тип (null для void)
- `IReadOnlyList<ParameterDeclarationNode> Parameters { get; }` - параметры функции
- `BlockNode? Body { get; }` - тело функции (null для type-only)
- `bool IsArrowFunction { get; }` - arrow function (`const fn = () => {}`)
- `bool IsAsync { get; }` - async функция
- `bool IsExport { get; }` - export модификатор

**Использование:** Представляет функцию `export async function name(...) { ... }` или `const name = (...) => { ... }`

#### 2.1.6 VariableDeclarationNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/VariableDeclarationNode.cs`

**Наследуется от:** `DeclarationNode`

**Свойства:**

- `TypeNode? Type { get; }` - тип переменной (опционально, для type inference)
- `ExpressionNode? Initializer { get; }` - инициализатор
- `VariableDeclarationKind Kind { get; }` - enum: `Let`, `Const`, `Var`

**Использование:** Представляет `const name: Type = value;`

#### 2.1.7 ObjectLiteralExpressionNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/ObjectLiteralExpressionNode.cs`

**Наследуется от:** `ExpressionNode`

**Свойства:**

- `IReadOnlyList<PropertyAssignmentNode> Properties { get; }` - свойства объекта

**Использование:** Представляет `{ prop1: value1, prop2: value2 }`

#### 2.1.8 PropertyAssignmentNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/PropertyAssignmentNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `IdentifierNode Name { get; }` - имя свойства
- `ExpressionNode Value { get; }` - значение
- `bool IsShorthand { get; }` - shorthand (`{ name }` вместо `{ name: name }`)

**Использование:** Представляет свойство объекта `name: value` или `name` (shorthand)

#### 2.1.9 TemplateLiteralExpressionNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/TemplateLiteralExpressionNode.cs`

**Наследуется от:** `ExpressionNode`

**Свойства:**

- `IReadOnlyList<TemplateLiteralPart> Parts { get; }` - части template literal

**Использование:** Представляет `` `string ${expr} string` ``

#### 2.1.10 GenericParameterNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/GenericParameterNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `IdentifierNode Name { get; }` - имя generic параметра
- `TypeNode? Constraint { get; }` - ограничение (например, `extends Type`)
- `TypeNode? Default { get; }` - значение по умолчанию

**Использование:** Представляет `<T extends BaseType = DefaultType>`

### 2.2 Enum узлы для TypeScript

#### 2.2.1 TypeScriptEnumDeclarationNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/TypeScriptEnumDeclarationNode.cs`

**Наследуется от:** `TypeDeclarationNode`

**Свойства:**

- `IReadOnlyList<TypeScriptEnumMemberNode> Members { get; }` - члены enum
- `bool IsConstEnum { get; }` - const enum (`const enum Name { ... }`)

**Использование:** Представляет `export enum Name { ... }` или `export const enum Name { ... }`

#### 2.2.2 TypeScriptEnumMemberNode

**Файл:** `ValIO.Core/Ast/Nodes/TypeScript/TypeScriptEnumMemberNode.cs`

**Наследуется от:** `DeclarationNode`

**Свойства:**

- `ExpressionNode? Value { get; }` - значение enum (null для auto-increment)

**Использование:** Представляет `Name = value` или `Name` (auto-increment)

---

## Часть 3: Детальный список стек-специфичных AST узлов

### 3.1 HTTP специфичные узлы (Language-agnostic)

#### 3.1.1 HttpMethodNode

**Файл:** `ValIO.Core/Ast/Nodes/Http/HttpMethodNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `HttpMethodKind Method { get; }` - enum: `Get`, `Post`, `Put`, `Delete`, `Patch`, `Head`, `Options`

**Использование:** Представляет HTTP метод

#### 3.1.2 RouteNode

**Файл:** `ValIO.Core/Ast/Nodes/Http/RouteNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `string Path { get; }` - путь (например, "/api/users/{id}")
- `IReadOnlyList<PathParameterNode> PathParameters { get; }` - path параметры

**Использование:** Представляет HTTP route

#### 3.1.3 PathParameterNode

**Файл:** `ValIO.Core/Ast/Nodes/Http/PathParameterNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `IdentifierNode Name { get; }` - имя параметра
- `TypeNode Type { get; }` - тип параметра

**Использование:** Представляет path параметр `{id}`

#### 3.1.4 QueryParameterNode

**Файл:** `ValIO.Core/Ast/Nodes/Http/QueryParameterNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `IdentifierNode Name { get; }` - имя параметра
- `TypeNode Type { get; }` - тип параметра
- `bool IsRequired { get; }` - required

**Использование:** Представляет query параметр

#### 3.1.5 HeaderParameterNode

**Файл:** `ValIO.Core/Ast/Nodes/Http/HeaderParameterNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `IdentifierNode Name { get; }` - имя параметра
- `TypeNode Type { get; }` - тип параметра
- `bool IsRequired { get; }` - required

**Использование:** Представляет header параметр

#### 3.1.6 RequestBodyNode

**Файл:** `ValIO.Core/Ast/Nodes/Http/RequestBodyNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `TypeNode Type { get; }` - тип body
- `string? ContentType { get; }` - content type (например, "application/json")
- `bool IsRequired { get; }` - required
- `RequestBodyKind Kind { get; }` - enum: `Json`, `FormData`, `MultipartFormData`, `Raw`

**Использование:** Представляет request body

#### 3.1.7 ResponseTypeNode

**Файл:** `ValIO.Core/Ast/Nodes/Http/ResponseTypeNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `TypeNode Type { get; }` - тип ответа
- `int StatusCode { get; }` - HTTP статус код
- `bool IsSuccess { get; }` - успешный ответ

**Использование:** Представляет тип ответа

### 3.2 Axios специфичные узлы

#### 3.2.1 AxiosCallExpressionNode

**Файл:** `ValIO.Generators.TypeScript.Axios/Ast/Nodes/AxiosCallExpressionNode.cs`

**Наследуется от:** `CallExpressionNode`

**Свойства:**

- `ExpressionNode Instance { get; }` - axios instance (например, `axios` или `customAxios`)
- `AxiosConfigNode Config { get; }` - конфигурация axios

**Использование:** Представляет `axios.request({ ... })` или `customAxios.request({ ... })`

#### 3.2.2 AxiosConfigNode

**Файл:** `ValIO.Generators.TypeScript.Axios/Ast/Nodes/AxiosConfigNode.cs`

**Наследуется от:** `ObjectLiteralExpressionNode`

**Свойства:**

- `HttpMethodNode Method { get; }` - HTTP метод
- `ExpressionNode Url { get; }` - URL (может быть template literal)
- `IReadOnlyList<QueryParameterNode> QueryParameters { get; }` - query параметры
- `IReadOnlyList<HeaderParameterNode> Headers { get; }` - headers
- `RequestBodyNode? Body { get; }` - body
- `ExpressionNode? Signal { get; }` - AbortSignal
- `ExpressionNode? Timeout { get; }` - timeout

**Использование:** Представляет конфигурацию axios `{ method: 'GET', url: '...', ... }`

### 3.3 Zod специфичные узлы

#### 3.3.1 ZodSchemaNode

**Файл:** `ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodSchemaNode.cs`

**Наследуется от:** `ExpressionNode`

**Свойства:**

- `ZodSchemaKind Kind { get; }` - enum: `Object`, `Array`, `Union`, `Intersection`, `Enum`, `String`, `Number`, `Boolean`, `Null`, `Any`, `Optional`, `Nullable`, `Effects`

**Использование:** Базовый узел для всех Zod схем

#### 3.3.2 ZodObjectNode

**Файл:** `ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodObjectNode.cs`

**Наследуется от:** `ZodSchemaNode`

**Свойства:**

- `IReadOnlyList<ZodPropertyNode> Properties { get; }` - свойства объекта

**Использование:** Представляет `z.object({ ... })`

#### 3.3.3 ZodArrayNode

**Файл:** `ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodArrayNode.cs`

**Наследуется от:** `ZodSchemaNode`

**Свойства:**

- `ZodSchemaNode ElementSchema { get; }` - схема элементов

**Использование:** Представляет `z.array(ElementSchema)`

#### 3.3.4 ZodUnionNode

**Файл:** `ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodUnionNode.cs`

**Наследуется от:** `ZodSchemaNode`

**Свойства:**

- `IReadOnlyList<ZodSchemaNode> Variants { get; }` - варианты union

**Использование:** Представляет `z.union([Schema1, Schema2, ...])`

#### 3.3.5 ZodIntersectionNode

**Файл:** `ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodIntersectionNode.cs`

**Наследуется от:** `ZodSchemaNode`

**Свойства:**

- `IReadOnlyList<ZodSchemaNode> Schemas { get; }` - схемы для intersection

**Использование:** Представляет `z.intersection(Schema1, Schema2)`

#### 3.3.6 ZodEnumNode

**Файл:** `ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEnumNode.cs`

**Наследуется от:** `ZodSchemaNode`

**Свойства:**

- `IReadOnlyList<ZodEnumValueNode> Values { get; }` - значения enum

**Использование:** Представляет `z.enum([...])`

#### 3.3.7 ZodEnumValueNode

**Файл:** `ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEnumValueNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `IdentifierNode Name { get; }` - имя значения
- `LiteralExpressionNode Value { get; }` - значение

**Использование:** Представляет значение enum

#### 3.3.8 ZodPropertyNode

**Файл:** `ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodPropertyNode.cs`

**Наследуется от:** `AstNode`

**Свойства:**

- `IdentifierNode Name { get; }` - имя свойства
- `ZodSchemaNode Schema { get; }` - схема свойства
- `bool IsRequired { get; }` - required
- `bool IsNullable { get; }` - nullable
- `bool IsOptional { get; }` - optional

**Использование:** Представляет свойство объекта Zod схемы

#### 3.3.9 ZodEffectsNode

**Файл:** `ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEffectsNode.cs`

**Наследуется от:** `ZodSchemaNode`

**Свойства:**

- `ZodSchemaNode InnerSchema { get; }` - внутренняя схема
- `ZodEffectKind EffectKind { get; }` - enum: `Refine`, `Transform`, `SuperRefine`
- `ExpressionNode? EffectFunction { get; }` - функция эффекта (опционально)

**Использование:** Представляет `z.string().min(1).max(100)` или другие эффекты

### 3.4 TanStack Query специфичные узлы

#### 3.4.1 QueryKeyNode

**Файл:** `ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/QueryKeyNode.cs`

**Наследуется от:** `ExpressionNode`

**Свойства:**

- `IReadOnlyList<ExpressionNode> Parts { get; }` - части ключа (например, `['users', id]`)

**Использование:** Представляет query key для TanStack Query

#### 3.4.2 UseQueryNode

**Файл:** `ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/UseQueryNode.cs`

**Наследуется от:** `CallExpressionNode`

**Свойства:**

- `ExpressionNode QueryKey { get; }` - query key (обычно `QueryKeyNode`)
- `ExpressionNode QueryFn { get; }` - query function (обычно `CallExpressionNode` с методом клиента)
- `QueryOptionsNode? Options { get; }` - опции query

**Использование:** Представляет `useQuery({ queryKey: [...], queryFn: ... })`

#### 3.4.3 UseMutationNode

**Файл:** `ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/UseMutationNode.cs`

**Наследуется от:** `CallExpressionNode`

**Свойства:**

- `ExpressionNode MutationFn { get; }` - mutation function
- `MutationOptionsNode? Options { get; }` - опции mutation

**Использование:** Представляет `useMutation({ mutationFn: ... })`

#### 3.4.4 QueryOptionsNode

**Файл:** `ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/QueryOptionsNode.cs`

**Наследуется от:** `ObjectLiteralExpressionNode`

**Свойства:**

- `ExpressionNode? Enabled { get; }` - enabled
- `ExpressionNode? StaleTime { get; }` - stale time
- `ExpressionNode? CacheTime { get; }` - cache time
- `ExpressionNode? RefetchOnMount { get; }` - refetch on mount
- `ExpressionNode? RefetchOnWindowFocus { get; }` - refetch on window focus
- `ExpressionNode? Retry { get; }` - retry

**Использование:** Представляет опции для `useQuery`

#### 3.4.5 MutationOptionsNode

**Файл:** `ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/MutationOptionsNode.cs`

**Наследуется от:** `ObjectLiteralExpressionNode`

**Свойства:**

- `ExpressionNode? OnSuccess { get; }` - onSuccess callback
- `ExpressionNode? OnError { get; }` - onError callback
- `ExpressionNode? OnSettled { get; }` - onSettled callback

**Использование:** Представляет опции для `useMutation`

#### 3.4.6 UseInfiniteQueryNode

**Файл:** `ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/UseInfiniteQueryNode.cs`

**Наследуется от:** `CallExpressionNode`

**Свойства:**

- `ExpressionNode QueryKey { get; }` - query key
- `ExpressionNode QueryFn { get; }` - query function с параметром `pageParam`
- `ExpressionNode? GetNextPageParam { get; }` - функция для получения следующей страницы
- `QueryOptionsNode? Options { get; }` - опции query

**Использование:** Представляет `useInfiniteQuery({ ... })`

---

## Часть 4: Детальный план рефакторинга TypeScript генерации

### Этап 3.1: Создание TypeScript специфичных AST узлов (Week 3, Days 1-2)

#### Шаг 3.1.1: Создание базовых TypeScript узлов

**Файлы для создания:**

1. **`ValIO.Core/Ast/Nodes/TypeScript/SourceFileNode.cs`**

   - Класс: `SourceFileNode : AstNode`
   - Свойства: `Imports`, `Exports`, `Declarations`, `FileHeaderComments`
   - Методы: `Accept<TResult>`, `Clone()`, вычисляемое свойство `Children`

2. **`ValIO.Core/Ast/Nodes/TypeScript/TypeAliasDeclarationNode.cs`**

   - Класс: `TypeAliasDeclarationNode : DeclarationNode`
   - Свойства: `Type`, `GenericParameters`
   - Методы: `Accept<TResult>`, `Clone()`

3. **`ValIO.Core/Ast/Nodes/TypeScript/InterfaceDeclarationNode.cs`**

   - Класс: `InterfaceDeclarationNode : TypeDeclarationNode`
   - Свойства: `Properties` (список `PropertySignatureNode`), `Extends`, `GenericParameters`
   - Методы: `Accept<TResult>`, `Clone()`

4. **`ValIO.Core/Ast/Nodes/TypeScript/PropertySignatureNode.cs`**

   - Класс: `PropertySignatureNode : DeclarationNode`
   - Свойства: `Type`, `IsRequired`, `IsReadonly`
   - Методы: `Accept<TResult>`, `Clone()`

5. **`ValIO.Core/Ast/Nodes/TypeScript/FunctionDeclarationNode.cs`**

   - Класс: `FunctionDeclarationNode : DeclarationNode`
   - Свойства: `ReturnType`, `Parameters`, `Body`, `IsArrowFunction`, `IsAsync`, `IsExport`
   - Методы: `Accept<TResult>`, `Clone()`

6. **`ValIO.Core/Ast/Nodes/TypeScript/VariableDeclarationNode.cs`**

   - Класс: `VariableDeclarationNode : DeclarationNode`
   - Свойства: `Type`, `Initializer`, `Kind`
   - Методы: `Accept<TResult>`, `Clone()`

7. **`ValIO.Core/Ast/Nodes/TypeScript/ObjectLiteralExpressionNode.cs`**

   - Класс: `ObjectLiteralExpressionNode : ExpressionNode`
   - Свойства: `Properties` (список `PropertyAssignmentNode`)
   - Методы: `Accept<TResult>`, `Clone()`

8. **`ValIO.Core/Ast/Nodes/TypeScript/PropertyAssignmentNode.cs`**

   - Класс: `PropertyAssignmentNode : AstNode`
   - Свойства: `Name`, `Value`, `IsShorthand`
   - Методы: `Accept<TResult>`, `Clone()`

9. **`ValIO.Core/Ast/Nodes/TypeScript/TemplateLiteralExpressionNode.cs`**

   - Класс: `TemplateLiteralExpressionNode : ExpressionNode`
   - Свойства: `Parts` (список `TemplateLiteralPart`)
   - Методы: `Accept<TResult>`, `Clone()`

10. **`ValIO.Core/Ast/Nodes/TypeScript/GenericParameterNode.cs`**

    - Класс: `GenericParameterNode : AstNode`
    - Свойства: `Name`, `Constraint`, `Default`
    - Методы: `Accept<TResult>`, `Clone()`

**Вспомогательные классы:**

11. **`ValIO.Core/Ast/Nodes/TypeScript/TemplateLiteralPart.cs`**

    - Класс: `TemplateLiteralPart` (record или class)
    - Свойства: `string Text` (для строковых частей), `ExpressionNode? Expression` (для выражений)
    - Использование: Представляет часть template literal

12. **`ValIO.Core/Ast/Enums/VariableDeclarationKind.cs`**

    - Enum: `VariableDeclarationKind` со значениями `Let`, `Const`, `Var`

#### Шаг 3.1.2: Создание TypeScript Enum узлов

**Файлы для создания:**

1. **`ValIO.Core/Ast/Nodes/TypeScript/TypeScriptEnumDeclarationNode.cs`**

   - Класс: `TypeScriptEnumDeclarationNode : TypeDeclarationNode`
   - Свойства: `Members` (список `TypeScriptEnumMemberNode`), `IsConstEnum`
   - Методы: `Accept<TResult>`, `Clone()`

2. **`ValIO.Core/Ast/Nodes/TypeScript/TypeScriptEnumMemberNode.cs`**

   - Класс: `TypeScriptEnumMemberNode : DeclarationNode`
   - Свойства: `Value` (опционально)
   - Методы: `Accept<TResult>`, `Clone()`

#### Шаг 3.1.3: Обновление Visitor интерфейса

**Файл:** `ValIO.Core/Ast/Visitors/IAstVisitor.cs`

**Добавить методы:**

```csharp
TResult VisitSourceFile(SourceFileNode node);
TResult VisitTypeAlias(TypeAliasDeclarationNode node);
TResult VisitInterface(InterfaceDeclarationNode node);
TResult VisitPropertySignature(PropertySignatureNode node);
TResult VisitFunction(FunctionDeclarationNode node);
TResult VisitVariable(VariableDeclarationNode node);
TResult VisitObjectLiteral(ObjectLiteralExpressionNode node);
TResult VisitPropertyAssignment(PropertyAssignmentNode node);
TResult VisitTemplateLiteral(TemplateLiteralExpressionNode node);
TResult VisitGenericParameter(GenericParameterNode node);
TResult VisitTypeScriptEnum(TypeScriptEnumDeclarationNode node);
TResult VisitTypeScriptEnumMember(TypeScriptEnumMemberNode node);
```

### Этап 3.2: Создание HTTP специфичных AST узлов (Week 3, Days 3-4)

#### Шаг 3.2.1: Создание HTTP узлов

**Файлы для создания:**

1. **`ValIO.Core/Ast/Nodes/Http/HttpMethodNode.cs`**

   - Класс: `HttpMethodNode : AstNode`
   - Свойства: `Method` (enum `HttpMethodKind`)
   - Методы: `Accept<TResult>`, `Clone()`

2. **`ValIO.Core/Ast/Nodes/Http/RouteNode.cs`**

   - Класс: `RouteNode : AstNode`
   - Свойства: `Path`, `PathParameters`
   - Методы: `Accept<TResult>`, `Clone()`

3. **`ValIO.Core/Ast/Nodes/Http/PathParameterNode.cs`**

   - Класс: `PathParameterNode : AstNode`
   - Свойства: `Name`, `Type`
   - Методы: `Accept<TResult>`, `Clone()`

4. **`ValIO.Core/Ast/Nodes/Http/QueryParameterNode.cs`**

   - Класс: `QueryParameterNode : AstNode`
   - Свойства: `Name`, `Type`, `IsRequired`
   - Методы: `Accept<TResult>`, `Clone()`

5. **`ValIO.Core/Ast/Nodes/Http/HeaderParameterNode.cs`**

   - Класс: `HeaderParameterNode : AstNode`
   - Свойства: `Name`, `Type`, `IsRequired`
   - Методы: `Accept<TResult>`, `Clone()`

6. **`ValIO.Core/Ast/Nodes/Http/RequestBodyNode.cs`**

   - Класс: `RequestBodyNode : AstNode`
   - Свойства: `Type`, `ContentType`, `IsRequired`, `Kind`
   - Методы: `Accept<TResult>`, `Clone()`

7. **`ValIO.Core/Ast/Nodes/Http/ResponseTypeNode.cs`**

   - Класс: `ResponseTypeNode : AstNode`
   - Свойства: `Type`, `StatusCode`, `IsSuccess`
   - Методы: `Accept<TResult>`, `Clone()`

**Enums:**

8. **`ValIO.Core/Ast/Enums/HttpMethodKind.cs`**

   - Enum: `HttpMethodKind` со значениями `Get`, `Post`, `Put`, `Delete`, `Patch`, `Head`, `Options`

9. **`ValIO.Core/Ast/Enums/RequestBodyKind.cs`**

   - Enum: `RequestBodyKind` со значениями `Json`, `FormData`, `MultipartFormData`, `Raw`

#### Шаг 3.2.2: Обновление Visitor интерфейса для HTTP узлов

**Файл:** `ValIO.Core/Ast/Visitors/IAstVisitor.cs`

**Добавить методы:**

```csharp
TResult VisitHttpMethod(HttpMethodNode node);
TResult VisitRoute(RouteNode node);
TResult VisitPathParameter(PathParameterNode node);
TResult VisitQueryParameter(QueryParameterNode node);
TResult VisitHeaderParameter(HeaderParameterNode node);
TResult VisitRequestBody(RequestBodyNode node);
TResult VisitResponseType(ResponseTypeNode node);
```

### Этап 3.3: Создание Axios специфичных AST узлов (Week 3, Days 5)

#### Шаг 3.3.1: Создание Axios узлов

**Файлы для создания:**

1. **`ValIO.Generators.TypeScript.Axios/Ast/Nodes/AxiosCallExpressionNode.cs`**

   - Класс: `AxiosCallExpressionNode : CallExpressionNode`
   - Свойства: `Instance`, `Config` (тип `AxiosConfigNode`)
   - Методы: `Accept<TResult>`, `Clone()`

2. **`ValIO.Generators.TypeScript.Axios/Ast/Nodes/AxiosConfigNode.cs`**

   - Класс: `AxiosConfigNode : ObjectLiteralExpressionNode`
   - Свойства: `Method`, `Url`, `QueryParameters`, `Headers`, `Body`, `Signal`, `Timeout`
   - Методы: `Accept<TResult>`, `Clone()`

#### Шаг 3.3.2: Обновление Visitor интерфейса для Axios узлов

**Файл:** `ValIO.Generators.TypeScript.Axios/Ast/Visitors/IAxiosAstVisitor.cs` (новый интерфейс)

**Методы:**

```csharp
TResult VisitAxiosCall(AxiosCallExpressionNode node);
TResult VisitAxiosConfig(AxiosConfigNode node);
```

### Этап 3.4: Создание Zod специфичных AST узлов (Week 4, Days 1-2)

#### Шаг 3.4.1: Создание Zod узлов

**Файлы для создания:**

1. **`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodSchemaNode.cs`**

   - Класс: `ZodSchemaNode : ExpressionNode` (abstract)
   - Свойства: `Kind` (enum `ZodSchemaKind`)
   - Методы: `Accept<TResult>`, `Clone()`

2. **`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodObjectNode.cs`**

   - Класс: `ZodObjectNode : ZodSchemaNode`
   - Свойства: `Properties` (список `ZodPropertyNode`)
   - Методы: `Accept<TResult>`, `Clone()`

3. **`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodArrayNode.cs`**

   - Класс: `ZodArrayNode : ZodSchemaNode`
   - Свойства: `ElementSchema`
   - Методы: `Accept<TResult>`, `Clone()`

4. **`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodUnionNode.cs`**

   - Класс: `ZodUnionNode : ZodSchemaNode`
   - Свойства: `Variants`
   - Методы: `Accept<TResult>`, `Clone()`

5. **`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodIntersectionNode.cs`**

   - Класс: `ZodIntersectionNode : ZodSchemaNode`
   - Свойства: `Schemas`
   - Методы: `Accept<TResult>`, `Clone()`

6. **`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEnumNode.cs`**

   - Класс: `ZodEnumNode : ZodSchemaNode`
   - Свойства: `Values` (список `ZodEnumValueNode`)
   - Методы: `Accept<TResult>`, `Clone()`

7. **`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEnumValueNode.cs`**

   - Класс: `ZodEnumValueNode : AstNode`
   - Свойства: `Name`, `Value`
   - Методы: `Accept<TResult>`, `Clone()`

8. **`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodPropertyNode.cs`**

   - Класс: `ZodPropertyNode : AstNode`
   - Свойства: `Name`, `Schema`, `IsRequired`, `IsNullable`, `IsOptional`
   - Методы: `Accept<TResult>`, `Clone()`

9. **`ValIO.Generators.TypeScript.Zod/Ast/Nodes/ZodEffectsNode.cs`**

   - Класс: `ZodEffectsNode : ZodSchemaNode`
   - Свойства: `InnerSchema`, `EffectKind`, `EffectFunction`
   - Методы: `Accept<TResult>`, `Clone()`

**Enums:**

10. **`ValIO.Generators.TypeScript.Zod/Ast/Enums/ZodSchemaKind.cs`**

    - Enum: `ZodSchemaKind` со значениями `Object`, `Array`, `Union`, `Intersection`, `Enum`, `String`, `Number`, `Boolean`, `Null`, `Any`, `Optional`, `Nullable`, `Effects`

11. **`ValIO.Generators.TypeScript.Zod/Ast/Enums/ZodEffectKind.cs`**

    - Enum: `ZodEffectKind` со значениями `Refine`, `Transform`, `SuperRefine`

#### Шаг 3.4.2: Обновление Visitor интерфейса для Zod узлов

**Файл:** `ValIO.Generators.TypeScript.Zod/Ast/Visitors/IZodAstVisitor.cs` (новый интерфейс)

**Методы:**

```csharp
TResult VisitZodSchema(ZodSchemaNode node);
TResult VisitZodObject(ZodObjectNode node);
TResult VisitZodArray(ZodArrayNode node);
TResult VisitZodUnion(ZodUnionNode node);
TResult VisitZodIntersection(ZodIntersectionNode node);
TResult VisitZodEnum(ZodEnumNode node);
TResult VisitZodEnumValue(ZodEnumValueNode node);
TResult VisitZodProperty(ZodPropertyNode node);
TResult VisitZodEffects(ZodEffectsNode node);
```

### Этап 3.5: Создание TanStack Query специфичных AST узлов (Week 4, Days 3-4)

#### Шаг 3.5.1: Создание TanStack Query узлов

**Файлы для создания:**

1. **`ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/QueryKeyNode.cs`**

   - Класс: `QueryKeyNode : ExpressionNode`
   - Свойства: `Parts` (список `ExpressionNode`)
   - Методы: `Accept<TResult>`, `Clone()`

2. **`ValIO.Generators.TypeScript.TanStackQuery/Ast/Nodes/UseQueryNode.cs`**

   - Класс: `UseQueryNode : CallExpressionNode