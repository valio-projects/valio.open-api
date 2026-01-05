---
name: AST Refactoring Plan
overview: ""
todos: []
---

# План рефакторинга кодогенерации на AST-ориентированную архитектуру

## Часть 1: Глубокий анализ текущей реализации

### 1.1 Архитектура C# генерации

#### 1.1.1 Файловая структура и ответственность

**Основные файлы:**

1. **`ValIO.Generators.CSharp/CSharpCodeGenerator.cs`** (896 строк)

                                                                                                                                                                                                                                                                                                                                                                                                - Наследуется от `BaseCodeGenerator`
                                                                                                                                                                                                                                                                                                                                                                                                - Методы генерации:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `GenerateModels()` - генерация классов/records/structs
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `GenerateEnums()` - генерация enum'ов
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `GenerateClients()` - генерация HTTP клиентов
                                                                                                                                                                                                                                                                                                                                                                                                - Использует `CSharpCodeRenderer` для рендеринга
                                                                                                                                                                                                                                                                                                                                                                                                - Использует `CSharpTypeMapper` для маппинга типов
                                                                                                                                                                                                                                                                                                                                                                                                - Включает inline-генерацию исключений (`GenerateBaseExceptionCode`, `GenerateExceptionCode`)

2. **`ValIO.Generators.CSharp/CSharpCodeRenderer.cs`** (1161 строка)

                                                                                                                                                                                                                                                                                                                                                                                                - Реализует `ICodeRenderer`
                                                                                                                                                                                                                                                                                                                                                                                                - Методы рендеринга:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderModel(IModelTemplateData)` - рендерит модель через StringBuilder
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderEnum(EnumTemplateData)` - рендерит enum через StringBuilder
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderClient(IClientTemplateData)` - рендерит HTTP клиент
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderOperation(IOperationTemplateData)` - рендерит метод клиента
                                                                                                                                                                                                                                                                                                                                                                                                - Приватные методы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `GenerateProperty()` - генерация свойства с атрибутами валидации
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `GenerateValidationAttributes()` - генерация DataAnnotations атрибутов
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `GenerateValidationConstants()` - генерация констант для валидации
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `GenerateMethod()` - генерация метода HTTP клиента
                                                                                                                                                                                                                                                                                                                                                                                                - Проблемы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Прямая конкатенация строк через StringBuilder
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Смешение логики (проверки типов) и форматирования
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Хардкод синтаксиса C# в методах
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Невозможность трансформаций до рендеринга

3. **`ValIO.Generators.CSharp/CSharpTypeMapper.cs`** (297 строк)

                                                                                                                                                                                                                                                                                                                                                                                                - Наследуется от `BaseTypeMapper`
                                                                                                                                                                                                                                                                                                                                                                                                - Методы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `MapType()` - маппинг типа в строку
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `MapPropertyType()` - маппинг типа свойства
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `MapTypeInfo()` - маппинг в `TypeInfo` объект
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `MapTypeInfoInternal()` - внутренняя логика маппинга
                                                                                                                                                                                                                                                                                                                                                                                                - Обрабатывает:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Примитивные типы (int, string, bool, etc.)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Коллекции (List<T>, IList<T>)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Nullable типы
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Ссылки на схемы (references)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - AllOf, OneOf, AnyOf
                                                                                                                                                                                                                                                                                                                                                                                                - Возвращает строки (`FullTypeName`), не AST узлы

4. **`ValIO.Generators.CSharp/Renderers/ClassModelRenderer.cs`** (111 строк)

                                                                                                                                                                                                                                                                                                                                                                                                - Реализует `IModelFormatRenderer`
                                                                                                                                                                                                                                                                                                                                                                                                - Генерирует только классы (не records, не structs)
                                                                                                                                                                                                                                                                                                                                                                                                - Простая структура: using → namespace → XML comment → class → properties

5. **`ValIO.Generators.CSharp/Renderers/RecordModelRenderer.cs`** (111 строк)

                                                                                                                                                                                                                                                                                                                                                                                                - Аналогичен ClassModelRenderer, но для records
                                                                                                                                                                                                                                                                                                                                                                                                - Использует `{ get; init; }` вместо `{ get; set; }`

6. **`ValIO.Generators.CSharp/Renderers/CSharpEnumRenderer.cs`** (166 строк)

                                                                                                                                                                                                                                                                                                                                                                                                - Реализует `IEnumFormatRenderer`
                                                                                                                                                                                                                                                                                                                                                                                                - Генерирует C# enum с underlying type
                                                                                                                                                                                                                                                                                                                                                                                                - Обрабатывает числовые и строковые значения enum

#### 1.1.2 Текущий процесс генерации C#

```
OpenAPI Document
  ↓
SchemaResolver (разрешение ссылок)
  ↓
TypeMapper.MapTypeInfo() → TypeInfo (объект с мета-данными)
  ↓
ModelExtractor.ExtractModels() → ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions>
  ↓
CSharpCodeRenderer.RenderModel() → StringBuilder.AppendLine(...) → string
```

**Детальный процесс `RenderModel()`:**

1. Определение типа модели (class/record) из `data.Format` и `typedOptions`
2. Определение необходимости DataAnnotations (проверка `generateValidationAttributes`)
3. Определение необходимости Regex (проверка свойств на наличие Pattern)
4. Генерация using директив (если нужны DataAnnotations/Regex)
5. Генерация namespace
6. Генерация XML комментария через `CommentGenerator.GenerateXmlComment()`
7. Генерация объявления класса/record:

                                                                                                                                                                                                                                                                                                                                                                                                - sealed модификатор (если `isSealed`)
                                                                                                                                                                                                                                                                                                                                                                                                - базовый класс (если `data.BaseClassName` не пуст)

8. Генерация констант валидации (если `generateValidationConstants`)
9. Генерация свойств через `GenerateProperty()`:

                                                                                                                                                                                                                                                                                                                                                                                                - XML комментарий
                                                                                                                                                                                                                                                                                                                                                                                                - Атрибуты валидации (`[Required]`, `[MinLength]`, `[MaxLength]`, `[RegularExpression]`, etc.)
                                                                                                                                                                                                                                                                                                                                                                                                - Объявление свойства: `public Type? Name { get; set; }`
                                                                                                                                                                                                                                                                                                                                                                                                - Значение по умолчанию (если есть)

10. Генерация методов Equals/GetHashCode (если `generateEqualityMembers` и class)

**Проблемы текущей реализации:**

1. **Нет разделения структуры и синтаксиса**: Методы `GenerateProperty()`, `GenerateValidationAttributes()` знают и о структуре данных (`PropertyTemplateData`), и о синтаксисе C# (`StringBuilder.AppendLine("public ...")`)

2. **Сложность тестирования**: Невозможно проверить структуру кода без парсинга строк. Например, нельзя проверить что:

                                                                                                                                                                                                                                                                                                                                                                                                - Класс имеет правильное количество свойств
                                                                                                                                                                                                                                                                                                                                                                                                - Свойство имеет правильные атрибуты
                                                                                                                                                                                                                                                                                                                                                                                                - Методы правильно сформированы

3. **Дублирование кода**: `ClassModelRenderer` и `RecordModelRenderer` содержат идентичную логику, различается только синтаксис (`{ get; set; }` vs `{ get; init; }`)

4. **Слабая типизация**: Работа с типами через строки (`FullTypeName`). Невозможно проверить на этапе компиляции корректность типа.

5. **Нет возможности трансформаций**: Нельзя модифицировать структуру кода до рендеринга. Например, нельзя:

                                                                                                                                                                                                                                                                                                                                                                                                - Добавить дополнительные методы
                                                                                                                                                                                                                                                                                                                                                                                                - Изменить порядок свойств
                                                                                                                                                                                                                                                                                                                                                                                                - Модифицировать атрибуты

### 1.2 Архитектура TypeScript генерации

#### 1.2.1 Файловая структура и ответственность

**Основные файлы:**

1. **`ValIO.Generators.TypeScript/TypeScriptCodeGenerator.cs`** (279 строк)

                                                                                                                                                                                                                                                                                                                                                                                                - Наследуется от `BaseCodeGenerator`
                                                                                                                                                                                                                                                                                                                                                                                                - Поддерживает опциональные рендереры:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `IQueryWrapperRenderer?` (TanStack Query)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `IModelFormatRenderer?` (Zod схемы)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `IEnumFormatRenderer?` (Zod enum схемы)
                                                                                                                                                                                                                                                                                                                                                                                                - Методы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `GenerateModelsAndEnumsUnifiedAsync()` - унифицированная генерация моделей и enum'ов
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `GenerateClients()` - генерация HTTP клиентов
                                                                                                                                                                                                                                                                                                                                                                                                - Определяет какой рендерер использовать на основе `TypeScriptGenerationOptions`

2. **`ValIO.Generators.TypeScript/TypeScriptCodeRenderer.cs`** (953 строки)

                                                                                                                                                                                                                                                                                                                                                                                                - Реализует `ICodeRenderer`
                                                                                                                                                                                                                                                                                                                                                                                                - Методы рендеринга:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderModel()` - рендерит interface/type через StringBuilder
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderEnum()` - рендерит enum/union type
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderClient()` - рендерит HTTP клиент (Fetch или Axios)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderOperation()` - рендерит метод HTTP клиента
                                                                                                                                                                                                                                                                                                                                                                                                - Приватные методы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderFetchOperation()` - генерация fetch метода
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderAxiosOperation()` - генерация axios метода
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `GenerateProperty()` - генерация свойства интерфейса
                                                                                                                                                                                                                                                                                                                                                                                                - Особенности:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Поддержка Zod: если `useZod = true`, генерирует `export type X = z.infer<typeof XSchema>`
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Поддержка intersection types для allOf
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Поддержка union types для enum'ов
                                                                                                                                                                                                                                                                                                                                                                                                - Проблемы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Дублирование логики между `RenderFetchOperation` и `RenderAxiosOperation`
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Хардкод синтаксиса TypeScript
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Невозможность переиспользования частей кода между Fetch и Axios

3. **`ValIO.Generators.TypeScript/TypeScriptTypeMapper.cs`** (56+ строк)

                                                                                                                                                                                                                                                                                                                                                                                                - Наследуется от `BaseTypeMapper`
                                                                                                                                                                                                                                                                                                                                                                                                - Методы аналогичны CSharpTypeMapper, но для TypeScript синтаксиса
                                                                                                                                                                                                                                                                                                                                                                                                - Обрабатывает:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Optional properties (`Type?`)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Union types (`A | B`)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Array types (`Type[]`)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - TypeScript specific типы (`Record<K, V>`)

4. **`ValIO.Generators.TypeScript.TanStackQuery/TanStackQueryRenderer.cs`** (1551 строка)

                                                                                                                                                                                                                                                                                                                                                                                                - Реализует `IQueryWrapperRenderer`
                                                                                                                                                                                                                                                                                                                                                                                                - Методы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderQueryHook()` - генерация `useQuery` хука
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderMutationHook()` - генерация `useMutation` хука
                                                                                                                                                                                                                                                                                                                                                                                                - Приватные методы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderQueryKeyFunction()` - генерация функции для query key
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderQueryHookFunction()` - генерация самого хука
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderMutationHookFunction()` - генерация mutation хука
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderClientFunction()` - генерация функции клиента (если нужно)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderTypeExports()` - генерация type exports (`ResultType`, `AwaitedResultType`)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderQueryOptionsHelper()` - генерация helper для query options
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderInfiniteQuerySupport()` - генерация infinite query support
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderPrefetchSupport()` - генерация prefetch support
                                                                                                                                                                                                                                                                                                                                                                                                - Особенности:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Генерирует query keys на основе параметров операции
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Генерирует типизированные хуки с правильными generics
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Поддерживает infinite queries, prefetch, query options
                                                                                                                                                                                                                                                                                                                                                                                                - Проблемы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Огромный файл (1551 строка)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Вся логика через StringBuilder
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Сложно тестировать отдельные части
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Невозможно переиспользовать части между query и mutation

5. **`ValIO.Generators.TypeScript.Zod/ZodSchemaRenderer.cs`** (1010 строк)

                                                                                                                                                                                                                                                                                                                                                                                                - Реализует `IModelFormatRenderer`
                                                                                                                                                                                                                                                                                                                                                                                                - Методы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `Render()` - генерация Zod схемы
                                                                                                                                                                                                                                                                                                                                                                                                - Особенности:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Обрабатывает allOf через `z.intersection()` или объединение свойств
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Обрабатывает oneOf/anyOf через `z.union()`
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Генерирует схемы для всех типов (object, array, enum, primitive)
                                                                                                                                                                                                                                                                                                                                                                                                - Приватные методы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `RenderExpression()` - рендерит ZodSchemaExpression в строку
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `CreateZodProperty()` - создает ZodPropertyTemplateData
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - `FindSchemaInDocument()` - находит схему в OpenAPI документе
                                                                                                                                                                                                                                                                                                                                                                                                - Проблемы:
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Сложная логика обработки allOf/oneOf/anyOf
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Работа через строки (`ZodSchemaExpression`)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                - Невозможно проверить корректность схемы до рендеринга

#### 1.2.2 Текущий процесс генерации TypeScript

**Генерация модели:**

```
ModelTemplateData
  ↓
TypeScriptCodeRenderer.RenderModel()
  ↓
Проверка useZod:
 - Если true: Render Zod schema → type alias через z.infer
 - Если false: Render interface/type через StringBuilder
```

**Генерация клиента:**

```
OperationTemplateData
  ↓
TypeScriptCodeRenderer.RenderOperation()
  ↓
Проверка HttpClientLibrary:
 - Если Fetch: RenderFetchOperation() → fetch(...)
 - Если Axios: RenderAxiosOperation() → axios.request({...})
```

**Генерация TanStack Query:**

```
OperationTemplateData
  ↓
TanStackQueryRenderer.RenderQueryHook() / RenderMutationHook()
  ↓
Генерация:
 - QueryKey функция
 - Client функция (опционально)
 - Type exports
 - Hook функция
 - Helper функции (query options, infinite query, prefetch)
```

**Проблемы:**

1. **Дублирование логики**: `RenderFetchOperation` и `RenderAxiosOperation` содержат похожую логику (построение URL, обработка параметров, обработка body)

2. **Сложная композиция**: TanStack Query хуки зависят от axios/fetch методов, но генерируются отдельно. Невозможно переиспользовать AST узлы.

3. **Нет единой модели**: Интерфейсы, типы, enum'ы, Zod схемы, TanStack Query хуки генерируются через разные рендереры, нет общей структуры.

### 1.3 Domain Models (TemplateData)

#### 1.3.1 Структура TemplateData

**`ModelTemplateData<TOptions, TPropertyOptions>`:**

- `Name: string` - имя модели
- `Description: string?` - описание
- `Properties: IReadOnlyList<PropertyTemplateData<TPropertyOptions>>` - свойства
- `BaseClassName: string?` - базовый класс (для allOf)
- `BaseClassProperties: IReadOnlyList<...>` - свойства базового класса
- `OwnProperties: IReadOnlyList<...>` - собственные свойства
- `Namespace: string?` - namespace (для C#)
- `ImportPath: string?` - путь для импорта (для TypeScript)
- `Format: ModelFormat` - формат (Class, Record, Interface, etc.)
- `CompositionInfo: SchemaCompositionInfo?` - информация о композиции (allOf, oneOf, anyOf)
- `OriginalSchema: IOpenApiSchema?` - оригинальная схема (для Zod)

**`PropertyTemplateData<TOptions>`:**

- `Name: string` - имя свойства
- `TypeInfo: TypeInfo` - информация о типе (объект с мета-данными)
- `Description: string?` - описание
- `IsRequired: bool` - обязательное
- `IsNullable: bool` - nullable
- `IsOptional: bool` - optional (для TypeScript)
- `DefaultValue: object?` - значение по умолчанию
- `Pattern: string?` - regex pattern (для валидации)
- `MinLength: int?`, `MaxLength: int?` - ограничения длины
- `Minimum: double?`, `Maximum: double?` - числовые ограничения

**`OperationTemplateData<TOptions>`:**

- `MethodName: string` - имя метода
- `HttpMethod: string` - HTTP метод (GET, POST, etc.)
- `Path: string` - путь операции
- `ReturnTypeInfo: TypeInfo` - тип возвращаемого значения
- `RequestBodyTypeInfo: TypeInfo?` - тип request body
- `PathParameters: IReadOnlyList<ParameterTemplateData>` - path параметры
- `QueryParameters: IReadOnlyList<ParameterTemplateData>` - query параметры
- `HeaderParameters: IReadOnlyList<ParameterTemplateData>` - header параметры
- `FormDataProperties: IReadOnlyList<FormDataPropertyTemplateData>?` - form-data свойства
- `Responses: IReadOnlyList<ResponseTemplateData>` - ответы операции

**`TypeInfo`:**

- `BaseType: string` - базовое имя типа
- `IsCollection: bool` - коллекция
- `IsDictionary: bool` - словарь
- `IsNullable: bool` - nullable
- `IsOptional: bool` - optional
- `IsPrimitive: bool` - примитив
- `GenericArguments: IReadOnlyList<TypeInfo>` - generic аргументы
- `FullTypeName: string` - полное имя типа (строка)
- `Language: GenerationLanguage?` - язык
- `Dependencies: IReadOnlyList<string>` - зависимости

#### 1.3.2 Проблемы TemplateData

1. **TemplateData не является AST**: Это данные для шаблона, но не структура кода. Нет иерархии узлов, нет композиции.

2. **Смешение уровней абстракции**: TemplateData содержит и domain информацию (из OpenAPI), и информацию о форматировании (Namespace, ImportPath).

3. **Слабая типизация**: `FullTypeName` - это строка. Невозможно проверить корректность типа на этапе компиляции.

4. **Нет возможности трансформаций**: TemplateData - это конечные данные, нельзя модифицировать структуру перед рендерингом.

## Часть 2: Целевая AST-ориентированная архитектура

### 2.1 Принципы архитектуры

1. **AST First**: Код представляется как дерево узлов. Рендеринг - это отдельный шаг, который обходит дерево и генерирует строку.

2. **Immutable AST**: Все узлы неизменяемые (immutable). Для модификации создаются новые узлы.

3. **Language-agnostic IR**: Промежуточное представление (IR) не зависит от языка. Общие узлы (TypeNode, ExpressionNode) не содержат синтаксиса конкретного языка.

4. **Separation of Concerns**: 

                                                                                                                                                                                                                                                                                                                                                                                                - Структура кода (AST узлы)
                                                                                                                                                                                                                                                                                                                                                                                                - Синтаксис языка (Renderer)
                                                                                                                                                                                                                                                                                                                                                                                                - Форматирование (Formatter)

5. **Extensibility**: Новые языки и стеки добавляются через новые узлы/рендереры, без изменения ядра.

### 2.2 Pipeline рефакторинга

```
OpenAPI Document
  ↓
TypeMapper → TypeInfo (domain model)
  ↓
ModelExtractor → ModelTemplateData (domain model)
  ↓
AST Builder → AST Nodes (структура кода)
  ↓
AST Transformer (optional) → AST Nodes (модифицированная структура)
  ↓
AST Renderer → String (код)
  ↓
Generated Code
```

### 2.3 Иерархия AST узлов

```
AstNode (abstract, immutable)
  ├── DeclarationNode (abstract)
  │   ├── TypeDeclarationNode (abstract)
  │   │   ├── ClassDeclarationNode (C#)
  │   │   ├── RecordDeclarationNode (C#)
  │   │   ├── InterfaceDeclarationNode (TypeScript)
  │   │   ├── EnumDeclarationNode (common)
  │   │   └── TypeAliasDeclarationNode (TypeScript)
  │   ├── MethodDeclarationNode (common)
  │   ├── PropertyDeclarationNode (common)
  │   └── ParameterDeclarationNode (common)
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

## Часть 3: Детальный список AST узлов

### 3.1 Общие (Language-agnostic) узлы

#### 3.1.1 Базовые узлы

**`AstNode` (abstract)**

- Файл: `ValIO.Core/Ast/Nodes/AstNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `AstNodeKind Kind { get; }` - тип узла (enum)
                                                                                                                                                                                                                                                                - `IReadOnlyList<AstNode> Children { get; }` - дочерние узлы (вычисляемое свойство)
                                                                                                                                                                                                                                                                - `IDictionary<string, object> Metadata { get; }` - метаданные (для расширений)
- Методы:
                                                                                                                                                                                                                                                                - `abstract TResult Accept<TResult>(IAstVisitor<TResult> visitor)` - Visitor pattern
                                                                                                                                                                                                                                                                - `abstract AstNode Clone()` - клонирование узла
- Реализация `Children`: Должно быть вычисляемое свойство, которое собирает дочерние узлы из всех свойств узла

**`DeclarationNode` (abstract)**

- Файл: `ValIO.Core/Ast/Nodes/DeclarationNode.cs`
- Наследуется от: `AstNode`
- Свойства:
                                                                                                                                                                                                                                                                - `IdentifierNode Name { get; }` - имя объявления
                                                                                                                                                                                                                                                                - `CommentNode? Documentation { get; }` - документация (XML/JSDoc)
                                                                                                                                                                                                                                                                - `IReadOnlyList<AttributeNode> Attributes { get; }` - атрибуты/декораторы
- Наследники: `TypeDeclarationNode`, `MethodDeclarationNode`, `PropertyDeclarationNode`, `ParameterDeclarationNode`

**`TypeDeclarationNode` (abstract)**

- Файл: `ValIO.Core/Ast/Nodes/TypeDeclarationNode.cs`
- Наследуется от: `DeclarationNode`
- Свойства:
                                                                                                                                                                                                                                                                - `AccessModifier AccessModifier { get; }` - модификатор доступа (public, private, etc.)
                                                                                                                                                                                                                                                                - `IReadOnlyList<TypeNode> BaseTypes { get; }` - базовые типы/интерфейсы (для наследования)
                                                                                                                                                                                                                                                                - `IReadOnlyList<MemberDeclarationNode> Members { get; }` - члены типа (properties, methods, etc.)
- Наследники: `ClassDeclarationNode`, `RecordDeclarationNode`, `InterfaceDeclarationNode`, `EnumDeclarationNode`

**`StatementNode` (abstract)**

- Файл: `ValIO.Core/Ast/Nodes/StatementNode.cs`
- Наследуется от: `AstNode`
- Свойства:
                                                                                                                                                                                                                                                                - `CommentNode? Documentation { get; }` - документация
- Наследники: `ReturnStatementNode`, `IfStatementNode`, `ForEachStatementNode`, `ExpressionStatementNode`

**`ExpressionNode` (abstract)**

- Файл: `ValIO.Core/Ast/Nodes/ExpressionNode.cs`
- Наследуется от: `AstNode`
- Свойства:
                                                                                                                                                                                                                                                                - `TypeNode? Type { get; }` - тип выражения (опционально, для type inference)
- Наследники: `LiteralExpressionNode`, `IdentifierExpressionNode`, `CallExpressionNode`, `MemberAccessExpressionNode`, `BinaryExpressionNode`

**`TypeNode` (abstract)**

- Файл: `ValIO.Core/Ast/Nodes/TypeNode.cs`
- Наследуется от: `AstNode`
- Свойства:
                                                                                                                                                                                                                                                                - `bool IsNullable { get; }` - nullable (для C#: `Type?`)
                                                                                                                                                                                                                                                                - `bool IsOptional { get; }` - optional (для TypeScript: `Type?`)
- Наследники: `SimpleTypeNode`, `GenericTypeNode`, `NullableTypeNode`, `ArrayTypeNode`, `UnionTypeNode`, `IntersectionTypeNode`, `DictionaryTypeNode`

#### 3.1.2 Узлы типов

**`SimpleTypeNode : TypeNode`**

- Файл: `ValIO.Core/Ast/Nodes/Types/SimpleTypeNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `IdentifierNode Name { get; }` - имя типа (например, "string", "int", "User")
                                                                                                                                                                                                                                                                - `bool IsPrimitive { get; }` - примитивный тип
- Использование: Представляет простые типы без generic аргументов

**`GenericTypeNode : TypeNode`**

- Файл: `ValIO.Core/Ast/Nodes/Types/GenericTypeNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `IdentifierNode Name { get; }` - имя generic типа (например, "List", "Dictionary")
                                                                                                                                                                                                                                                                - `IReadOnlyList<TypeNode> TypeArguments { get; }` - аргументы типа
- Использование: Представляет generic типы (List<T>, Dictionary<K, V>)

**`NullableTypeNode : TypeNode`**

- Файл: `ValIO.Core/Ast/Nodes/Types/NullableTypeNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `TypeNode InnerType { get; }` - внутренний тип
- Использование: Обертка для nullable типов (C#: `Type?`)

**`OptionalTypeNode : TypeNode`**

- Файл: `ValIO.Core/Ast/Nodes/Types/OptionalTypeNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `TypeNode InnerType { get; }` - внутренний тип
- Использование: Обертка для optional типов (TypeScript: `Type?`)

**`ArrayTypeNode : TypeNode`**

- Файл: `ValIO.Core/Ast/Nodes/Types/ArrayTypeNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `TypeNode ElementType { get; }` - тип элементов массива
                                                                                                                                                                                                                                                                - `bool IsReadOnly { get; }` - readonly массив (для TypeScript: `readonly Type[]`)
- Использование: Представляет массивы (C#: `Type[]`, TypeScript: `Type[]`)

**`UnionTypeNode : TypeNode`**

- Файл: `ValIO.Core/Ast/Nodes/Types/UnionTypeNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `IReadOnlyList<TypeNode> Types { get; }` - типы в union
- Использование: Представляет union types (TypeScript: `A | B | C`)

**`IntersectionTypeNode : TypeNode`**

- Файл: `ValIO.Core/Ast/Nodes/Types/IntersectionTypeNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `IReadOnlyList<TypeNode> Types { get; }` - типы в intersection
- Использование: Представляет intersection types (TypeScript: `A & B`)

**`DictionaryTypeNode : TypeNode`**

- Файл: `ValIO.Core/Ast/Nodes/Types/DictionaryTypeNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `TypeNode KeyType { get; }` - тип ключа
                                                                                                                                                                                                                                                                - `TypeNode ValueType { get; }` - тип значения
- Использование: Представляет словари (C#: `Dictionary<K, V>`, TypeScript: `Record<K, V>`)

#### 3.1.3 Узлы выражений

**`LiteralExpressionNode : ExpressionNode`**

- Файл: `ValIO.Core/Ast/Nodes/Expressions/LiteralExpressionNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `object Value { get; }` - значение литерала
                                                                                                                                                                                                                                                                - `LiteralKind Kind { get; }` - тип литерала (enum: String, Number, Boolean, Null)
- Использование: Представляет литералы (`"string"`, `123`, `true`, `null`)

**`IdentifierExpressionNode : ExpressionNode`**

- Файл: `ValIO.Core/Ast/Nodes/Expressions/IdentifierExpressionNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `IdentifierNode Identifier { get; }` - идентификатор
- Использование: Представляет использование идентификатора (переменная, функция, тип)

**`CallExpressionNode : ExpressionNode`**

- Файл: `ValIO.Core/Ast/Nodes/Expressions/CallExpressionNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `ExpressionNode Target { get; }` - вызываемый объект/функция
                                                                                                                                                                                                                                                                - `IReadOnlyList<ExpressionNode> Arguments { get; }` - аргументы вызова
- Использование: Представляет вызов функции/метода (`func(arg1, arg2)`)

**`MemberAccessExpressionNode : ExpressionNode`**

- Файл: `ValIO.Core/Ast/Nodes/Expressions/MemberAccessExpressionNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `ExpressionNode Object { get; }` - объект
                                                                                                                                                                                                                                                                - `IdentifierNode Member { get; }` - член объекта
- Использование: Представляет доступ к члену (`obj.member`)

**`BinaryExpressionNode : ExpressionNode`**

- Файл: `ValIO.Core/Ast/Nodes/Expressions/BinaryExpressionNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `ExpressionNode Left { get; }` - левый операнд
                                                                                                                                                                                                                                                                - `BinaryOperator Operator { get; }` - оператор (enum: Add, Subtract, Multiply, Divide, Equals, NotEquals, And, Or, etc.)
                                                                                                                                                                                                                                                                - `ExpressionNode Right { get; }` - правый операнд
- Использование: Представляет бинарные операции (`a + b`, `x == y`)

**`ConditionalExpressionNode : ExpressionNode`**

- Файл: `ValIO.Core/Ast/Nodes/Expressions/ConditionalExpressionNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `ExpressionNode Condition { get; }` - условие
                                                                                                                                                                                                                                                                - `ExpressionNode TrueExpression { get; }` - выражение для true
                                                                                                                                                                                                                                                                - `ExpressionNode FalseExpression { get; }` - выражение для false
- Использование: Представляет тернарный оператор (`condition ? trueExpr : falseExpr`)

#### 3.1.4 Узлы объявлений

**`PropertyDeclarationNode : DeclarationNode`**

- Файл: `ValIO.Core/Ast/Nodes/PropertyDeclarationNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `TypeNode Type { get; }` - тип свойства
                                                                                                                                                                                                                                                                - `ExpressionNode? DefaultValue { get; }` - значение по умолчанию
                                                                                                                                                                                                                                                                - `bool IsRequired { get; }` - required (для TypeScript)
                                                                                                                                                                                                                                                                - `PropertyAccessorKind Accessor { get; }` - тип доступа (enum: Get, Set, GetSet, GetInit)
- Использование: Представляет свойство класса/интерфейса

**`MethodDeclarationNode : DeclarationNode`**

- Файл: `ValIO.Core/Ast/Nodes/MethodDeclarationNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `TypeNode? ReturnType { get; }` - возвращаемый тип (null для void)
                                                                                                                                                                                                                                                                - `IReadOnlyList<ParameterDeclarationNode> Parameters { get; }` - параметры метода
                                                                                                                                                                                                                                                                - `BlockNode? Body { get; }` - тело метода (null для абстрактных методов)
                                                                                                                                                                                                                                                                - `bool IsAsync { get; }` - async метод
                                                                                                                                                                                                                                                                - `AccessModifier AccessModifier { get; }` - модификатор доступа
- Использование: Представляет метод/функцию

**`ParameterDeclarationNode : DeclarationNode`**

- Файл: `ValIO.Core/Ast/Nodes/ParameterDeclarationNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `TypeNode Type { get; }` - тип параметра
                                                                                                                                                                                                                                                                - `ExpressionNode? DefaultValue { get; }` - значение по умолчанию
                                                                                                                                                                                                                                                                - `bool IsRequired { get; }` - required
                                                                                                                                                                                                                                                                - `ParameterKind Kind { get; }` - тип параметра (enum: Value, Ref, Out, In)
- Использование: Представляет параметр метода/функции

**`EnumMemberDeclarationNode : DeclarationNode`**

- Файл: `ValIO.Core/Ast/Nodes/EnumMemberDeclarationNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `ExpressionNode? Value { get; }` - значение enum (null для auto-increment)
                                                                                                                                                                                                                                                                - `CommentNode? Documentation { get; }` - документация
- Использование: Представляет значение enum'а

#### 3.1.5 Узлы операторов (Statements)

**`ReturnStatementNode : StatementNode`**

- Файл: `ValIO.Core/Ast/Nodes/Statements/ReturnStatementNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `ExpressionNode? Value { get; }` - возвращаемое значение (null для void return)
- Использование: Представляет return statement

**`IfStatementNode : StatementNode`**

- Файл: `ValIO.Core/Ast/Nodes/Statements/IfStatementNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `ExpressionNode Condition { get; }` - условие
                                                                                                                                                                                                                                                                - `BlockNode ThenBlock { get; }` - then блок
                                                                                                                                                                                                                                                                - `BlockNode? ElseBlock { get; }` - else блок (null если нет else)
- Использование: Представляет if statement

**`ForEachStatementNode : StatementNode`**

- Файл: `ValIO.Core/Ast/Nodes/Statements/ForEachStatementNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `IdentifierNode Variable { get; }` - переменная итератора
                                                                                                                                                                                                                                                                - `ExpressionNode Collection { get; }` - коллекция для итерации
                                                                                                                                                                                                                                                                - `BlockNode Body { get; }` - тело цикла
- Использование: Представляет foreach/for...of statement

**`ExpressionStatementNode : StatementNode`**

- Файл: `ValIO.Core/Ast/Nodes/Statements/ExpressionStatementNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `ExpressionNode Expression { get; }` - выражение
- Использование: Представляет выражение как statement (например, вызов функции)

#### 3.1.6 Вспомогательные узлы

**`IdentifierNode : AstNode`**

- Файл: `ValIO.Core/Ast/Nodes/IdentifierNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `string Name { get; }` - имя идентификатора
- Использование: Представляет идентификатор (имя переменной, функции, типа)

**`CommentNode : AstNode`**

- Файл: `ValIO.Core/Ast/Nodes/CommentNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `string Text { get; }` - текст комментария
                                                                                                                                                                                                                                                                - `CommentKind Kind { get; }` - тип комментария (enum: Xml, JSDoc, SingleLine, MultiLine)
- Использование: Представляет комментарий

**`AttributeNode : AstNode`**

- Файл: `ValIO.Core/Ast/Nodes/AttributeNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `IdentifierNode Name { get; }` - имя атрибута
                                                                                                                                                                                                                                                                - `IReadOnlyList<ExpressionNode> Arguments { get; }` - аргументы атрибута
- Использование: Представляет атрибут/декоратор (C#: `[Attribute]`, TypeScript: `@Decorator`)

**`BlockNode : AstNode`**

- Файл: `ValIO.Core/Ast/Nodes/BlockNode.cs`
- Свойства:
                                                                                                                                                                                                                                                                - `IReadOnlyList<StatementNode> Statements { get; }` - statements в бл