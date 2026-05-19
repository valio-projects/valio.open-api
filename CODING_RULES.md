# Правила работы с кодом ValIO

## Общие принципы

## 1. Строгая типизация

### 1.1. Использование типов вместо строк
- **НЕ используй** строки для работы с типами, магические константы, проверки через `StartsWith`, `EndsWith`, `Contains`, `Equals` для определения типов
- **Используй** объектное представление типов через `TypeInfo`, `ModelTemplateData`, `OperationTemplateData`, `EnumTemplateData`
- **Работай** с мета-информацией объектов: `IsCollection`, `IsDictionary`, `IsNullable`, `IsPrimitive`, `GenericArguments`, `BaseType`

### 1.2. Формирование типов
- `FullTypeName` в `TypeInfo` должен содержать **только реальный тип** без оберток языка:
  - ✅ Правильно: `"string"`, `"int"`, `"User"`, `"List<User>"`, `"Dictionary<string, User>"`
  - ❌ Неправильно: `"Task<string>"`, `"Task<User>"`, `"Promise<User>"`, `"HttpResponseMessage"`
- Обертки языка (`Task<T>`, `Promise<T>`, `Response<T>`) формируются **только во время рендеринга** на основе объектного представления `TypeInfo`
- Nullable модификатор (`?`) добавляется **только в рендерере** на основе `IsNullable` и `IsNonNullableValueType()`

### 1.3. Примеры правильной работы с типами

```csharp
// ✅ ПРАВИЛЬНО: работа с объектным представлением
private string FormatTypeNameFromTypeInfo(TypeInfo typeInfo)
{
    if (typeInfo.IsCollection && typeInfo.GenericArguments.Count > 0)
    {
        var itemTypeName = FormatTypeNameFromTypeInfo(typeInfo.GenericArguments[0]);
        return $"List<{itemTypeName}>";
    }
    
    if (typeInfo.IsDictionary && typeInfo.GenericArguments.Count >= 2)
    {
        var keyTypeName = FormatTypeNameFromTypeInfo(typeInfo.GenericArguments[0]);
        var valueTypeName = FormatTypeNameFromTypeInfo(typeInfo.GenericArguments[1]);
        return $"Dictionary<{keyTypeName}, {valueTypeName}>";
    }
    
    var typeName = typeInfo.FullTypeName;
    if (typeInfo.IsNullable && !typeInfo.IsNonNullableValueType())
    {
        return $"{typeName}?";
    }
    
    return typeName;
}

// ❌ НЕПРАВИЛЬНО: работа со строками
private string FormatTypeName(string typeName)
{
    if (typeName.StartsWith("Task"))
        return typeName;
    if (typeName == "void")
        return "Task";
    return $"Task<{typeName}>";
}
```

## 2. Разделение кода по языкам

### 2.1. Структура проектов

```
ValIO.Core/                    # Общий код для всех языков
├── Interfaces/               # Интерфейсы (не зависят от языка)
├── Models/                   # Модели данных (не зависят от языка)
├── Services/                 # Сервисы общего назначения
│   ├── BaseCodeGenerator.cs # Базовый генератор
│   ├── BaseTypeMapper.cs     # Базовый маппер типов
│   ├── BaseTypeInfoBuilder.cs # Базовый построитель TypeInfo
│   ├── BaseEnumExtractor.cs  # Базовый извлекатель enum
│   ├── ModelExtractor.cs      # Извлечение моделей (общее)
│   ├── OperationExtractor.cs # Извлечение операций (общее)
│   └── SchemaResolver.cs     # Разрешение ссылок (общее)
└── Utilities/                # Утилиты общего назначения

ValIO.Generators.CSharp/      # C# специфичный код
├── CSharpCodeGenerator.cs    # Генератор для C#
├── CSharpTypeMapper.cs       # Маппер типов для C#
├── CSharpTypeInfoBuilder.cs  # Построитель TypeInfo для C#
├── CSharpCodeRenderer.cs     # Рендерер кода для C#
├── CSharpEnumExtractor.cs    # Извлекатель enum для C#
├── Renderers/                # Рендереры для C# (Class, Record, Enum)
└── Services/                 # C# специфичные сервисы

ValIO.Generators.TypeScript/  # TypeScript специфичный код
├── TypeScriptCodeGenerator.cs # Генератор для TypeScript
├── TypeScriptTypeMapper.cs   # Маппер типов для TypeScript
├── TypeScriptTypeInfoBuilder.cs # Построитель TypeInfo для TypeScript
├── TypeScriptCodeRenderer.cs # Рендерер кода для TypeScript
├── TypeScriptEnumExtractor.cs # Извлекатель enum для TypeScript
└── Services/                 # TypeScript специфичные сервисы
```

### 2.2. Правила разделения

#### ValIO.Core - общий код
**Что должно быть в ValIO.Core:**
- ✅ Парсинг OpenAPI схемы в объектное представление (`ModelTemplateData`, `OperationTemplateData`, `EnumTemplateData`)
- ✅ Извлечение данных из OpenAPI документа (модели, операции, enum'ы)
- ✅ Определение зависимостей между типами
- ✅ Разрешение ссылок на схемы (`SchemaResolver`)
- ✅ Базовые классы для генераторов, мапперов, построителей TypeInfo
- ✅ Общие модели данных (`TypeInfo`, `ModelTemplateData`, и т.д.)
- ✅ Общие интерфейсы (`ICodeGenerator`, `ITypeMapper`, `ITypeInfoBuilder`, и т.д.)
- ✅ Общие утилиты (форматирование, валидация, и т.д.)

**Что НЕ должно быть в ValIO.Core:**
- ❌ Языко-специфичный синтаксис (например, `Task<T>`, `Promise<T>`)
- ❌ Языко-специфичные типы (например, `List<T>` для C#, `Array<T>` для TypeScript)
- ❌ Языко-специфичные рендереры
- ❌ Языко-специфичные мапперы типов
- ❌ Языко-специфичные генераторы кода

#### ValIO.Generators.* - языко-специфичный код
**Что должно быть в языко-специфичных проектах:**
- ✅ Генераторы кода для конкретного языка (`CSharpCodeGenerator`, `TypeScriptCodeGenerator`)
- ✅ Мапперы типов для конкретного языка (`CSharpTypeMapper`, `TypeScriptTypeMapper`)
- ✅ Построители TypeInfo для конкретного языка (`CSharpTypeInfoBuilder`, `TypeScriptTypeInfoBuilder`)
- ✅ Рендереры кода для конкретного языка (`CSharpCodeRenderer`, `TypeScriptCodeRenderer`)
- ✅ Извлекатели enum для конкретного языка (`CSharpEnumExtractor`, `TypeScriptEnumExtractor`)
- ✅ Языко-специфичные рендереры (например, `ClassModelRenderer`, `RecordModelRenderer` для C#)
- ✅ Языко-специфичные стратегии именования
- ✅ Языко-специфичные анализаторы зависимостей

### 2.3. Примеры правильного разделения

```csharp
// ✅ ПРАВИЛЬНО: ValIO.Core - общий код
namespace ValIO.Core.Services;

public class ModelExtractor : IModelExtractor
{
    // Извлечение моделей не зависит от языка
    public ModelTemplateData ExtractModelTemplateData(
        IOpenApiSchema schema, 
        string modelName, 
        ITypeMapper typeMapper,  // Интерфейс, реализация в языко-специфичном проекте
        ITypeInfoBuilder typeInfoBuilder,  // Интерфейс, реализация в языко-специфичном проекте
        IDependencyAnalyzer dependencyAnalyzer)
    {
        // Общая логика извлечения
    }
}

// ✅ ПРАВИЛЬНО: ValIO.Generators.CSharp - C# специфичный код
namespace ValIO.Generators.CSharp;

public class CSharpCodeRenderer : ICodeRenderer
{
    private string FormatTypeNameFromTypeInfo(TypeInfo typeInfo)
    {
        // C# специфичная логика форматирования типов
        if (typeInfo.IsCollection)
        {
            return $"List<{FormatTypeNameFromTypeInfo(typeInfo.GenericArguments[0])}>";
        }
        // ...
    }
    
    private string FormatAsyncReturnTypeFromTypeInfo(TypeInfo returnTypeInfo)
    {
        // C# специфичная логика: оборачиваем в Task<T>
        if (returnTypeInfo.BaseType == "void")
            return "Task";
        return $"Task<{FormatTypeNameFromTypeInfo(returnTypeInfo)}>";
    }
}

// ❌ НЕПРАВИЛЬНО: C# специфичный код в ValIO.Core
namespace ValIO.Core.Services;

public class ModelExtractor
{
    public string FormatTypeName(TypeInfo typeInfo)
    {
        // ❌ C# специфичный код в общем проекте
        if (typeInfo.IsCollection)
        {
            return $"List<{...}>";  // Это C# синтаксис!
        }
    }
}
```

## 3. Работа с OpenAPI схемой

### 3.1. Парсинг и извлечение данных
- Используй `Microsoft.OpenApi` для парсинга OpenAPI документа
- Автоматически определяй версию схемы (3.0, 3.1, 3.2)
- Поддерживай рекурсивные схемы через `SchemaResolver`
- Извлекай данные в объектное представление (`ModelTemplateData`, `OperationTemplateData`, `EnumTemplateData`)

### 3.2. Поддерживаемые возможности OpenAPI
- ✅ AnyOf, AllOf, OneOf
- ✅ Dictionaries, HashMaps, Associative Arrays
- ✅ Data Types (string, number, integer, boolean, array, objects)
- ✅ Keywords (pattern, required, enum, minimum, maximum, exclusiveMinimum, exclusiveMaximum)
- ✅ Inheritance and Polymorphism
- ✅ Path Parameters, Query Parameters, Header Parameters
- ✅ Different responses for different HTTP Status Codes
- ✅ Request Body (JSON, form-data, multipart/form-data)
- ✅ Files (binary)
- ✅ Comments и примеры из схемы

### 3.3. Работа с локальными файлами и URL
- Поддерживай работу с локальными файлами OpenAPI schema (JSON, YAML)
- Поддерживай работу с OpenAPI schema по URL из интернета

## 4. Генерация кода

### 4.1. Использование StringBuilders вместо шаблонов
- **НЕ используй** RazorLight или другие шаблонные движки
- **Используй** StringBuilders и классы-рендереры для генерации кода
- Разделяй рендереры по типам: `ClassModelRenderer`, `RecordModelRenderer`, `CSharpEnumRenderer`, и т.д.

### 4.2. Структура рендереров
```csharp
// ✅ ПРАВИЛЬНО: разделение рендереров
public class ClassModelRenderer : IModelFormatRenderer
{
    public string Render(IModelTemplateData data) { }
}

public class RecordModelRenderer : IModelFormatRenderer
{
    public string Render(IModelTemplateData data) { }
}

public class CSharpEnumRenderer : IEnumRenderer
{
    public string Render(EnumTemplateData data) { }
}
```

### 4.3. Генерация комментариев
- Генерируй XML комментарии для классов, методов, свойств, enum'ов
- Используй описания из OpenAPI схемы
- Используй примеры из OpenAPI схемы

## 5. Паттерны проектирования

### 5.1. Используемые паттерны
- **Фабричный метод** (`CodeGeneratorFactory`, `TypeMapperFactory`)
- **Стратегия** (разные генераторы для разных языков)
- **Шаблонный метод** (`BaseCodeGenerator`, `BaseTypeMapper`, `BaseTypeInfoBuilder`)
- **Строитель** (`TypeInfoBuilder` для построения `TypeInfo`)
- **Посетитель** (для обхода схемы и извлечения данных)

### 5.2. Принципы SOLID
- **Single Responsibility**: каждый класс отвечает за одну задачу
- **Open/Closed**: открыт для расширения, закрыт для модификации
- **Liskov Substitution**: базовые классы могут быть заменены производными
- **Interface Segregation**: интерфейсы разделены по функциональности
- **Dependency Inversion**: зависимости через интерфейсы, не через конкретные классы

### 5.3. Принцип DRY
- Не дублируй код
- Выноси общую логику в базовые классы или утилиты
- Используй композицию вместо наследования где это уместно

## 6. Тестирование

### 6.1. Юнит тесты
- Покрывай юнит тестами каждый модуль
- Проверяй каждое правило отдельно
- Примеры: проверка генерации кода для схемы с string полем, int полем, min/max значениями

### 6.2. Интеграционные тесты
- Проверяй генерацию кода из реальных референсных OpenAPI schemas
- Проверяй полный цикл: парсинг → извлечение → генерация

### 6.3. Подход AAA
- **Arrange**: подготовка данных
- **Act**: выполнение действия
- **Assert**: проверка результата

## 7. Комментарии и документация

### 7.1. XML комментарии
- Пиши XML комментарии для всех публичных классов, методов, свойств
- Используй `<summary>`, `<param>`, `<returns>`, `<exception>`

### 7.2. Примеры комментариев
```csharp
/// <summary>
/// Извлекает модели из OpenAPI документа
/// </summary>
/// <param name="document">OpenAPI документ</param>
/// <param name="typeMapper">Маппер типов для текущего языка</param>
/// <returns>Список извлеченных моделей</returns>
public IReadOnlyList<ModelTemplateData> ExtractModelTemplateData(
    OpenApiDocument document, 
    ITypeMapper typeMapper)
{
    // ...
}
```

## 8. Обработка ошибок

### 8.1. Валидация входных данных
- Валидируй входные данные на раннем этапе
- Возвращай понятные сообщения об ошибках
- Логируй ошибки с контекстом

### 8.2. Обработка исключений
- Используй специфичные исключения
- Не глотай исключения без логирования
- Пробрасывай исключения с контекстом

## 9. Производительность

### 9.1. Кэширование
- Кэшируй разрешенные схемы (`SchemaResolver`)
- Кэшируй операции для быстрого поиска
- Кэшируй маппинг типов

### 9.2. Оптимизация
- Используй `StringBuilder` для построения больших строк
- Избегай множественных проходов по данным
- Используй `IReadOnlyList` для неизменяемых коллекций

## 10. Конфигурация

### 10.1. Гибкость настройки
- Поддерживай настройку генераторов через конфигурацию
- Используй строго типизированные опции (`CSharpGenerationOptions`, `TypeScriptGenerationOptions`)
- Валидируй конфигурацию перед использованием

### 10.2. Примеры опций
- `propertyAccessor`: Init, Set
- `useRequired`: использовать required модификатор
- `useSealed`: использовать sealed модификатор
- `modelType`: Class, Record, Struct
- `generateEqualityMembers`: генерировать Equals/GetHashCode
- `useNullable`: использовать nullable reference types
- `generateValidationAttributes`: генерировать атрибуты валидации

## 11. Зависимости и импорты

### 11.1. Определение зависимостей
- Анализируй зависимости между типами
- Определяй необходимые импорты/using директивы
- Группируй зависимости по категориям

### 11.2. Генерация импортов
- Генерируй только необходимые импорты
- Упорядочивай импорты (алфавитный порядок)
- Убирай дубликаты

## 12. Именование

### 12.1. Конвенции именования
- **Модели**: PascalCase
- **Свойства**: PascalCase
- **Методы**: PascalCase
- **Enum'ы**: PascalCase
- **Параметры**: camelCase (для TypeScript), PascalCase (для C#)
- **Переменные**: camelCase (для TypeScript), PascalCase (для C#)

### 12.2. Санитизация идентификаторов
- Убирай недопустимые символы
- Заменяй пробелы и спецсимволы
- Обрабатывай зарезервированные слова

## 13. Обратная совместимость

### 13.1. Отказ от обратной совместимости
- **НЕ используй** `Obsolete` атрибуты
- **НЕ поддерживай** старые форматы конфигурации
- **Удаляй** устаревший код без сохранения обратной совместимости

## 14. Технологии и библиотеки

### 14.1. Основные технологии
- **.NET 9.0** и современный синтаксис C#
- **Microsoft.OpenApi** - парсинг OpenAPI
- **System.CommandLine** - CLI интерфейс
- **Microsoft.Extensions.DependencyInjection** - DI
- **Microsoft.Extensions.Logging** - логирование
- **System.Text.Json** - работа с JSON
- **NUnit** - тестирование

### 14.2. Асинхронный код
- Используй асинхронный код где это возможно
- **НЕ используй** суффикс "Async" в названии методов (это не нужно)
- Используй `CancellationToken` для отмены операций

### 14.3. Коллекции
- Используй `HashSet`, `Dictionary` и другие узко специализированные коллекции при необходимости
- Используй `IReadOnlyList` для неизменяемых коллекций
- Используй `global usings` для часто используемых пространств имен

## 15. Структура кода

### 15.1. Разбиение на методы и классы
- Разбивай код на методы по функциональности
- Группируй методы и классы по функциональности
- Используй частичные классы (`partial`) для разделения больших файлов

### 15.2. Размер методов и классов
- Методы должны быть короткими и делать одну вещь
- Классы должны иметь четкую ответственность
- Избегай слишком больших файлов (> 1000 строк)

## 16. Примеры правильного кода

### 16.1. Работа с TypeInfo
```csharp
// ✅ ПРАВИЛЬНО
private string FormatTypeNameFromTypeInfo(TypeInfo typeInfo)
{
    if (typeInfo.IsCollection && typeInfo.GenericArguments.Count > 0)
    {
        var itemType = typeInfo.GenericArguments[0];
        var itemTypeName = FormatTypeNameFromTypeInfo(itemType);
        return $"List<{itemTypeName}>";
    }
    
    if (typeInfo.IsDictionary && typeInfo.GenericArguments.Count >= 2)
    {
        var keyType = typeInfo.GenericArguments[0];
        var valueType = typeInfo.GenericArguments[1];
        var keyTypeName = FormatTypeNameFromTypeInfo(keyType);
        var valueTypeName = FormatTypeNameFromTypeInfo(valueType);
        return $"Dictionary<{keyTypeName}, {valueTypeName}>";
    }
    
    var typeName = typeInfo.FullTypeName;
    if (typeInfo.IsNullable && !typeInfo.IsNonNullableValueType())
    {
        return $"{typeName}?";
    }
    
    return typeName;
}

// ❌ НЕПРАВИЛЬНО
private string FormatTypeName(string typeName)
{
    if (typeName.Contains("List<"))
        return typeName;
    if (typeName == "void")
        return "Task";
    return $"Task<{typeName}>";
}
```

### 16.2. Разделение по языкам
```csharp
// ✅ ПРАВИЛЬНО: ValIO.Core - общий код
public abstract class BaseTypeInfoBuilder : ITypeInfoBuilder
{
    public virtual TypeInfo BuildFromSchema(IOpenApiSchema schema, bool isNullable = false)
    {
        // Общая логика для всех языков
        return TypeMapper.MapPropertyTypeInfo(schema, !isNullable, SchemaResolver);
    }
}

// ✅ ПРАВИЛЬНО: ValIO.Generators.CSharp - C# специфичный код
public class CSharpTypeInfoBuilder : BaseTypeInfoBuilder
{
    protected override TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable)
    {
        return TypeInfo.Collection("List", itemType, isNullable, Language);
    }
}

// ❌ НЕПРАВИЛЬНО: C# специфичный код в ValIO.Core
public class BaseTypeInfoBuilder
{
    protected TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable)
    {
        return TypeInfo.Collection("List", itemType, isNullable, Language); // List - это C#!
    }
}
```

## 17. Чеклист перед коммитом

- [ ] Код использует строгую типизацию (нет работы со строками для типов)
- [ ] Языко-специфичный код находится в соответствующих проектах
- [ ] Общий код находится в ValIO.Core
- [ ] FullTypeName не содержит оберток языка (Task, Promise, и т.д.)
- [ ] Обертки языка формируются только в рендерере
- [ ] Код покрыт XML комментариями
- [ ] Код следует принципам SOLID и DRY
- [ ] Нет магических констант и строковых проверок для типов
- [ ] Используется объектное представление TypeInfo
- [ ] Код протестирован (юнит и интеграционные тесты)
## AST-first TypeScript generation

- TypeScript code generation is AST-first: steps, builders, and schema renderers create `SourceFileNode`, `ImportNode`, `VariableDeclarationNode`, `FunctionDeclarationNode`, `TypeAliasDeclarationNode`, `CallExpressionNode`, `ObjectLiteralExpressionNode`, and related nodes.
- `StringBuilder`, `CodeFormatter.AppendLine`, and raw string concatenation for TypeScript syntax are allowed only inside dedicated AST renderer implementations or temporary legacy code explicitly scheduled for removal.
- New TypeScript validators, transports, query wrappers, framework wrappers, methods, imports, exports, and variables must be expressed as AST nodes before rendering.
