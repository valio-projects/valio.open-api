---
name: ""
overview: Детальный план глобального рефакторинга приложения ValIO с разделением на слои (парсинг, обработка данных, рендеринг), унификацией кода между C# и TypeScript генераторами, удалением Razor шаблонов в пользу string builders, и созданием общих абстракций и моделей.
todos:
  - id: move-typeinfo-to-core
    content: Переместить TypeInfo из ValIO.Generators.CSharp.Models в ValIO.Core.Models, удалить C# специфичные свойства (IsTask, IsHttpResponseMessage), добавить универсальные свойства (Language, Dependencies, IsOptional, Format)
    status: completed
  - id: create-universal-template-data
    content: Создать универсальные TemplateData модели в ValIO.Core/Models/TemplateData/ (ModelTemplateData, EnumTemplateData, OperationTemplateData, ClientTemplateData) с уже подготовленными данными и зависимостями
    status: completed
    dependencies:
      - move-typeinfo-to-core
  - id: create-renderer-interfaces
    content: Создать интерфейсы для рендеринга в ValIO.Core/Interfaces/ (ICodeRenderer, IModelRenderer, IEnumRenderer, IClientRenderer, IOperationRenderer)
    status: completed
    dependencies:
      - create-universal-template-data
  - id: create-base-type-mapper
    content: Создать BaseTypeMapper в ValIO.Core/Services/ с общей логикой маппинга типов, рефакторить CSharpTypeMapper и TypeScriptTypeMapper для наследования от базового класса
    status: completed
    dependencies:
      - move-typeinfo-to-core
  - id: create-dependency-analyzer
    content: Создать DependencyAnalyzer в ValIO.Core/Services/ для анализа зависимостей между типами, с методами AnalyzeModelDependencies, AnalyzeOperationDependencies, CollectAllDependencies, ResolveImportPaths
    status: completed
    dependencies:
      - move-typeinfo-to-core
  - id: create-base-code-generator
    content: Создать BaseCodeGenerator в ValIO.Core/Services/ с общей логикой для всех генераторов (ExtractData, ProcessData, GenerateCode, ValidateInputs, GroupOperations), рефакторить CSharpCodeGenerator и TypeScriptCodeGenerator для наследования
    status: completed
    dependencies:
      - create-base-type-mapper
      - create-dependency-analyzer
  - id: create-csharp-renderer
    content: Создать CSharpCodeRenderer в ValIO.Generators.CSharp/ с методами RenderModel, RenderEnum, RenderClient, RenderOperation, RenderUsings используя StringBuilders вместо Razor шаблонов
    status: completed
    dependencies:
      - create-renderer-interfaces
      - create-universal-template-data
  - id: create-typescript-renderer
    content: Создать TypeScriptCodeRenderer в ValIO.Generators.TypeScript/ с методами RenderModel, RenderEnum, RenderClient, RenderOperation, RenderImports используя StringBuilders
    status: completed
    dependencies:
      - create-renderer-interfaces
      - create-universal-template-data
  - id: create-zod-renderer
    content: Переименовать ZodSchemaGenerator в ZodSchemaRenderer, реализовать IModelFormatRenderer, использовать StringBuilders, убрать логику маппинга в ZodTypeMapper
    status: completed
    dependencies:
      - create-renderer-interfaces
      - create-universal-template-data
  - id: refactor-extractors
    content: Рефакторить ModelExtractor, OperationExtractor, EnumExtractor для возврата TemplateData вместо ApiModel/ApiOperation/ApiEnum, собирать зависимости сразу при извлечении
    status: completed
    dependencies:
      - create-universal-template-data
      - create-dependency-analyzer
  - id: create-typeinfo-builder
    content: Создать TypeInfoBuilder в ValIO.Core/Services/ для построения TypeInfo из OpenAPI схем с методами BuildFromSchema, BuildCollection, BuildDictionary, BuildNullable
    status: completed
    dependencies:
      - move-typeinfo-to-core
  - id: create-dependency-collector
    content: Создать DependencyCollector в ValIO.Core/Services/ для сбора зависимостей из TypeInfo с методами CollectFromTypeInfo, CollectFromModel, CollectFromOperation, ResolveImportPaths
    status: completed
    dependencies:
      - move-typeinfo-to-core
      - create-dependency-analyzer
  - id: create-import-path-resolver
    content: Создать ImportPathResolver в ValIO.Core/Services/ для определения путей импорта/usings с методами ResolveModelImport, ResolveEnumImport, ResolveSchemaImport, ResolveClientImport
    status: completed
    dependencies:
      - create-dependency-collector
  - id: refactor-tanstack-query
    content: "Переименовать TanStackQueryGenerator в TanStackQueryRenderer, реализовать IQueryWrapperRenderer, использовать StringBuilders, получать готовые данные вместо обработки OpenAPI. Примечание: Renderer создан, но методы рендеринга еще не полностью реализованы (есть TODO)"
    status: completed
    dependencies:
      - create-renderer-interfaces
      - create-universal-template-data
  - id: create-model-format-renderer
    content: Создать IModelFormatRenderer и реализации (InterfaceModelRenderer, ZodSchemaModelRenderer, ClassModelRenderer, RecordModelRenderer) для унификации форматов моделей
    status: completed
    dependencies:
      - create-renderer-interfaces
  - id: create-enum-format-renderer
    content: Создать IEnumFormatRenderer и реализации (TypeScriptEnumRenderer, TypeScriptUnionTypeRenderer, ZodEnumSchemaRenderer, CSharpEnumRenderer) для унификации форматов enum
    status: completed
    dependencies:
      - create-renderer-interfaces
  - id: remove-razor-templates
    content: Удалить все .cshtml файлы из Templates/, удалить зависимость от RazorLight, удалить ITemplateRegistry, TemplateRegistry, TemplateRegistryFactory, TemplateCache
    status: completed
    dependencies:
      - create-csharp-renderer
      - create-typescript-renderer
      - create-zod-renderer
      - refactor-tanstack-query
  - id: update-generators-to-use-renderers
    content: Обновить CSharpCodeGenerator и TypeScriptCodeGenerator для использования новых renderers вместо Razor шаблонов, интегрировать DependencyCollector и ImportPathResolver
    status: completed
    dependencies:
      - create-csharp-renderer
      - create-typescript-renderer
      - refactor-extractors
      - create-dependency-collector
      - create-import-path-resolver
  - id: unify-model-enum-generation
    content: Унифицировать генерацию моделей и enum через один processor и несколько renderers на основе конфигурации, удалить дублирование между typescript-model/zod-schema и typescript-enum/zod-enum
    status: completed
    dependencies:
      - create-model-format-renderer
      - create-enum-format-renderer
      - update-generators-to-use-renderers
  - id: cleanup-unused-code
    content: Удалить неиспользуемый код, мусорные свойства из TypeInfo, оптимизировать производительность через кэширование TypeInfo и зависимостей
    status: completed
    dependencies:
      - remove-razor-templates
      - unify-model-enum-generation
---

# План глобального рефакторинга ValIO

## Анализ текущего состояния

### Выявленные проблемы

1. **Дублирование кода:**

   - `CSharpCodeGenerator` и `TypeScriptCodeGenerator` имеют ~80% дублирующегося кода
   - `CSharpTypeMapper` и `TypeScriptTypeMapper` дублируют логику маппинга
   - Повторяющаяся логика извлечения данных, валидации, группировки операций

2. **Нарушение разделения ответственности:**

   - Генераторы содержат логику парсинга, обработки данных и рендеринга
   - Razor шаблоны содержат бизнес-логику (FormatType, FormatParameter, BuildPath)
   - Определение импортов/usings происходит в шаблонах

3. **Проблемы с TypeInfo:**

   - Находится в `ValIO.Generators.CSharp.Models`, но используется в TypeScript
   - Содержит мусорные свойства (`IsTask`, `IsHttpResponseMessage`) специфичные для C#
   - Не универсален для разных языков

4. **Работа со строками вместо объектов:**

   - Много логики работает с готовыми строками типов
   - Нет мета-информации о зависимостях между типами
   - Сложно определить импорты/usings

5. **Смешение форматов генерации:**

   - `typescript-model`, `zod-schema`, `typescript-enum`, `zod-enum` - по сути одно и то же, просто разные форматы

## Целевая архитектура

### Слои приложения

```
┌─────────────────────────────────────────┐
│         Presentation Layer              │
│  - CLI Commands                         │
│  - Configuration                        │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│         Orchestration Layer             │
│  - CodeGenerator (координатор)          │
│  - GenerationOrchestrator               │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│         Data Processing Layer           │
│  - Extractors (Models, Operations, Enums)│
│  - Type Mappers                         │
│  - Schema Resolvers                     │
│  - Dependency Analyzers                 │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│         Rendering Layer                 │
│  - Code Renderers (String Builders)    │
│  - Import/Using Collectors              │
│  - Formatters                           │
└─────────────────────────────────────────┘
```

### Принципы разделения

1. **Парсинг (Data Extraction):** Извлечение данных из OpenAPI без знания о целевом языке
2. **Обработка (Data Processing):** Маппинг типов, анализ зависимостей, подготовка данных
3. **Рендеринг (Code Generation):** Только форматирование готовых данных в код

## Этап 1: Создание общих моделей и абстракций

### 1.1. Перемещение TypeInfo в ValIO.Core

**Файл:** `ValIO.Core/Models/TypeInfo.cs`

**Изменения:**

- Переместить из `ValIO.Generators.CSharp.Models` в `ValIO.Core.Models`
- Удалить свойства специфичные для C#: `IsTask`, `IsHttpResponseMessage`
- Добавить универсальные свойства:
  - `Language` - язык генерации (для определения синтаксиса)
  - `Dependencies` - список зависимостей (имена типов, которые нужно импортировать)
  - `IsOptional` - для TypeScript (вместо nullable)
  - `Format` - формат типа (enum, union, intersection)

**Методы:**

- Удалить `Task()`, `HttpResponseMessage()`
- Оставить универсальные: `Simple()`, `Collection()`, `Dictionary()`, `Nullable()`

### 1.2. Создание универсальных Template Data моделей

**Файлы:**

- `ValIO.Core/Models/TemplateData/ModelTemplateData.cs` - универсальная модель
- `ValIO.Core/Models/TemplateData/EnumTemplateData.cs` - универсальный enum
- `ValIO.Core/Models/TemplateData/OperationTemplateData.cs` - универсальная операция
- `ValIO.Core/Models/TemplateData/ClientTemplateData.cs` - универсальный клиент

**Структура:**

- Все данные уже подготовлены для рендеринга
- Список зависимостей (импортов) уже собран
- Типы уже отформатированы
- Нет логики в моделях

### 1.3. Создание интерфейсов для рендеринга

**Файлы:**

- `ValIO.Core/Interfaces/ICodeRenderer.cs` - базовый интерфейс рендеринга
- `ValIO.Core/Interfaces/IModelRenderer.cs` - рендеринг моделей
- `ValIO.Core/Interfaces/IEnumRenderer.cs` - рендеринг enum'ов
- `ValIO.Core/Interfaces/IClientRenderer.cs` - рендеринг клиентов
- `ValIO.Core/Interfaces/IOperationRenderer.cs` - рендеринг операций

**Интерфейсы:**

```csharp
public interface ICodeRenderer<TLanguage>
{
    string RenderModel(ModelTemplateData data);
    string RenderEnum(EnumTemplateData data);
    string RenderClient(ClientTemplateData data);
    string RenderOperation(OperationTemplateData data);
}
```

## Этап 2: Рефакторинг TypeMapper

### 2.1. Создание базового TypeMapper

**Файл:** `ValIO.Core/Services/BaseTypeMapper.cs`

**Цель:** Вынести общую логику маппинга типов

**Методы:**

- `MapSchemaToTypeInfo()` - универсальный маппинг
- `ResolveSchemaReference()` - разрешение ссылок
- `DetectCollection()` - определение коллекций
- `DetectDictionary()` - определение словарей
- `DetectPrimitive()` - определение примитивов

### 2.2. Рефакторинг CSharpTypeMapper

**Файл:** `ValIO.Generators.CSharp/CSharpTypeMapper.cs`

**Изменения:**

- Наследоваться от `BaseTypeMapper`
- Переопределять только C# специфичную логику
- Убрать дублирование с TypeScriptTypeMapper

### 2.3. Рефакторинг TypeScriptTypeMapper

**Файл:** `ValIO.Generators.TypeScript/TypeScriptTypeMapper.cs`

**Изменения:**

- Наследоваться от `BaseTypeMapper`
- Переопределять только TypeScript специфичную логику
- Убрать дублирование с CSharpTypeMapper

### 2.4. Создание DependencyAnalyzer

**Файл:** `ValIO.Core/Services/DependencyAnalyzer.cs`

**Цель:** Анализ зависимостей между типами

**Методы:**

- `AnalyzeModelDependencies()` - анализ зависимостей модели
- `AnalyzeOperationDependencies()` - анализ зависимостей операции
- `CollectAllDependencies()` - сбор всех зависимостей
- `ResolveImportPaths()` - определение путей импорта

## Этап 3: Разделение генераторов на слои

### 3.1. Создание BaseCodeGenerator

**Файл:** `ValIO.Core/Services/BaseCodeGenerator.cs`

**Цель:** Общая логика для всех генераторов

**Методы:**

- `ExtractData()` - извлечение данных (делегирует extractors)
- `ProcessData()` - обработка данных (маппинг типов, анализ зависимостей)
- `GenerateCode()` - генерация кода (делегирует renderers)
- `ValidateInputs()` - валидация входных данных
- `GroupOperations()` - группировка операций

### 3.2. Рефакторинг CSharpCodeGenerator

**Файл:** `ValIO.Generators.CSharp/CSharpCodeGenerator.cs`

**Изменения:**

- Наследоваться от `BaseCodeGenerator`
- Оставить только C# специфичную логику
- Убрать дублирование с TypeScriptCodeGenerator

### 3.3. Рефакторинг TypeScriptCodeGenerator

**Файл:** `ValIO.Generators.TypeScript/TypeScriptCodeGenerator.cs`

**Изменения:**

- Наследоваться от `BaseCodeGenerator`
- Оставить только TypeScript специфичную логику
- Убрать дублирование с CSharpCodeGenerator

## Этап 4: Создание слоя рендеринга

### 4.1. Создание CSharpCodeRenderer

**Файл:** `ValIO.Generators.CSharp/CSharpCodeRenderer.cs`

**Цель:** Рендеринг C# кода через StringBuilders

**Методы:**

- `RenderModel()` - рендеринг класса/record
- `RenderEnum()` - рендеринг enum
- `RenderClient()` - рендеринг клиента
- `RenderOperation()` - рендеринг метода
- `RenderUsings()` - рендеринг using директив

**Принципы:**

- Только форматирование готовых данных
- Нет логики определения типов, импортов, форматирования
- Все данные уже подготовлены в TemplateData

### 4.2. Создание TypeScriptCodeRenderer

**Файл:** `ValIO.Generators.TypeScript/TypeScriptCodeRenderer.cs`

**Цель:** Рендеринг TypeScript кода через StringBuilders

**Методы:**

- `RenderModel()` - рендеринг interface/type
- `RenderEnum()` - рендеринг enum/union type
- `RenderClient()` - рендеринг клиента (fetch/axios)
- `RenderOperation()` - рендеринг метода
- `RenderImports()` - рендеринг import директив

### 4.3. Создание ZodSchemaRenderer

**Файл:** `ValIO.Generators.TypeScript.Zod/ZodSchemaRenderer.cs`

**Цель:** Рендеринг Zod схем через StringBuilders

**Методы:**

- `RenderSchema()` - рендеринг Zod схемы модели
- `RenderEnumSchema()` - рендеринг Zod enum схемы
- `RenderImports()` - рендеринг импортов

**Примечание:** Это не отдельный генератор, а renderer для формата ValidationSchemas

### 4.4. Удаление Razor шаблонов

**Действия:**

- Удалить все `.cshtml` файлы из `Templates/`
- Удалить зависимость от RazorLight
- Удалить `ITemplateRegistry`, `TemplateRegistry`, `TemplateRegistryFactory`

## Этап 5: Унификация форматов генерации

### 5.1. Создание ModelFormatRenderer

**Файл:** `ValIO.Core/Interfaces/IModelFormatRenderer.cs`

**Цель:** Абстракция для разных форматов моделей

**Реализации:**

- `InterfaceModelRenderer` - для TypeScript interfaces
- `ZodSchemaModelRenderer` - для Zod схем
- `ClassModelRenderer` - для C# классов
- `RecordModelRenderer` - для C# records

### 5.2. Создание EnumFormatRenderer

**Файл:** `ValIO.Core/Interfaces/IEnumFormatRenderer.cs`

**Цель:** Абстракция для разных форматов enum'ов

**Реализации:**

- `TypeScriptEnumRenderer` - для TypeScript enum
- `TypeScriptUnionTypeRenderer` - для TypeScript union types
- `ZodEnumSchemaRenderer` - для Zod enum схем
- `CSharpEnumRenderer` - для C# enum

### 5.3. Рефакторинг генерации моделей

**Изменения:**

- Один метод `GenerateModels()` в BaseCodeGenerator
- Выбор renderer'а на основе конфигурации
- Унифицированная обработка всех форматов

## Этап 6: Рефакторинг Extractors

### 6.1. Улучшение ModelExtractor

**Файл:** `ValIO.Core/Services/ModelExtractor.cs`

**Изменения:**

- Возвращать `ModelTemplateData` вместо `ApiModel`
- Собирать зависимости сразу при извлечении
- Определять формат модели (class/interface/schema)

### 6.2. Улучшение OperationExtractor

**Файл:** `ValIO.Core/Services/OperationExtractor.cs`

**Изменения:**

- Возвращать `OperationTemplateData` вместо `ApiOperation`
- Собирать зависимости сразу при извлечении
- Определять типы параметров через TypeMapper

### 6.3. Улучшение EnumExtractor

**Файл:** `ValIO.Core/Services/EnumExtractor.cs`

**Изменения:**

- Возвращать `EnumTemplateData` вместо `ApiEnum`
- Определять формат enum'а (enum/union/schema)

## Этап 7: Создание сервисов обработки данных

### 7.1. Создание TypeInfoBuilder

**Файл:** `ValIO.Core/Services/TypeInfoBuilder.cs`

**Цель:** Построение TypeInfo из OpenAPI схем

**Методы:**

- `BuildFromSchema()` - построение из схемы
- `BuildCollection()` - построение коллекции
- `BuildDictionary()` - построение словаря
- `BuildNullable()` - построение nullable типа

### 7.2. Создание DependencyCollector

**Файл:** `ValIO.Core/Services/DependencyCollector.cs`

**Цель:** Сбор зависимостей из TypeInfo

**Методы:**

- `CollectFromTypeInfo()` - сбор из одного TypeInfo
- `CollectFromModel()` - сбор из модели
- `CollectFromOperation()` - сбор из операции
- `ResolveImportPaths()` - определение путей импорта

### 7.3. Создание ImportPathResolver

**Файл:** `ValIO.Core/Services/ImportPathResolver.cs`

**Цель:** Определение путей импорта/usings

**Методы:**

- `ResolveModelImport()` - путь к модели
- `ResolveEnumImport()` - путь к enum
- `ResolveSchemaImport()` - путь к схеме (Zod)
- `ResolveClientImport()` - путь к клиенту

## Этап 8: Рефакторинг Zod генератора

### 8.1. Превращение ZodSchemaGenerator в Renderer

**Файл:** `ValIO.Generators.TypeScript.Zod/ZodSchemaRenderer.cs`

**Изменения:**

- Переименовать `ZodSchemaGenerator` в `ZodSchemaRenderer`
- Реализовать `IModelFormatRenderer`
- Убрать логику маппинга (переместить в ZodTypeMapper)
- Использовать StringBuilders вместо Razor

### 8.2. Улучшение ZodTypeMapper

**Файл:** `ValIO.Generators.TypeScript.Zod/ZodTypeMapper.cs`

**Изменения:**

- Возвращать структурированные данные вместо строк
- Собирать зависимости автоматически
- Не генерировать код, только данные

## Этап 9: Рефакторинг TanStackQueryGenerator

### 9.1. Превращение в Renderer

**Файл:** `ValIO.Generators.TypeScript.TanStackQuery/TanStackQueryRenderer.cs`

**Изменения:**

- Переименовать `TanStackQueryGenerator` в `TanStackQueryRenderer`
- Реализовать `IQueryWrapperRenderer`
- Использовать StringBuilders
- Получать готовые данные, не обрабатывать OpenAPI

### 9.2. Интеграция в TypeScriptCodeGenerator

**Изменения:**

- Использовать renderer вместо генератора
- Передавать готовые OperationTemplateData
- Не дублировать логику обработки операций

## Этап 10: Удаление дублирования между форматами

### 10.1. Унификация Model/Enum генерации

**Принцип:** Один extractor, один processor, несколько renderers

**Структура:**

```
ModelExtractor → ModelProcessor → ModelFormatRenderer (выбор на основе конфига)
  ↓                ↓                    ↓
ApiModel      ModelTemplateData    Interface/Zod/Class
```

### 10.2. Удаление дублирования в шаблонах

**Действия:**

- `typescript-model` и `zod-schema` - один processor, разные renderers
- `typescript-enum` и `zod-enum` - один processor, разные renderers

## Этап 11: Оптимизация и очистка

### 11.1. Удаление неиспользуемого кода

**Файлы для удаления:**

- Все `.cshtml` шаблоны
- `ITemplateRegistry`, `TemplateRegistry`, `TemplateRegistryFactory`
- `TemplateCache`
- Мусорные свойства из TypeInfo

### 11.2. Улучшение производительности

**Оптимизации:**

- Кэширование TypeInfo
- Кэширование зависимостей
- Ленивая загрузка renderers

### 11.3. Улучшение тестируемости

**Изменения:**

- Каждый слой тестируется отдельно
- Renderers тестируются на готовых данных
- Extractors тестируются на OpenAPI схемах

## Порядок реализации

1. **Этап 1:** Создание общих моделей (TypeInfo, TemplateData)
2. **Этап 2:** Рефакторинг TypeMapper (базовый класс, унификация)
3. **Этап 6:** Улучшение Extractors (возврат TemplateData)
4. **Этап 7:** Создание сервисов обработки (TypeInfoBuilder, DependencyCollector)
5. **Этап 4:** Создание Renderers (StringBuilders вместо Razor)
6. **Этап 3:** Рефакторинг генераторов (базовый класс, разделение слоев)
7. **Этап 5:** Унификация форматов (ModelFormatRenderer, EnumFormatRenderer)
8. **Этап 8-9:** Рефакторинг Zod и TanStackQuery (превращение в renderers)
9. **Этап 10:** Удаление дублирования
10. **Этап 11:** Оптимизация и очистка

## Ключевые принципы

1. **Разделение ответственности:** Парсинг → Обработка → Рендеринг
2. **Работа с объектами:** Всегда использовать объектное представление, не строки
3. **Мета-информация:** Собирать зависимости, пути импорта заранее
4. **Унификация:** Один processor, несколько renderers для разных форматов
5. **Тестируемость:** Каждый слой тестируется независимо

## Ожидаемые результаты

1. **Уменьшение дублирования:** ~70% кода будет общим
2. **Улучшение поддерживаемости:** Четкое разделение слоев
3. **Упрощение тестирования:** Каждый слой тестируется отдельно
4. **Гибкость:** Легко добавлять новые форматы (новые renderers)
5. **Производительность:** Кэширование, оптимизация