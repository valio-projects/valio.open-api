# Анализ процесса инициализации и запуска приложения ValIO

## 1. Точка входа (Program.cs)

### 1.1. Main метод
```csharp
static async Task<int> Main(string[] args)
```

**Процесс:**
1. Создается `ServiceCollection` для DI контейнера
2. Вызывается `ConfigureServices(services)` для регистрации всех сервисов
3. Строится `ServiceProvider` из зарегистрированных сервисов
4. Получается `RootCommand` из DI контейнера
5. Парсятся аргументы командной строки через `rootCommand.Parse(args)`
6. Выполняется команда через `parseResult.InvokeAsync()`

### 1.2. ConfigureServices - Регистрация сервисов

**Зарегистрированные сервисы:**

#### Logging
- `ILoggerFactory` и `ILogger<T>` - через `AddLogging()`
- Настроен консольный вывод с минимальным уровнем `Information`

#### HTTP Client
- `IHttpClientFactory` - через `AddHttpClient()`

#### Core Services
- `INamingConverter` → `NamingConverter` (Singleton)
- `IFileWriter` → `FileWriter` (Singleton)

#### Parsers
- `IOpenApiDocumentParser` → `OpenApiDocumentParser` (Singleton)

#### Configuration
- `IConfigurationLoader` → `ConfigurationLoader` (Singleton)
- `IConfigurationValidator` → `ConfigurationValidator` (Singleton)

#### Code Generators
- `ITypeMapper` → `CSharpTypeMapper` (Singleton)
- `ICodeGenerator` → `CSharpCodeGenerator` (Singleton)

#### Factory
- `CodeGeneratorFactory` (Singleton)

#### CLI
- `RootCommand` (Singleton)

## 2. Инициализация CLI (RootCommand.cs)

### 2.1. Конструктор RootCommand
```csharp
public RootCommand(IServiceProvider serviceProvider)
```

**Процесс:**
1. Создается команда `generate` через `GenerateCommand(serviceProvider)`
2. Создается команда `validate` через `ValidateCommand(serviceProvider)`
3. Команды добавляются в корневую команду

## 3. Выполнение команды Generate (GenerateCommand.cs)

### 3.1. Параметры команды
- `--schema, -s` - путь или URL к OpenAPI схеме (обязательный)
- `--output, -o` - путь к выходной директории (обязательный)
- `--language, -l` - целевой язык программирования (обязательный)
- `--config, -c` - путь к файлу конфигурации (опциональный)

### 3.2. Процесс выполнения (SetAction)

**Шаг 1: Валидация параметров**
- Проверка наличия `schema` и `output`
- Если отсутствуют - вывод ошибки и возврат кода 1

**Шаг 2: Получение сервисов из DI**
- `ILogger<GenerateCommand>`
- `IOpenApiDocumentParser`
- `IConfigurationLoader`
- `IConfigurationValidator`
- `CodeGeneratorFactory`

**Шаг 3: Загрузка конфигурации**
- Если указан `--config` и файл существует:
  - Загрузка конфигурации из файла через `configLoader.LoadFromFileAsync()`
- Иначе:
  - Создание конфигурации по умолчанию с параметрами из командной строки

**Шаг 4: Валидация конфигурации**
- Вызов `configValidator.Validate(generationConfig)`
- Если невалидна - вывод ошибок и возврат кода 1
- Если есть предупреждения - вывод предупреждений

**Шаг 5: Парсинг OpenAPI документа**
- Определение, является ли `schema` URL или путем к файлу
- Если URL - `parser.ParseFromUrlAsync(schema)`
- Если путь - `parser.ParseFromFileAsync(schema)`

**Шаг 6: Создание генератора**
- Вызов `factory.CreateGenerator(generationConfig.Language, generationConfig.Options)`
- Фабрика ищет подходящий генератор из зарегистрированных в DI

**Шаг 7: Генерация кода**
- Вызов `generator.GenerateAsync(document, generationConfig)`
- Если успешно - вывод информации о количестве сгенерированных файлов
- Если есть предупреждения - вывод предупреждений
- Если ошибка - вывод ошибок и возврат кода 1

## 4. Процесс генерации кода (CSharpCodeGenerator)

### 4.1. GenerateAsync метод

**Шаг 1: Предзагрузка шаблонов**
- Проверка, загружены ли шаблоны
- Если нет - определение пути к шаблонам и загрузка через `EnsureTemplatesPreloadedAsync()`

**Шаг 2: Создание SchemaResolver**
- Создание `SchemaResolver(document)` для разрешения ссылок в схеме

**Шаг 3: Создание Extractors**
- `ModelExtractor(schemaResolver, namingConverter)`
- `OperationExtractor(schemaResolver)`
- `EnumExtractor(schemaResolver, namingConverter)`

**Шаг 4: Извлечение данных**
- Извлечение моделей через `modelExtractor.ExtractModels(document)`
- Извлечение операций через `operationExtractor.ExtractOperations(document)`
- Извлечение enum'ов через `enumExtractor.ExtractEnums(document)`

**Шаг 5: Маппинг типов**
- Маппинг типов для моделей через `MapModelTypes()`
- Маппинг типов для операций через `MapOperationTypes()`

**Шаг 6: Генерация файлов**
- Если `config.GenerateEnums` - генерация enum файлов
- Если `config.GenerateModels` - генерация файлов моделей
- Если `config.GenerateClients` - генерация файлов клиентов

**Шаг 7: Запись файлов**
- Запись всех сгенерированных файлов через `fileWriter.WriteFilesAsync()`

## 5. Зависимости и их жизненный цикл

### Singleton сервисы (создаются один раз)
- `INamingConverter` - используется для преобразования имен
- `IFileWriter` - используется для записи файлов
- `IOpenApiDocumentParser` - парсер OpenAPI документов
- `IConfigurationLoader` - загрузчик конфигурации
- `IConfigurationValidator` - валидатор конфигурации
- `ITypeMapper` - маппер типов (CSharpTypeMapper)
- `ICodeGenerator` - генератор кода (CSharpCodeGenerator)
- `CodeGeneratorFactory` - фабрика генераторов
- `RootCommand` - корневая команда CLI

### Сервисы, создаваемые внутри генератора (на каждый документ)
- `SchemaResolver` - создается для каждого OpenAPI документа
- `ModelExtractor` - создается с SchemaResolver
- `OperationExtractor` - создается с SchemaResolver
- `EnumExtractor` - создается с SchemaResolver
- `TemplateRegistry` - создается при первом использовании генератора

## 6. Поток данных

```
Command Line Args
    ↓
RootCommand.Parse()
    ↓
GenerateCommand.SetAction()
    ↓
[DI] IOpenApiDocumentParser → ParseFromFileAsync/ParseFromUrlAsync
    ↓
OpenApiDocument
    ↓
[DI] CodeGeneratorFactory → CreateGenerator()
    ↓
[DI] CSharpCodeGenerator → GenerateAsync()
    ↓
SchemaResolver (новый для документа)
    ↓
Extractors (новые с SchemaResolver)
    ↓
Extracted Data (Models, Operations, Enums)
    ↓
Type Mapping
    ↓
Code Generation (через TemplateRegistry)
    ↓
[DI] IFileWriter → WriteFilesAsync()
    ↓
Generated Files
```

## 7. Особенности архитектуры

### 7.1. Dependency Injection
- Все сервисы регистрируются в `ConfigureServices()`
- Генераторы получают зависимости через конструктор
- Extractors создаются внутри генератора, так как требуют SchemaResolver

### 7.2. Factory Pattern
- `CodeGeneratorFactory` используется для создания генераторов
- Фабрика ищет подходящий генератор из зарегистрированных в DI

### 7.3. Strategy Pattern
- Различные генераторы реализуют `ICodeGenerator`
- Выбор генератора происходит через фабрику на основе языка и опций

### 7.4. Template Pattern
- Шаблоны загружаются при первом использовании генератора
- Используется Scriban для рендеринга шаблонов

## 8. Возможные улучшения

1. **Ленивая загрузка шаблонов**: Шаблоны загружаются при первом использовании, что хорошо, но можно добавить предзагрузку при старте приложения

2. **Кэширование SchemaResolver**: SchemaResolver создается для каждого документа, можно добавить кэширование

3. **Параллельная обработка**: Можно добавить параллельную обработку нескольких документов

4. **Валидация на раннем этапе**: Добавить валидацию параметров командной строки до создания сервисов

5. **Обработка ошибок**: Улучшить обработку ошибок с более детальными сообщениями
