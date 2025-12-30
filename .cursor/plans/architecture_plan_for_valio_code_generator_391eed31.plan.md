---
name: Architecture Plan for ValIO Code Generator
overview: Детальный план разработки консольной утилиты для генерации HTTP клиентов, моделей, методов и enum из OpenAPI схемы с поддержкой C#, TypeScript (с различными конфигурациями) и будущей поддержкой C++ для Unreal Engine.
todos: []
---

# План разработки консольной утилиты ValIO

## Архитектура решения

### Структура решения (Solution Structure)

Решение будет состоять из следующих проектов:

1. **ValIO** - консольное приложение (entry point)
2. **ValIO.Core** - основная бизнес-логика, абстракции, модели
3. **ValIO.Parsers** - парсинг OpenAPI схем (JSON/YAML)
4. **ValIO.Configuration** - конфигурация генерации
5. **ValIO.Generators.CSharp** - генератор для C#
6. **ValIO.Generators.TypeScript** - генератор для TypeScript
7. **ValIO.Generators.TypeScript.Zod** - генератор TypeScript с Zod
8. **ValIO.Generators.TypeScript.Axios** - генератор TypeScript с Axios
9. **ValIO.Generators.TypeScript.TanStackQuery** - генератор TypeScript с TanStack Query
10. **ValIO.Generators.Cpp** - генератор для C++ (заготовка для будущего)
11. **ValIO.CLI** - CLI интерфейс (может быть частью ValIO или отдельным проектом)

### Архитектурные слои

```javascript
┌─────────────────────────────────────────┐
│         ValIO (CLI Application)         │
│  - Program.cs                           │
│  - DI Container Setup                   │
│  - Command Handlers                     │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│           ValIO.CLI                     │
│  - Commands                             │
│  - Options                              │
│  - Command Handlers                     │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│          ValIO.Configuration            │
│  - Configuration Models                 │
│  - Configuration Loader                 │
│  - Configuration Validator              │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│          ValIO.Parsers                  │
│  - OpenAPI Parser                       │
│  - Schema Resolver                      │
│  - Reference Resolver                   │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│           ValIO.Core                    │
│  - Domain Models                        │
│  - Interfaces                           │
│  - Services                             │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│    ValIO.Generators.* (Strategy)        │
│  - CSharp Generator                     │
│  - TypeScript Generators                │
│  - Cpp Generator                        │
└─────────────────────────────────────────┘
```

## Детальная структура проектов

### 1. ValIO.Core

**Назначение**: Базовые абстракции, модели домена, интерфейсы

#### Интерфейсы

- `ICodeGenerator` - базовый интерфейс для всех генераторов
  ```csharp
          public interface ICodeGenerator
          {
              Task<GenerationResult> GenerateAsync(OpenApiDocument document, GenerationConfiguration config);
              bool SupportsLanguage(GenerationLanguage language);
              bool SupportsOptions(GenerationOptions options);
          }
  ```

- `ISchemaResolver` - разрешение ссылок в OpenAPI схеме
  ```csharp
          public interface ISchemaResolver
          {
              OpenApiSchema ResolveReference(OpenApiReference reference);
              OpenApiSchema ResolveSchema(OpenApiSchema schema);
              string GetSchemaName(OpenApiSchema schema, string defaultName);
          }
  ```

- `IModelExtractor` - извлечение моделей из схемы
  ```csharp
          public interface IModelExtractor
          {
              IReadOnlyList<ApiModel> ExtractModels(OpenApiDocument document);
              ApiModel ExtractModel(OpenApiSchema schema, string modelName);
          }
  ```

- `IOperationExtractor` - извлечение операций из схемы
  ```csharp
          public interface IOperationExtractor
          {
              IReadOnlyList<ApiOperation> ExtractOperations(OpenApiDocument document);
              ApiOperation ExtractOperation(OpenApiPathItem pathItem, OperationType operationType, string path);
          }
  ```

- `ITemplateRenderer` - рендеринг шаблонов кода
  ```csharp
          public interface ITemplateRenderer
          {
              string Render(string template, object model);
              Task<string> RenderFileAsync(string templatePath, object model);
          }
  ```

- `IFileWriter` - запись сгенерированных файлов
  ```csharp
          public interface IFileWriter
          {
              Task WriteFileAsync(string path, string content);
              Task WriteFilesAsync(IReadOnlyDictionary<string, string> files);
              void EnsureDirectoryExists(string directoryPath);
          }
  ```

- `INamingConverter` - конвертация имен (camelCase, PascalCase, etc.)
  ```csharp
          public interface INamingConverter
          {
              string ToPascalCase(string name);
              string ToCamelCase(string name);
              string ToSnakeCase(string name);
              string ToKebabCase(string name);
              string SanitizeIdentifier(string name);
          }
  ```

- `ITypeMapper` - маппинг типов OpenAPI на типы целевого языка
  ```csharp
          public interface ITypeMapper
          {
              string MapType(OpenApiSchema schema);
              string MapPropertyType(OpenApiSchema schema, bool isRequired);
              string MapEnumType(OpenApiSchema schema);
          }
  ```


#### Модели домена

- `GenerationLanguage` - enum языков генерации
  ```csharp
          public enum GenerationLanguage
          {
              CSharp,
              TypeScript,
              Cpp
          }
  ```

- `GenerationOptions` - опции генерации (Strategy pattern)
  ```csharp
          public class GenerationOptions
          {
              public ValidationLibrary? ValidationLibrary { get; set; }
              public HttpClientLibrary? HttpClientLibrary { get; set; }
              public QueryWrapperLibrary? QueryWrapperLibrary { get; set; }
          }
  ```

- `GenerationConfiguration` - конфигурация генерации
  ```csharp
          public class GenerationConfiguration
          {
              public GenerationLanguage Language { get; set; }
              public GenerationOptions Options { get; set; }
              public FileOrganization FileOrganization { get; set; }
              public NamingConvention NamingConvention { get; set; }
              public string OutputPath { get; set; }
              public string? Namespace { get; set; }
              public bool GenerateEnums { get; set; }
              public bool GenerateModels { get; set; }
              public bool GenerateClients { get; set; }
              // ... другие настройки
          }
  ```

- `FileOrganization` - организация файлов
  ```csharp
          public class FileOrganization
          {
              public FileGroupingStrategy GroupingStrategy { get; set; } // ByTag, ByRoute, SingleFile
              public bool SeparateModels { get; set; }
              public bool SeparateClients { get; set; }
              public string? ModelsPath { get; set; }
              public string? ClientsPath { get; set; }
          }
  ```

- `ApiModel` - модель данных API
  ```csharp
          public class ApiModel
          {
              public string Name { get; set; }
              public string? Description { get; set; }
              public IReadOnlyList<ApiProperty> Properties { get; set; }
              public IReadOnlyList<string> RequiredProperties { get; set; }
              public OpenApiSchema OriginalSchema { get; set; }
          }
  ```

- `ApiProperty` - свойство модели
  ```csharp
          public class ApiProperty
          {
              public string Name { get; set; }
              public string Type { get; set; }
              public bool IsRequired { get; set; }
              public bool IsNullable { get; set; }
              public string? Description { get; set; }
              public object? DefaultValue { get; set; }
          }
  ```

- `ApiOperation` - операция API
  ```csharp
          public class ApiOperation
          {
              public string OperationId { get; set; }
              public string Path { get; set; }
              public OperationType HttpMethod { get; set; }
              public string? Summary { get; set; }
              public string? Description { get; set; }
              public IReadOnlyList<string> Tags { get; set; }
              public IReadOnlyList<ApiParameter> Parameters { get; set; }
              public ApiRequestBody? RequestBody { get; set; }
              public IReadOnlyDictionary<string, ApiResponse> Responses { get; set; }
          }
  ```

- `ApiParameter` - параметр операции
  ```csharp
          public class ApiParameter
          {
              public string Name { get; set; }
              public ParameterLocation Location { get; set; } // query, path, header, cookie
              public string Type { get; set; }
              public bool IsRequired { get; set; }
              public string? Description { get; set; }
          }
  ```

- `GenerationResult` - результат генерации
  ```csharp
          public class GenerationResult
          {
              public bool Success { get; set; }
              public IReadOnlyDictionary<string, string> GeneratedFiles { get; set; }
              public IReadOnlyList<string> Errors { get; set; }
              public IReadOnlyList<string> Warnings { get; set; }
          }
  ```


#### Сервисы

- `SchemaResolver` - реализация ISchemaResolver
- `ModelExtractor` - реализация IModelExtractor
- `OperationExtractor` - реализация IOperationExtractor
- `NamingConverter` - реализация INamingConverter

### 2. ValIO.Parsers

**Назначение**: Парсинг OpenAPI схем из различных источников

#### Классы

- `OpenApiDocumentParser` - основной парсер OpenAPI документов
  ```csharp
          public class OpenApiDocumentParser : IOpenApiDocumentParser
          {
              private readonly IOpenApiReader _jsonReader;
              private readonly IOpenApiReader _yamlReader;
              private readonly IHttpClient _httpClient;
              private readonly ILogger<OpenApiDocumentParser> _logger;
              
              public Task<OpenApiDocument> ParseFromFileAsync(string filePath);
              public Task<OpenApiDocument> ParseFromUrlAsync(string url);
              public Task<OpenApiDocument> ParseFromStreamAsync(Stream stream, string? format);
          }
  ```

- `OpenApiSchemaResolver` - разрешение ссылок в схеме
  ```csharp
          public class OpenApiSchemaResolver : ISchemaResolver
          {
              private readonly OpenApiDocument _document;
              
              public OpenApiSchema ResolveReference(OpenApiReference reference);
              public OpenApiSchema ResolveSchema(OpenApiSchema schema);
          }
  ```

- `ReferenceResolver` - разрешение $ref ссылок (Visitor pattern)
- `OpenApiValidator` - валидация OpenAPI документа

#### Интерфейсы

- `IOpenApiDocumentParser`
  ```csharp
          public interface IOpenApiDocumentParser
          {
              Task<OpenApiDocument> ParseFromFileAsync(string filePath);
              Task<OpenApiDocument> ParseFromUrlAsync(string url);
              Task<OpenApiDocument> ParseFromStreamAsync(Stream stream, string? format);
          }
  ```


### 3. ValIO.Configuration

**Назначение**: Загрузка и валидация конфигурации

#### Классы

- `ConfigurationLoader` - загрузка конфигурации из файла/строки
  ```csharp
          public class ConfigurationLoader : IConfigurationLoader
          {
              public Task<GenerationConfiguration> LoadFromFileAsync(string filePath);
              public GenerationConfiguration LoadFromJson(string json);
              public GenerationConfiguration LoadFromYaml(string yaml);
          }
  ```

- `ConfigurationValidator` - валидация конфигурации
  ```csharp
          public class ConfigurationValidator : IConfigurationValidator
          {
              public ValidationResult Validate(GenerationConfiguration config);
          }
  ```

- `ConfigurationBuilder` - строитель конфигурации (Builder pattern)
  ```csharp
          public class ConfigurationBuilder
          {
              public ConfigurationBuilder WithLanguage(GenerationLanguage language);
              public ConfigurationBuilder WithOptions(GenerationOptions options);
              public ConfigurationBuilder WithFileOrganization(FileOrganization fileOrganization);
              public GenerationConfiguration Build();
          }
  ```


#### Модели конфигурации

- `ValIOConfig` - корневая модель конфигурации (соответствует JSON/YAML)
- `CSharpConfig` - специфичные настройки для C#
- `TypeScriptConfig` - специфичные настройки для TypeScript

### 4. ValIO.Generators.CSharp

**Назначение**: Генерация C# кода

#### Классы

- `CSharpCodeGenerator` - основной генератор (Strategy pattern)
  ```csharp
          public class CSharpCodeGenerator : ICodeGenerator
          {
              private readonly IModelExtractor _modelExtractor;
              private readonly IOperationExtractor _operationExtractor;
              private readonly ITypeMapper _typeMapper;
              private readonly INamingConverter _namingConverter;
              private readonly ITemplateRenderer _templateRenderer;
              private readonly IFileWriter _fileWriter;
              private readonly ILogger<CSharpCodeGenerator> _logger;
              
              public Task<GenerationResult> GenerateAsync(OpenApiDocument document, GenerationConfiguration config);
          }
  ```

- `CSharpModelGenerator` - генератор моделей C#
  ```csharp
          public class CSharpModelGenerator
          {
              public string GenerateModel(ApiModel model, CSharpGenerationOptions options);
              public string GenerateProperty(ApiProperty property, CSharpGenerationOptions options);
          }
  ```

- `CSharpClientGenerator` - генератор клиентов C#
  ```csharp
          public class CSharpClientGenerator
          {
              public string GenerateClient(IReadOnlyList<ApiOperation> operations, CSharpGenerationOptions options);
              public string GenerateMethod(ApiOperation operation, CSharpGenerationOptions options);
          }
  ```

- `CSharpEnumGenerator` - генератор enum'ов
- `CSharpTypeMapper` - маппинг типов OpenAPI на C# типы
- `CSharpTemplateRenderer` - рендеринг шаблонов (может использовать Scriban, Razor, или простую замену)

### 5. ValIO.Generators.TypeScript

**Назначение**: Базовый генератор TypeScript

#### Классы

- `TypeScriptCodeGenerator` - базовый генератор TypeScript
- `TypeScriptModelGenerator` - генератор интерфейсов TypeScript
- `TypeScriptClientGenerator` - генератор клиентов (базовый)
- `TypeScriptTypeMapper` - маппинг типов
- `TypeScriptEnumGenerator` - генератор enum'ов

### 6. ValIO.Generators.TypeScript.Zod

**Назначение**: Генерация Zod схем валидации

#### Классы

- `ZodSchemaGenerator` - генератор Zod схем
  ```csharp
          public class ZodSchemaGenerator
          {
              public string GenerateSchema(ApiModel model);
              public string GeneratePropertySchema(ApiProperty property);
          }
  ```

- `ZodTypeMapper` - маппинг типов на Zod типы

### 7. ValIO.Generators.TypeScript.Axios

**Назначение**: Генерация клиентов с использованием Axios

#### Классы

- `AxiosClientGenerator` - генератор клиентов с Axios
  ```csharp
          public class AxiosClientGenerator : TypeScriptClientGenerator
          {
              public override string GenerateClient(IReadOnlyList<ApiOperation> operations, TypeScriptGenerationOptions options);
              public string GenerateAxiosMethod(ApiOperation operation);
          }
  ```


### 8. ValIO.Generators.TypeScript.TanStackQuery

**Назначение**: Генерация оберток для TanStack Query

#### Классы

- `TanStackQueryGenerator` - генератор хуков TanStack Query
  ```csharp
          public class TanStackQueryGenerator
          {
              public string GenerateQueryHook(ApiOperation operation);
              public string GenerateMutationHook(ApiOperation operation);
              public string GenerateHookName(string operationId);
          }
  ```


### 9. ValIO.CLI

**Назначение**: CLI интерфейс (используя System.CommandLine)

#### Классы

- `GenerateCommand` - команда генерации
  ```csharp
          public class GenerateCommand : Command
          {
              public GenerateCommand() : base("generate", "Generate code from OpenAPI schema")
              {
                  // Options
                  var schemaOption = new Option<string>("--schema", "-s") { Required = true };
                  var outputOption = new Option<string>("--output", "-o") { Required = true };
                  var languageOption = new Option<GenerationLanguage>("--language", "-l") { Required = true };
                  var configOption = new Option<FileInfo>("--config", "-c");
                  
                  // Handler
                  this.SetHandler(ExecuteAsync, schemaOption, outputOption, languageOption, configOption);
              }
              
              private async Task<int> ExecuteAsync(string schema, string output, GenerationLanguage language, FileInfo? config);
          }
  ```

- `ValidateCommand` - команда валидации схемы
- `RootCommand` - корневая команда приложения

### 10. ValIO (Main Application)

**Назначение**: Entry point, DI setup

#### Классы

- `Program.cs` - точка входа
  ```csharp
          class Program
          {
              static async Task<int> Main(string[] args)
              {
                  var services = new ServiceCollection();
                  ConfigureServices(services);
                  
                  var serviceProvider = services.BuildServiceProvider();
                  var rootCommand = serviceProvider.GetRequiredService<RootCommand>();
                  
                  return await rootCommand.InvokeAsync(args);
              }
              
              static void ConfigureServices(IServiceCollection services)
              {
                  // Регистрация всех сервисов
              }
          }
  ```


## Паттерны проектирования

### 1. Strategy Pattern

- **Применение**: Различные генераторы кода (CSharpCodeGenerator, TypeScriptCodeGenerator, etc.)
- **Интерфейс**: `ICodeGenerator`
- **Реализации**: Все генераторы в папке Generators

### 2. Factory Pattern

- **Применение**: Создание генераторов в зависимости от языка и опций
- **Класс**: `CodeGeneratorFactory`
  ```csharp
          public class CodeGeneratorFactory
          {
              private readonly IServiceProvider _serviceProvider;
              
              public ICodeGenerator CreateGenerator(GenerationLanguage language, GenerationOptions? options);
          }
  ```


### 3. Builder Pattern

- **Применение**: Построение конфигурации генерации
- **Класс**: `ConfigurationBuilder`

### 4. Visitor Pattern

- **Применение**: Обход OpenAPI документа для извлечения данных
- **Классы**: `OpenApiDocumentVisitor`, `SchemaVisitor`, `OperationVisitor`

### 5. Template Method Pattern

- **Применение**: Базовые шаги генерации (в базовых классах генераторов)

### 6. Adapter Pattern

- **Применение**: Адаптация различных шаблонных движков (Scriban, Razor, Handlebars)

### 7. Composite Pattern

- **Применение**: Организация файлов в группы (FileOrganization)

## Шаблоны генерации кода

Для генерации кода можно использовать:

1. **Scriban** - легковесный шаблонный движок для .NET
2. **Handlebars.NET** - порт Handlebars для .NET
3. **Razor Engine** - для более сложных случаев
4. **Простая замена строк** - для простых шаблонов

Рекомендация: Начать с Scriban для простоты и производительности.

## Структура файлов и организация

### Пример структуры для C#:

```javascript
Output/
  Models/
    User.cs
    Product.cs
    Order.cs
  Clients/
    UsersClient.cs
    ProductsClient.cs
    OrdersClient.cs
  Enums/
    OrderStatus.cs
    UserRole.cs
```

### Пример структуры для TypeScript (группировка по tags):

```javascript
Output/
  models/
    user.model.ts
    product.model.ts
  api/
    users/
      users.api.ts
      users.queries.ts
    products/
      products.api.ts
      products.queries.ts
  schemas/
    user.schema.ts (zod)
    product.schema.ts (zod)
```

## Этапы разработки

### Этап 1: Базовая инфраструктура (Week 1-2)

1. Создать структуру решения и проектов
2. Настроить DI контейнер
3. Реализовать базовые интерфейсы в ValIO.Core
4. Реализовать парсер OpenAPI в ValIO.Parsers
5. Настроить логирование

### Этап 2: Конфигурация и CLI (Week 2-3)

1. Реализовать ValIO.Configuration
2. Реализовать базовый CLI интерфейс
3. Добавить команды generate и validate
4. Тестирование парсинга и конфигурации

### Этап 3: Генератор C# (Week 3-5)

1. Реализовать ValIO.Generators.CSharp
2. Реализовать генерацию моделей
3. Реализовать генерацию enum'ов
4. Реализовать генерацию клиентов (HttpClient)
5. Тестирование генератора C#

### Этап 4: Генератор TypeScript базовый (Week 5-6)

1. Реализовать ValIO.Generators.TypeScript
2. Реализовать генерацию интерфейсов
3. Реализовать генерацию enum'ов
4. Реализовать базовую генерацию клиентов (fetch)

### Этап 5: TypeScript расширения (Week 6-8)

1. Реализовать ValIO.Generators.TypeScript.Zod
2. Реализовать ValIO.Generators.TypeScript.Axios
3. Реализовать ValIO.Generators.TypeScript.TanStackQuery
4. Интеграция всех компонентов TypeScript
5. Тестирование всех вариантов TypeScript

### Этап 6: Файловая организация (Week 8-9)

1. Реализовать различные стратегии группировки файлов
2. Реализовать разделение моделей и клиентов
3. Тестирование различных конфигураций

### Этап 7: Заготовка для C++ (Week 9-10)

1. Создать структуру ValIO.Generators.Cpp
2. Реализовать базовые интерфейсы
3. Оставить место для будущей реализации

### Этап 8: Тестирование и документация (Week 10-12)

1. Написать unit тесты для всех модулей
2. Написать интеграционные тесты
3. Создать примеры конфигураций
4. Написать документацию
5. Оптимизация производительности

## Технологии и библиотеки

- **.NET 9.0** - основная платформа
- **Microsoft.OpenApi** - парсинг OpenAPI
- **System.CommandLine** - CLI интерфейс
- **Microsoft.Extensions.DependencyInjection** - DI
- **Microsoft.Extensions.Logging** - логирование
- **YamlDotNet** или **SharpYaml** - парсинг YAML конфигураций
- **Scriban** - шаблонный движок
- **System.Text.Json** - работа с JSON
- **xUnit** или **NUnit** - тестирование
- **Moq** или **NSubstitute** - моки для тестов

## Конфигурационный файл (пример)

```json
{
  "language": "TypeScript",
  "options": {
    "validationLibrary": "Zod",
    "httpClientLibrary": "Axios",
    "queryWrapperLibrary": "TanStackQuery"
  },
  "fileOrganization": {
    "groupingStrategy": "ByTag",
    "separateModels": true,
    "separateClients": true,
    "modelsPath": "models",
    "clientsPath": "api"
  },
  "outputPath": "./generated",
  "namespace": "Api",
  "generateEnums": true,
  "generateModels": true,
  "generateClients": true,
  "namingConvention": {
    "models": "PascalCase",
    "properties": "camelCase",
    "methods": "camelCase"
  }
}
```

## Расширяемость

Архитектура позволяет легко добавлять:

- Новые языки программирования (создать новый проект Generator)
- Новые библиотеки валидации для TypeScript