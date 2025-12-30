---
name: TypeScript Code Generation Implementation Plan
overview: Детальный план реализации генерации TypeScript клиентов, моделей и enum'ов с поддержкой переключения между TypeScript interfaces/enums и схемами валидации (Zod/Yup/Valli), переключения между fetch и axios, и опциональной интеграции TanStack Query с автоматической сериализацией/валидацией payload.
todos: []
---

# План реализации генерации TypeScript клиентов и моделей

## Архитектура решения

### Структура проектов

Решение будет состоять из следующих проектов (часть уже существует):

1. **ValIO.Generators.TypeScript** - базовый генератор TypeScript (уже существует, требует доработки)
2. **ValIO.Generators.TypeScript.Zod** - генератор Zod схем
3. **ValIO.Generators.TypeScript.Axios** - генератор клиентов с Axios
4. **ValIO.Generators.TypeScript.TanStackQuery** - генератор хуков TanStack Query

### Стратегия реализации

Вместо создания множества отдельных генераторов для каждой комбинации, используем **композитный подход**:

- Базовый генератор `TypeScriptCodeGenerator` обрабатывает общую логику
- Специализированные генераторы расширяют базовый для конкретных библиотек
- Генераторы выбираются через Factory на основе опций конфигурации

## Этап 1: Расширение базовых моделей и интерфейсов

### 1.1. Создание TypeScriptGenerationOptions

**Файл**: `ValIO.Generators.TypeScript/Models/TypeScriptGenerationOptions.cs`

```csharp
public class TypeScriptGenerationOptions
{
    /// <summary>
    /// Формат моделей: Interfaces или ValidationSchemas
    /// </summary>
    public ModelFormat ModelFormat { get; set; } = ModelFormat.Interfaces;
    
    /// <summary>
    /// Библиотека валидации (если ModelFormat = ValidationSchemas)
    /// </summary>
    public ValidationLibrary? ValidationLibrary { get; set; }
    
    /// <summary>
    /// HTTP клиент: Fetch или Axios
    /// </summary>
    public HttpClientLibrary HttpClientLibrary { get; set; } = HttpClientLibrary.Fetch;
    
    /// <summary>
    /// Использовать TanStack Query
    /// </summary>
    public bool UseTanStackQuery { get; set; } = false;
    
    /// <summary>
    /// Сериализовать/валидировать payload через схемы (только если ValidationLibrary задан)
    /// </summary>
    public bool SerializeWithSchemas { get; set; } = true;
    
    /// <summary>
    /// Десериализовать/валидировать response через схемы (только если ValidationLibrary задан)
    /// </summary>
    public bool DeserializeWithSchemas { get; set; } = true;
    
    /// <summary>
    /// Генерировать enum'ы как TypeScript enum или union types
    /// </summary>
    public EnumFormat EnumFormat { get; set; } = EnumFormat.Enum;
    
    /// <summary>
    /// Использовать strict null checks
    /// </summary>
    public bool UseStrictNullChecks { get; set; } = true;
}

public enum ModelFormat
{
    Interfaces,      // TypeScript interfaces
    ValidationSchemas // Zod/Yup/Valli schemas
}

public enum EnumFormat
{
    Enum,           // enum MyEnum { ... }
    UnionType       // type MyEnum = "value1" | "value2"
}
```

### 1.2. Расширение TypeScriptTypeMapper для TypeInfo

**Файл**: `ValIO.Generators.TypeScript/TypeScriptTypeMapper.cs`

Добавить методы аналогично `CSharpTypeMapper`:

- `MapTypeInfo(IOpenApiSchema schema, ISchemaResolver schemaResolver)`
- `MapPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)`

Использовать `TypeInfo` из `ValIO.Generators.CSharp.Models` (общий класс) или создать аналогичный для TypeScript.

### 1.3. Обновление конфигурации

**Файл**: `ValIO.Configuration/Models/ValIOConfig.cs`

Добавить:

```csharp
public class ConfigOptions
{
    // ... существующие поля ...
    
    /// <summary>
    /// TypeScript специфичные опции
    /// </summary>
    public ConfigTypeScriptOptions? TypeScript { get; set; }
}

public class ConfigTypeScriptOptions
{
    public string? ModelFormat { get; set; } // "Interfaces" или "ValidationSchemas"
    public string? ValidationLibrary { get; set; } // "Zod", "Yup", "Valli"
    public string? HttpClientLibrary { get; set; } // "Fetch", "Axios"
    public bool? UseTanStackQuery { get; set; }
    public bool? SerializeWithSchemas { get; set; }
    public bool? DeserializeWithSchemas { get; set; }
    public string? EnumFormat { get; set; } // "Enum" или "UnionType"
    public bool? UseStrictNullChecks { get; set; }
}
```

**Файл**: `ValIO.Configuration/Services/ConfigurationLoader.cs`

Добавить метод `ParseTypeScriptOptions` аналогично `ParseCSharpOptions`.

## Этап 2: Рефакторинг базового TypeScript генератора

### 2.1. Переход на RazorLight шаблоны

**Текущее состояние**: Используются Scriban шаблоны (`.sbncs`)

**Требуется**: Перейти на RazorLight шаблоны (`.cshtml`) для консистентности с C# генератором

**Действия**:

1. Переименовать шаблоны из `.sbncs` в `.cshtml`
2. Обновить `TypeScriptCodeGenerator` для использования `ITemplateRegistry` с RazorLight
3. Обновить регистрацию в `Program.cs` для TypeScript шаблонов

### 2.2. Расширение TypeScriptCodeGenerator

**Файл**: `ValIO.Generators.TypeScript/TypeScriptCodeGenerator.cs`

Добавить:

- Поддержку `TypeInfo` для строгой типизации
- Методы генерации enum'ов
- Поддержку `TypeScriptGenerationOptions`
- Улучшенную обработку параметров операций (path, query, header)
- Обработку request body (JSON, form-data, binary)

### 2.3. Создание моделей для шаблонов

**Файлы**:

- `ValIO.Generators.TypeScript/Models/TypeScriptModelTemplateData.cs`
- `ValIO.Generators.TypeScript/Models/TypeScriptClientTemplateData.cs`
- `ValIO.Generators.TypeScript/Models/TypeScriptEnumTemplateData.cs`
- `ValIO.Generators.TypeScript/Models/TypeScriptOperationTemplateData.cs`

Аналогично моделям C# генератора, но адаптированные для TypeScript.

## Этап 3: Генератор Zod схем

### 3.1. Создание ZodSchemaGenerator

**Файл**: `ValIO.Generators.TypeScript.Zod/ZodSchemaGenerator.cs`

```csharp
public class ZodSchemaGenerator
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptTypeMapper _typeMapper;
    
    /// <summary>
    /// Генерирует Zod схему для модели
    /// </summary>
    public string GenerateSchema(ApiModel model, ISchemaResolver schemaResolver);
    
    /// <summary>
    /// Генерирует Zod схему для свойства
    /// </summary>
    public string GeneratePropertySchema(ApiProperty property, IOpenApiSchema schema, ISchemaResolver schemaResolver);
    
    /// <summary>
    /// Генерирует Zod схему для enum
    /// </summary>
    public string GenerateEnumSchema(ApiEnum apiEnum);
}
```

### 3.2. Создание ZodTypeMapper

**Файл**: `ValIO.Generators.TypeScript.Zod/ZodTypeMapper.cs`

Маппинг OpenAPI типов на Zod типы:

- `string` → `z.string()`
- `number` → `z.number()`
- `boolean` → `z.boolean()`
- `date` → `z.string().datetime()` или `z.date()`
- `array` → `z.array(...)`
- `object` → `z.object({ ... })`
- Поддержка валидации: `min`, `max`, `minLength`, `maxLength`, `pattern`, `enum`

### 3.3. Шаблоны RazorLight для Zod

**Файлы**:

- `ValIO.Generators.TypeScript.Zod/Templates/zod-schema.cshtml` - шаблон для Zod схемы модели
- `ValIO.Generators.TypeScript.Zod/Templates/zod-enum.cshtml` - шаблон для Zod enum схемы

## Этап 4: Генератор Axios клиентов

### 4.1. Создание AxiosClientGenerator

**Файл**: `ValIO.Generators.TypeScript.Axios/AxiosClientGenerator.cs`

```csharp
public class AxiosClientGenerator : TypeScriptCodeGenerator
{
    /// <summary>
    /// Генерирует Axios клиент для группы операций
    /// </summary>
    protected override string GenerateClientCode(
        List<ApiOperation> operations, 
        string clientName, 
        GenerationConfiguration config, 
        ISchemaResolver schemaResolver);
    
    /// <summary>
    /// Генерирует Axios метод для операции
    /// </summary>
    private string GenerateAxiosMethod(
        ApiOperation operation, 
        TypeScriptGenerationOptions options,
        ISchemaResolver schemaResolver);
}
```

### 4.2. Шаблоны RazorLight для Axios

**Файл**: `ValIO.Generators.TypeScript.Axios/Templates/axios-client.cshtml`

Генерирует:

- Импорт axios
- Класс/объект с методами для каждой операции
- Обработку параметров (path, query, headers)
- Обработку request body
- Типизированные ответы

## Этап 5: Генератор TanStack Query

### 5.1. Создание TanStackQueryGenerator

**Файл**: `ValIO.Generators.TypeScript.TanStackQuery/TanStackQueryGenerator.cs`

```csharp
public class TanStackQueryGenerator
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptTypeMapper _typeMapper;
    
    /// <summary>
    /// Генерирует React Query hook для query операции (GET)
    /// </summary>
    public string GenerateQueryHook(
        ApiOperation operation, 
        TypeScriptGenerationOptions options,
        ISchemaResolver schemaResolver);
    
    /// <summary>
    /// Генерирует React Query hook для mutation операции (POST, PUT, DELETE, PATCH)
    /// </summary>
    public string GenerateMutationHook(
        ApiOperation operation, 
        TypeScriptGenerationOptions options,
        ISchemaResolver schemaResolver);
    
    /// <summary>
    /// Генерирует имя хука на основе operationId
    /// </summary>
    public string GenerateHookName(string operationId, bool isMutation);
}
```

### 5.2. Интеграция с валидационными схемами

Если `ValidationLibrary` задан и `SerializeWithSchemas = true`:

**Для Request (Mutation)**:

```typescript
// Валидация и сериализация request body через Zod схему
const validatedData = CreateUserRequestSchema.parse(request);
const serialized = validatedData; // Zod уже преобразует типы (например, Date из string)
```

**Для Response (Query/Mutation)**:

```typescript
// Десериализация и валидация response через Zod схему
const response = await axios.get(...);
const validated = GetUserResponseSchema.parse(response.data);
// Zod преобразует типы (например, string в Date согласно схеме)
```

### 5.3. Шаблоны RazorLight для TanStack Query

**Файлы**:

- `ValIO.Generators.TypeScript.TanStackQuery/Templates/tanstack-query-hook.cshtml` - шаблон для query хука
- `ValIO.Generators.TypeScript.TanStackQuery/Templates/tanstack-mutation-hook.cshtml` - шаблон для mutation хука

## Этап 6: Композитная генерация

### 6.1. Обновление TypeScriptCodeGenerator

Генератор должен поддерживать комбинации опций:

1. **Interfaces + Fetch** - базовый вариант
2. **Interfaces + Axios** - использует AxiosClientGenerator
3. **Zod Schemas + Fetch** - генерирует Zod схемы + базовые fetch методы
4. **Zod Schemas + Axios** - генерирует Zod схемы + Axios методы
5. **Interfaces + TanStack Query + Fetch** - генерирует хуки с fetch
6. **Interfaces + TanStack Query + Axios** - генерирует хуки с axios
7. **Zod Schemas + TanStack Query + Fetch** - генерирует Zod схемы + хуки с валидацией
8. **Zod Schemas + TanStack Query + Axios** - генерирует Zod схемы + хуки с валидацией

### 6.2. Логика выбора генераторов

**Файл**: `ValIO.Core/Factories/CodeGeneratorFactory.cs`

Обновить для поддержки комбинаций TypeScript опций:

- Если `ValidationLibrary = Zod` → использовать ZodSchemaGenerator
- Если `HttpClientLibrary = Axios` → использовать AxiosClientGenerator
- Если `UseTanStackQuery = true` → использовать TanStackQueryGenerator

### 6.3. Структура генерируемых файлов

**Пример для Zod + Axios + TanStack Query**:

```
output/
  models/
    user.model.ts          // TypeScript interface
    user.schema.ts         // Zod schema
  api/
    users/
      users.api.ts         // Axios client methods
      users.queries.ts     // TanStack Query hooks
  enums/
    order-status.enum.ts   // TypeScript enum
    order-status.schema.ts // Zod enum schema
```

## Этап 7: Сериализация и валидация

### 7.1. Генерация helper функций

**Файл**: `ValIO.Generators.TypeScript/Templates/serialization-helpers.cshtml`

Генерирует вспомогательные функции для:

- Сериализации request body через Zod схему
- Десериализации response через Zod схему
- Преобразования типов (например, string → Date)

### 7.2. Интеграция в TanStack Query хуки

В шаблонах TanStack Query:

- **Query hooks**: Десериализация response через схему
- **Mutation hooks**: Сериализация request через схему + десериализация response

## Этап 8: Регистрация в DI

### 8.1. Обновление Program.cs

**Файл**: `ValIO/Program.cs`

Добавить регистрацию:

```csharp
// TypeScript Type Mapper
services.AddSingleton<ITypeMapper, TypeScriptTypeMapper>();

// TypeScript Generators
services.AddSingleton<ICodeGenerator, TypeScriptCodeGenerator>();
// services.AddSingleton<ICodeGenerator, AxiosClientGenerator>(); // Если нужен отдельный генератор
```

## Этап 9: Тестирование

### 9.1. Unit тесты

- Тесты для `ZodSchemaGenerator`
- Тесты для `AxiosClientGenerator`
- Тесты для `TanStackQueryGenerator`
- Тесты для `TypeScriptTypeMapper` с TypeInfo

### 9.2. Интеграционные тесты

- Генерация из реальных OpenAPI схем
- Проверка всех комбинаций опций
- Проверка сериализации/валидации

## Этап 10: Документация и примеры

### 10.1. Примеры конфигураций

Создать примеры `valio.config.json` для:

- Базовый TypeScript (interfaces + fetch)
- TypeScript + Zod + Axios
- TypeScript + Zod + TanStack Query + Axios

### 10.2. Документация

Документировать:

- Все доступные опции TypeScript генератора
- Примеры использования сгенерированного кода
- Интеграция с React приложениями

## Порядок реализации

1. **Этап 1**: Расширение базовых моделей (TypeScriptGenerationOptions, TypeInfo для TypeScript)
2. **Этап 2**: Рефакторинг базового TypeScript генератора (переход на RazorLight)
3. **Этап 3**: Генератор Zod схем
4. **Этап 4**: Генератор Axios клиентов
5. **Этап 5**: Генератор TanStack Query
6. **Этап 6**: Композитная генерация (объединение всех компонентов)
7. **Этап 7**: Сериализация и валидация
8. **Этап 8**: Регистрация в DI
9. **Этап 9**: Тестирование
10. **Этап 10**: Документация

## Ключевые файлы для изменения

### Новые файлы:

- `ValIO.Generators.TypeScript/Models/TypeScriptGenerationOptions.cs`
- `ValIO.Generators.TypeScript.Zod/ZodSchemaGenerator.cs`
- `ValIO.Generators.TypeScript.Zod/ZodTypeMapper.cs`
- `ValIO.Generators.TypeScript.Axios/AxiosClientGenerator.cs`
- `ValIO.Generators.TypeScript.TanStackQuery/TanStackQueryGenerator.cs`
- Шаблоны RazorLight для всех генераторов

### Изменяемые файлы:

- `ValIO.Generators.TypeScript/TypeScriptCodeGenerator.cs` - рефакторинг
- `ValIO.Generators.TypeScript/TypeScriptTypeMapper.cs` - добавление TypeInfo
- `ValIO.Configuration/Models/ValIOConfig.cs` - добавление TypeScript опций
- `ValIO.Configuration/Services/ConfigurationLoader.cs` - парсинг TypeScript опций
- `ValIO/Program.cs` - регистрация генераторов
- `ValIO.Core/Factories/CodeGeneratorFactory.cs` - выбор генераторов