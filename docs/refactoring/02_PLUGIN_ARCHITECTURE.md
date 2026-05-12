# 02 — Plugin Architecture: Self-registering генераторы и парсинг конфига

> **Этап:** 0 (фундамент)  
> **Приоритет:** Критический  
> **Блокирует:** Этапы 1–4

---

## Содержание

1. [Анализ текущих проблем](#1-анализ-текущих-проблем)
2. [Целевая Plugin-архитектура](#2-целевая-plugin-архитектура)
3. [IGeneratorPlugin — контракт плагина](#3-igeneratorplugin--контракт-плагина)
4. [IGeneratorOptionsDescriptor — самостоятельный парсинг конфига](#4-igeneratoroptionsdescriptor--самостоятельный-парсинг-конфига)
5. [ICodeGenerationPipeline и ICodeGenerationStep](#5-icodegeneration-pipeline-и-icodegenerationstep)
6. [GeneratorRegistry — замена CodeGeneratorFactory](#6-generatorregistry--замена-codegeneratorfactory)
7. [Рефакторинг ConfigurationLoader](#7-рефакторинг-configurationloader)
8. [Рефакторинг GenerationOptions](#8-рефакторинг-generationoptions)
9. [CLI: self-registering аргументы и help](#9-cli-self-registering-аргументы-и-help)
10. [Примеры реализации плагинов](#10-примеры-реализации-плагинов)
11. [DI регистрация](#11-di-регистрация)
12. [Карта файлов и изменений](#12-карта-файлов-и-изменений)

---

## 1. Анализ текущих проблем

### 1.1 ConfigurationLoader — монолит

```csharp
// ValIO.Configuration/Services/ConfigurationLoader.cs
// ~500 строк, знает о ВСЕХ конкретных генераторах:

public GenerationConfiguration MapToGenerationConfiguration(ProjectConfig projectConfig)
{
    // ...
    if (projectConfig.Options.CSharp != null)
        generationOptions.CSharpOptions = ParseCSharpOptions(...);  // C# специфика

    if (projectConfig.Options.TypeScript != null)
        generationOptions.TypeScriptOptions = ParseTypeScriptOptions(...);  // TS специфика
    
    if (projectConfig.Options.Cpp != null)
        generationOptions.CppOptions = ParseCppOptions(...);  // C++ специфика
    
    // При добавлении Python, PHP, Java — нужно добавлять сюда!
}
```

**Нарушения:** OCP (нельзя расширить без изменения), DIP (зависит от конкретных реализаций), SRP (делает слишком много).

### 1.2 CodeGeneratorFactory — switch на языки

```csharp
// ValIO/Factories/CodeGeneratorFactory.cs
public ICodeGenerator CreateGenerator(GenerationLanguage language, GenerationOptions? options)
{
    return language switch
    {
        GenerationLanguage.CSharp      => ...,
        GenerationLanguage.TypeScript  => ...,
        GenerationLanguage.Cpp         => ...,
        _ => throw new NotSupportedException(...)  // добавить язык = редактировать Core!
    };
}
```

### 1.3 GenerationLanguage enum

```csharp
// ValIO.Core/Models/GenerationLanguage.cs
public enum GenerationLanguage { CSharp, TypeScript, Cpp }
// PHP, Python, Java, Kotlin, Swift, Dart — всё нужно добавлять сюда!
```

### 1.4 ValidationLibrary и QueryWrapperLibrary

```csharp
// Core знает о конкретных библиотеках:
public enum ValidationLibrary { Zod, Yup, Valli }  // нет Valibot, TypeBox, Zod4!
public enum QueryWrapperLibrary { TanStackQuery, SWR }
```

### 1.5 TypeScriptCodeGenerator — God Object

`TypeScriptCodeGenerator.GenerateCodeFromIr()` напрямую создаёт и вызывает:
- `ZodSchemaRenderer`
- `TanStackQueryHookAstBuilder`
- `TypeScriptParamsAstBuilder`
- `TypeScriptResultsAstBuilder`
- и т.д.

При добавлении нового транспорта (Ky, Angular) нужно редактировать этот класс.

---

## 2. Целевая Plugin-архитектура

### Принципы

- Каждый генератор/стек/транспорт — **самостоятельный плагин**
- Плагин **регистрирует себя** в DI — больше никто о нём не знает
- `ValIO` (CLI) — просто регистрирует плагины и запускает pipeline
- `ValIO.Core` — не знает о конкретных языках и библиотеках

### Граф компонентов

```
IGeneratorPlugin
    ├── string LanguageId          "typescript" / "csharp" / "php" / ...
    ├── string[] TransportIds       "fetch" / "axios" / "ky" / ...
    │
    ├── IGeneratorOptionsDescriptor   — парсинг config + CLI args + validation
    ├── ICodeGenerationPipeline       — список шагов генерации
    │   └── ICodeGenerationStep[]
    │       ├── ModelsStep
    │       ├── EnumsStep
    │       ├── FetchClientStep
    │       ├── AxiosClientStep
    │       └── ...
    │
    └── IServiceRegistrar             — регистрация своих сервисов в DI
```

---

## 3. IGeneratorPlugin — контракт плагина

```csharp
// ValIO.Core/Plugins/IGeneratorPlugin.cs

/// <summary>
/// Контракт генераторного плагина.
/// Каждый плагин самостоятельно регистрирует свои возможности.
/// Паттерн: Self-Registering Component (вариация Registry + Strategy)
/// </summary>
public interface IGeneratorPlugin
{
    /// <summary>
    /// Уникальный идентификатор языка (строка, не enum).
    /// Примеры: "typescript", "csharp", "php", "python", "java"
    /// </summary>
    string LanguageId { get; }

    /// <summary>
    /// Human-readable название плагина
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Описание плагина для --help
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Алиасы языка (для парсинга конфига): "ts", "typescript", "TypeScript"
    /// </summary>
    IReadOnlyList<string> LanguageAliases { get; }

    /// <summary>
    /// Дескриптор опций — знает как парсить конфиг и CLI аргументы
    /// </summary>
    IGeneratorOptionsDescriptor OptionsDescriptor { get; }

    /// <summary>
    /// Поддерживаемые транспорты/стеки данного плагина
    /// (пустой список = все конфигурации)
    /// </summary>
    IReadOnlyList<ITransportDescriptor> SupportedTransports { get; }

    /// <summary>
    /// Создаёт pipeline генерации для заданной конфигурации.
    /// Паттерн: Factory Method
    /// </summary>
    ICodeGenerationPipeline CreatePipeline(
        GenerationContext context,
        IServiceProvider services);

    /// <summary>
    /// Регистрирует свои сервисы в DI-контейнер.
    /// Вызывается один раз при старте приложения.
    /// </summary>
    void ConfigureServices(IServiceCollection services);
}

/// <summary>
/// Описание поддерживаемого транспорта/стека
/// </summary>
public interface ITransportDescriptor
{
    string TransportId { get; }     // "fetch", "axios", "ky", "angular-rxjs", "angular-promise"
    string DisplayName { get; }     // "Fetch API", "Axios", "Ky", "Angular HttpClient (RxJS)"
    string[] Aliases { get; }       // ["fetch", "native-fetch"]
}
```

---

## 4. IGeneratorOptionsDescriptor — самостоятельный парсинг конфига

```csharp
// ValIO.Core/Plugins/IGeneratorOptionsDescriptor.cs

/// <summary>
/// Отвечает за:
/// 1. Парсинг своих опций из конфига (Dictionary<string, object>)
/// 2. Регистрацию своих CLI аргументов
/// 3. Валидацию опций
/// 4. Генерацию help-текста для своих опций
/// 
/// Паттерн: Strategy + Visitor (каждый плагин сам описывает свои опции)
/// </summary>
public interface IGeneratorOptionsDescriptor
{
    /// <summary>
    /// Секция в JSON конфиге (например, "typescript", "axios", "zod")
    /// </summary>
    string ConfigSectionKey { get; }

    /// <summary>
    /// Парсит опции из сырого словаря (от JSON/YAML десериализации).
    /// Возвращает типизированный объект опций плагина.
    /// </summary>
    /// <param name="rawOptions">Сырые опции из конфига</param>
    /// <param name="errors">Ошибки парсинга (out parameter)</param>
    /// <returns>Типизированные опции или null при ошибке</returns>
    object? ParseOptions(
        IReadOnlyDictionary<string, object?> rawOptions,
        out IReadOnlyList<string> errors);

    /// <summary>
    /// Валидирует распарсенные опции в контексте всей конфигурации генерации.
    /// </summary>
    IReadOnlyList<string> ValidateOptions(object options, GenerationConfiguration config);

    /// <summary>
    /// Описание опций для --help
    /// </summary>
    IReadOnlyList<OptionDescription> GetOptionDescriptions();
}

/// <summary>
/// Описание одной опции для help-текста
/// </summary>
public sealed record OptionDescription(
    string Name,
    string Description,
    string? DefaultValue,
    string[] ValidValues,
    bool IsRequired);
```

### Алгоритм работы ParseOptions

```
ParseOptions(rawOptions, out errors):
    1. Получить значение по ConfigSectionKey из rawOptions
    2. Если раздел отсутствует → вернуть default опции (не ошибка)
    3. Десериализовать секцию в типизированный объект (System.Text.Json или ручной маппинг)
    4. Валидировать каждое поле, собирать ошибки
    5. Вернуть объект (или null если критическая ошибка)
```

---

## 5. ICodeGenerationPipeline и ICodeGenerationStep

### ICodeGenerationPipeline

```csharp
// ValIO.Core/Plugins/ICodeGenerationPipeline.cs

/// <summary>
/// Pipeline генерации кода.
/// Паттерн: Chain of Responsibility — шаги выполняются последовательно.
/// Каждый шаг получает общий контекст и может добавлять файлы в результат.
/// </summary>
public interface ICodeGenerationPipeline
{
    /// <summary>
    /// Упорядоченный список шагов.
    /// Шаги могут зависеть от результатов предыдущих (через контекст).
    /// </summary>
    IReadOnlyList<ICodeGenerationStep> Steps { get; }

    /// <summary>
    /// Выполняет все шаги и возвращает агрегированный результат.
    /// </summary>
    Task<GenerationResult> ExecuteAsync(
        GenerationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Общий контекст генерации, передаётся через все шаги.
/// Паттерн: Context Object (данные доступны всем шагам, иммутабельны)
/// </summary>
public sealed class GenerationContext
{
    public required IrDocument IrDocument { get; init; }
    public required GenerationConfiguration Configuration { get; init; }
    public required object PluginOptions { get; init; }    // типизированные опции плагина
    public required IServiceProvider Services { get; init; }
    public ICollection<string> Warnings { get; } = new List<string>();
}
```

### ICodeGenerationStep

```csharp
// ValIO.Core/Plugins/ICodeGenerationStep.cs

/// <summary>
/// Один шаг генерации кода.
/// Паттерн: Command + Chain of Responsibility
/// 
/// Каждый шаг:
/// - независим (не знает о других шагах)
/// - отвечает только за свою часть генерации (SRP)
/// - может быть включён/выключен через ShouldExecute
/// </summary>
public interface ICodeGenerationStep
{
    /// <summary>
    /// Человекочитаемое имя шага (для логирования)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Порядок выполнения (меньше = раньше).
    /// Например: Models=10, Enums=20, Schemas=30, Clients=40, Hooks=50
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Проверяет, нужно ли выполнять этот шаг для текущей конфигурации.
    /// Паттерн: Specification
    /// </summary>
    bool ShouldExecute(GenerationContext context);

    /// <summary>
    /// Выполняет шаг, возвращает сгенерированные файлы.
    /// Key = путь к файлу (абсолютный), Value = содержимое файла.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
        GenerationContext context,
        CancellationToken cancellationToken = default);
}
```

### Базовый Pipeline

```csharp
// ValIO.Core/Plugins/CodeGenerationPipeline.cs

public sealed class CodeGenerationPipeline : ICodeGenerationPipeline
{
    private readonly IReadOnlyList<ICodeGenerationStep> _steps;
    private readonly ILogger<CodeGenerationPipeline> _logger;
    private readonly IFileWriter _fileWriter;

    public CodeGenerationPipeline(
        IEnumerable<ICodeGenerationStep> steps,
        ILogger<CodeGenerationPipeline> logger,
        IFileWriter fileWriter)
    {
        // Сортируем шаги по Order при создании
        _steps = steps.OrderBy(s => s.Order).ToImmutableList();
        _logger = logger;
        _fileWriter = fileWriter;
    }

    public IReadOnlyList<ICodeGenerationStep> Steps => _steps;

    public async Task<GenerationResult> ExecuteAsync(
        GenerationContext context,
        CancellationToken cancellationToken = default)
    {
        var allFiles = new Dictionary<string, string>();
        var errors = new List<string>();

        foreach (var step in _steps)
        {
            if (!step.ShouldExecute(context))
            {
                _logger.LogDebug("Skipping step: {StepName}", step.Name);
                continue;
            }

            _logger.LogInformation("Executing step: {StepName}", step.Name);

            try
            {
                var files = await step.ExecuteAsync(context, cancellationToken);
                foreach (var (path, content) in files)
                    allFiles[path] = content;
            }
            catch (Exception ex)
            {
                var err = $"Step '{step.Name}' failed: {ex.Message}";
                errors.Add(err);
                _logger.LogError(ex, "Step {StepName} failed", step.Name);
            }
        }

        // Записываем файлы одним батчем после всех шагов
        if (allFiles.Count > 0 && errors.Count == 0)
            await _fileWriter.WriteFilesAsync(allFiles);

        return new GenerationResult
        {
            Success = errors.Count == 0,
            GeneratedFiles = allFiles,
            Errors = errors,
            Warnings = context.Warnings.ToList()
        };
    }
}
```

### Пример шага: TypeScriptModelsStep

```csharp
// ValIO.Generators.TypeScript/Steps/TypeScriptModelsStep.cs

public sealed class TypeScriptModelsStep : ICodeGenerationStep
{
    private readonly TypeScriptAstBuilder _astBuilder;
    private readonly TypeScriptAstRenderer _renderer;
    private readonly IFilePathResolver _pathResolver;

    public string Name => "TypeScript Models";
    public int Order => 10;   // Модели генерируются первыми

    public bool ShouldExecute(GenerationContext context)
    {
        // Шаг выполняется если:
        // 1. Запрошена генерация моделей
        // 2. Есть модели в IR
        return context.Configuration.GenerateModels
            && context.IrDocument.Models.Count > 0;
    }

    public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
        GenerationContext context,
        CancellationToken cancellationToken = default)
    {
        var options = (TypeScriptGenerationOptions)context.PluginOptions;
        var files = new Dictionary<string, string>();

        foreach (var model in context.IrDocument.Models)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var astNode = _astBuilder.BuildModelSourceFile(model, options);
            var code = _renderer.Render(astNode);
            var path = _pathResolver.ResolveModelPath(model.Name, context.Configuration);
            files[path] = code;
        }

        return files;
    }
}
```

---

## 6. GeneratorRegistry — замена CodeGeneratorFactory

```csharp
// ValIO.Core/Plugins/IGeneratorRegistry.cs

/// <summary>
/// Реестр плагинов генераторов.
/// Паттерн: Registry / Service Locator (ограниченный — только для генераторов)
/// Плагины регистрируются через DI, не через switch.
/// </summary>
public interface IGeneratorRegistry
{
    /// <summary>
    /// Найти плагин по идентификатору языка или его алиасу.
    /// </summary>
    IGeneratorPlugin? FindPlugin(string languageId);

    /// <summary>
    /// Все зарегистрированные плагины.
    /// </summary>
    IReadOnlyList<IGeneratorPlugin> AllPlugins { get; }
}

// ValIO.Core/Plugins/GeneratorRegistry.cs
public sealed class GeneratorRegistry : IGeneratorRegistry
{
    private readonly IReadOnlyDictionary<string, IGeneratorPlugin> _byId;
    private readonly IReadOnlyList<IGeneratorPlugin> _all;

    // Все плагины инжектируются через DI как IEnumerable<IGeneratorPlugin>
    public GeneratorRegistry(IEnumerable<IGeneratorPlugin> plugins)
    {
        _all = plugins.ToImmutableList();
        _byId = _all
            .SelectMany(p => p.LanguageAliases.Append(p.LanguageId)
                .Select(alias => (alias.ToLowerInvariant(), p)))
            .ToDictionary(t => t.Item1, t => t.Item2);
    }

    public IGeneratorPlugin? FindPlugin(string languageId)
        => _byId.GetValueOrDefault(languageId.ToLowerInvariant());

    public IReadOnlyList<IGeneratorPlugin> AllPlugins => _all;
}
```

### Как это заменяет CodeGeneratorFactory

```csharp
// БЫЛО (GenerateCommand.cs):
var gen = factory.CreateGenerator(generationConfig.Language, generationConfig.Options);

// СТАЛО:
var plugin = registry.FindPlugin(projectConfig.Language)
    ?? throw new NotSupportedException($"No generator found for language: '{projectConfig.Language}'");

var options = plugin.OptionsDescriptor.ParseOptions(rawOptions, out var errors);
var context = new GenerationContext { IrDocument = ir, Configuration = config, PluginOptions = options };
var pipeline = plugin.CreatePipeline(context, services);
var result = await pipeline.ExecuteAsync(context, cancellationToken);
```

---

## 7. Рефакторинг ConfigurationLoader

### Целевой ConfigurationLoader (без знания о языках)

```csharp
// ValIO.Configuration/Services/ConfigurationLoader.cs — после рефакторинга

public class ConfigurationLoader : IConfigurationLoader
{
    private readonly ILogger<ConfigurationLoader> _logger;
    private readonly IGeneratorRegistry _registry;      // NEW: знает о плагинах

    public ConfigurationLoader(
        ILogger<ConfigurationLoader> logger,
        IGeneratorRegistry registry)
    {
        _logger = logger;
        _registry = registry;
    }

    public GenerationConfiguration MapToGenerationConfiguration(ProjectConfig projectConfig)
    {
        // 1. Определяем язык через реестр (не enum!)
        var plugin = _registry.FindPlugin(projectConfig.Language)
            ?? throw new ArgumentException($"Unknown language: '{projectConfig.Language}'");

        // 2. Сырые опции из конфига — просто словарь, не типизированный
        var rawOptions = projectConfig.Options?.ToRawDictionary()
            ?? new Dictionary<string, object?>();

        // 3. Плагин сам парсит свои опции
        var pluginOptions = plugin.OptionsDescriptor.ParseOptions(rawOptions, out var parseErrors);
        if (parseErrors.Count > 0)
            throw new ConfigurationException(parseErrors);

        // 4. Строим GenerationConfiguration без знания о конкретных опциях
        return new GenerationConfiguration
        {
            LanguageId = plugin.LanguageId,     // строка, не enum
            OutputPath = projectConfig.OutputPath,
            Namespace = projectConfig.Namespace,
            GenerateModels = projectConfig.GenerateModels,
            GenerateClients = projectConfig.GenerateClients,
            GenerateEnums = projectConfig.GenerateModels,
            PluginOptions = pluginOptions,        // object (типизирован внутри плагина)
            FileOrganization = MapFileOrganization(projectConfig.FileOrganization),
            NamingConvention = MapNamingConventions(projectConfig.NamingConvention)
        };
    }

    // MapFileOrganization и MapNamingConventions — без изменений
    // Больше НИКАКИХ ParseCSharpOptions, ParseTypeScriptOptions, ParseCppOptions!
}
```

### Формат конфига (новый)

```json
{
  "projects": {
    "api": {
      "language": "typescript",
      "schema": "./openapi.json",
      "output": "./src/api",
      "options": {
        "typescript": {
          "modelFormat": "interfaces",
          "enumFormat": "union-type"
        },
        "transports": ["fetch", "axios"],
        "validation": "zod4",
        "queryWrapper": "tanstack-react",
        "fetch": {
          "customFetcherPath": "../lib/fetcher"
        },
        "axios": {
          "customInstancePath": "../lib/axios-instance"
        }
      }
    }
  }
}
```

### ProjectConfig — опции как словарь

```csharp
// ValIO.Configuration/Models/ProjectConfig.cs

public class ProjectConfig
{
    public string Language { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public bool GenerateModels { get; set; } = true;
    public bool GenerateClients { get; set; } = true;
    public string? Namespace { get; set; }

    // NEW: опции как словарь вместо typed ConfigOptions
    // Каждый плагин сам знает как парсить свою секцию
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Options { get; set; }

    public ConfigFileOrganization? FileOrganization { get; set; }
    public ConfigNamingConventions? NamingConvention { get; set; }
}
```

---

## 8. Рефакторинг GenerationOptions

### Удаление enum-ов из Core

```csharp
// БЫЛО (ValIO.Core/Models/):
public enum ValidationLibrary { Zod, Yup, Valli }         // УДАЛИТЬ
public enum QueryWrapperLibrary { TanStackQuery, SWR }     // УДАЛИТЬ
public enum GenerationLanguage { CSharp, TypeScript, Cpp } // УДАЛИТЬ

// СТАЛО: используем строки
// Идентификаторы определяются плагинами при регистрации:
// - "zod3", "zod4", "valibot", "typebox", "yup"
// - "tanstack-react", "tanstack-vue", "tanstack-angular", "swr"
// - "typescript", "csharp", "php", "python", "java", "kotlin", "swift", "dart"
```

### GenerationConfiguration — без языко-специфичных опций

```csharp
// ValIO.Core/Models/GenerationConfiguration.cs

public class GenerationConfiguration
{
    /// <summary>Идентификатор языка (строка, не enum)</summary>
    public required string LanguageId { get; init; }

    /// <summary>Путь для вывода файлов</summary>
    public required string OutputPath { get; init; }

    /// <summary>Namespace (для языков где применимо)</summary>
    public string? Namespace { get; init; }

    public bool GenerateModels { get; init; } = true;
    public bool GenerateClients { get; init; } = true;
    public bool GenerateEnums { get; init; } = true;

    /// <summary>
    /// Типизированные опции плагина.
    /// Тип зависит от плагина — TypeScriptGenerationOptions, CSharpGenerationOptions, etc.
    /// </summary>
    public object? PluginOptions { get; init; }

    public FileOrganization FileOrganization { get; init; } = new();
    public NamingConventions NamingConvention { get; init; } = new();
}
```

---

## 9. CLI: self-registering аргументы и help

### Текущая проблема

`GenerateCommand` содержит фиксированный набор аргументов. При добавлении нового плагина нужно редактировать CLI.

### Целевой подход: плагины регистрируют свои опции для CLI

```csharp
// ValIO/CLI/GenerateCommand.cs — после рефакторинга

public class GenerateCommand : Command
{
    public GenerateCommand(
        IServiceProvider serviceProvider,
        IGeneratorRegistry registry)
        : base("generate", "Generate code from OpenAPI schema")
    {
        // Базовые аргументы (не зависят от плагинов)
        var schemaOption = new Option<string?>("--schema", "-s") { ... };
        var outputOption = new Option<string?>("--output", "-o") { ... };
        var languageOption = new Option<string?>("--language", "-l") { ... };
        var configOption = new Option<FileInfo?>("--config", "-c") { ... };
        var projectsOption = new Option<string[]>("--projects", "-p") { ... };

        Options.Add(schemaOption);
        Options.Add(outputOption);
        Options.Add(languageOption);
        Options.Add(configOption);
        Options.Add(projectsOption);

        // Дополнительные аргументы от плагинов (опциональные overrides)
        foreach (var plugin in registry.AllPlugins)
        {
            foreach (var optDesc in plugin.OptionsDescriptor.GetOptionDescriptions())
            {
                var cliOption = new Option<string?>($"--{plugin.LanguageId}-{optDesc.Name}")
                {
                    Description = $"[{plugin.DisplayName}] {optDesc.Description}"
                };
                Options.Add(cliOption);
            }
        }

        // ...
    }
}
```

### IGeneratorOptionsDescriptor.GetOptionDescriptions() — алгоритм

```
GetOptionDescriptions():
    Для каждой опции плагина вернуть OptionDescription:
    - Name: имя опции в camelCase (например, "modelFormat", "enumFormat")
    - Description: описание опции на английском
    - DefaultValue: значение по умолчанию как строка
    - ValidValues: допустимые значения (для enum-like опций)
    - IsRequired: обязательна ли опция
```

---

## 10. Примеры реализации плагинов

### TypeScript плагин

```csharp
// ValIO.Generators.TypeScript/TypeScriptGeneratorPlugin.cs

public sealed class TypeScriptGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "typescript";
    public string DisplayName => "TypeScript";
    public string Description => "Generate TypeScript clients from OpenAPI schema";
    public IReadOnlyList<string> LanguageAliases => ["ts", "typescript", "TypeScript"];

    public IGeneratorOptionsDescriptor OptionsDescriptor 
        => new TypeScriptOptionsDescriptor();

    public IReadOnlyList<ITransportDescriptor> SupportedTransports =>
    [
        new TransportDescriptor("fetch",            "Fetch API",           ["fetch", "native-fetch"]),
        new TransportDescriptor("axios",            "Axios",               ["axios"]),
        new TransportDescriptor("ky",               "Ky",                  ["ky"]),
        new TransportDescriptor("undici",           "Undici",              ["undici"]),
        new TransportDescriptor("ofetch",           "Ofetch",              ["ofetch"]),
        new TransportDescriptor("got",              "Got",                 ["got"]),
        new TransportDescriptor("angular-rxjs",     "Angular HttpClient (RxJS)",    ["angular-rxjs"]),
        new TransportDescriptor("angular-promise",  "Angular HttpClient (Promise)", ["angular-promise"]),
        new TransportDescriptor("solid",            "SolidStart",          ["solid", "solidstart"]),
    ];

    public ICodeGenerationPipeline CreatePipeline(
        GenerationContext context,
        IServiceProvider services)
    {
        var options = (TypeScriptGenerationOptions)context.PluginOptions!;
        
        var steps = new List<ICodeGenerationStep>();
        
        // Модели всегда генерируются
        if (context.Configuration.GenerateModels)
            steps.Add(services.GetRequiredService<TypeScriptModelsStep>());
        
        // Enum'ы
        if (context.Configuration.GenerateEnums)
            steps.Add(services.GetRequiredService<TypeScriptEnumsStep>());
        
        // Транспорты
        foreach (var transport in options.Transports)
        {
            var step = transport switch
            {
                "fetch"           => services.GetRequiredService<FetchClientStep>(),
                "axios"           => services.GetRequiredService<AxiosClientStep>(),
                "ky"              => services.GetRequiredService<KyClientStep>(),
                "undici"          => services.GetRequiredService<UndiciClientStep>(),
                "ofetch"          => services.GetRequiredService<OfetchClientStep>(),
                "got"             => services.GetRequiredService<GotClientStep>(),
                "angular-rxjs"    => services.GetRequiredService<AngularRxjsClientStep>(),
                "angular-promise" => services.GetRequiredService<AngularPromiseClientStep>(),
                "solid"           => services.GetRequiredService<SolidStartClientStep>(),
                _ => throw new NotSupportedException($"Transport: {transport}")
            };
            steps.Add(step);
        }
        
        // Validation schemas
        if (options.ValidationLibrary != null)
        {
            var validationStep = options.ValidationLibrary switch
            {
                "zod3"     => services.GetRequiredService<Zod3SchemasStep>(),
                "zod4"     => services.GetRequiredService<Zod4SchemasStep>(),
                "valibot"  => services.GetRequiredService<ValibotSchemasStep>(),
                "typebox"  => services.GetRequiredService<TypeBoxSchemasStep>(),
                _ => throw new NotSupportedException($"Validation: {options.ValidationLibrary}")
            };
            steps.Add(validationStep);
        }
        
        // TanStack Query
        if (options.QueryWrapper != null)
        {
            var queryStep = options.QueryWrapper switch
            {
                "tanstack-react"   => services.GetRequiredService<TanStackReactStep>(),
                "tanstack-vue"     => services.GetRequiredService<TanStackVueStep>(),
                "tanstack-angular" => services.GetRequiredService<TanStackAngularStep>(),
                "tanstack-solid"   => services.GetRequiredService<TanStackSolidStep>(),
                "tanstack-svelte"  => services.GetRequiredService<TanStackSvelteStep>(),
                _ => throw new NotSupportedException($"QueryWrapper: {options.QueryWrapper}")
            };
            steps.Add(queryStep);
        }
        
        // MSW
        if (options.GenerateMsw)
            steps.Add(services.GetRequiredService<MswStep>());
        
        return new CodeGenerationPipeline(steps, ...);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Регистрация всех TS сервисов
        services.AddSingleton<TypeScriptTypeMapper>();
        services.AddSingleton<TypeScriptAstBuilder>();
        services.AddSingleton<TypeScriptAstRenderer>();
        
        // Шаги
        services.AddTransient<TypeScriptModelsStep>();
        services.AddTransient<TypeScriptEnumsStep>();
        services.AddTransient<FetchClientStep>();
        services.AddTransient<AxiosClientStep>();
        // ... и т.д.
    }
}
```

### TypeScriptOptionsDescriptor — самостоятельный парсинг

```csharp
// ValIO.Generators.TypeScript/Options/TypeScriptOptionsDescriptor.cs

public sealed class TypeScriptOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public string ConfigSectionKey => "typescript";

    public object? ParseOptions(
        IReadOnlyDictionary<string, object?> rawOptions,
        out IReadOnlyList<string> errors)
    {
        var errs = new List<string>();
        var opts = new TypeScriptGenerationOptions();

        // --- modelFormat ---
        if (rawOptions.TryGetValue("modelFormat", out var mf) && mf is string mfStr)
        {
            opts.ModelFormat = mfStr.ToLowerInvariant() switch
            {
                "interfaces"          => TypeScriptModelFormat.Interfaces,
                "validationschemas"
                or "validation-schemas" => TypeScriptModelFormat.ValidationSchemas,
                _ => (TypeScriptModelFormat?)null
                         ?? AddError(errs, $"Unknown modelFormat: '{mfStr}'", TypeScriptModelFormat.Interfaces)
            };
        }

        // --- transports ---
        if (rawOptions.TryGetValue("transports", out var tr))
        {
            var transportList = ParseStringOrArray(tr);
            foreach (var t in transportList)
            {
                if (!KnownTransports.Contains(t.ToLowerInvariant()))
                    errs.Add($"Unknown transport: '{t}'. Valid: {string.Join(", ", KnownTransports)}");
            }
            opts.Transports = transportList.Select(t => t.ToLowerInvariant()).ToList();
        }

        // --- validation ---
        if (rawOptions.TryGetValue("validation", out var vld) && vld is string vldStr)
        {
            opts.ValidationLibrary = vldStr.ToLowerInvariant() switch
            {
                "zod" or "zod3" => "zod3",
                "zod4"          => "zod4",
                "valibot"       => "valibot",
                "typebox"       => "typebox",
                "yup"           => "yup",
                _               => AddError(errs, $"Unknown validation: '{vldStr}'", (string?)null)
            };
        }

        // --- queryWrapper ---
        if (rawOptions.TryGetValue("queryWrapper", out var qw) && qw is string qwStr)
        {
            opts.QueryWrapper = qwStr.ToLowerInvariant() switch
            {
                "tanstack" or "tanstack-react" or "react-query" => "tanstack-react",
                "tanstack-vue"                                   => "tanstack-vue",
                "tanstack-angular"                               => "tanstack-angular",
                "tanstack-solid"                                 => "tanstack-solid",
                "tanstack-svelte"                                => "tanstack-svelte",
                "swr"                                            => "swr",
                _                                                => AddError(errs, $"Unknown queryWrapper: '{qwStr}'", (string?)null)
            };
        }

        // --- msw ---
        if (rawOptions.TryGetValue("msw", out var msw) && msw is bool mswBool)
            opts.GenerateMsw = mswBool;

        // ... остальные опции

        errors = errs;
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options, GenerationConfiguration config)
    {
        var opts = (TypeScriptGenerationOptions)options;
        var errors = new List<string>();

        if (opts.ValidationLibrary != null && opts.ModelFormat != TypeScriptModelFormat.ValidationSchemas)
            errors.Add("Validation library specified but modelFormat is not 'validationSchemas'");

        return errors;
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new("modelFormat",  "Format of generated models",    "interfaces",     ["interfaces", "validationSchemas"], false),
        new("transports",   "HTTP transports to generate",   "fetch",          KnownTransports, false),
        new("validation",   "Validation library",            null,             ["zod3", "zod4", "valibot", "typebox"], false),
        new("queryWrapper", "Query wrapper library",         null,             ["tanstack-react", "tanstack-vue", ...], false),
        new("msw",          "Generate MSW mocks",            "false",          ["true", "false"], false),
    ];

    private static readonly string[] KnownTransports =
        ["fetch", "axios", "ky", "undici", "ofetch", "got", "angular-rxjs", "angular-promise", "solid"];
}
```

---

## 11. DI регистрация

```csharp
// ValIO/ServiceRegistration/ServiceCollectionExtensions.cs

public static IServiceCollection AddValIOCore(this IServiceCollection services)
{
    // Ядро
    services.AddSingleton<IGeneratorRegistry, GeneratorRegistry>();
    services.AddSingleton<IConfigurationLoader, ConfigurationLoader>();
    services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
    services.AddSingleton<IOpenApiDocumentParser, OpenApiDocumentParser>();
    services.AddSingleton<IFileWriter, FileWriter>();
    services.AddSingleton<INamingConverter, NamingConverter>();
    services.AddSingleton<IrBuilder>();
    
    return services;
}

public static IServiceCollection AddGeneratorPlugin<TPlugin>(
    this IServiceCollection services)
    where TPlugin : class, IGeneratorPlugin, new()
{
    var plugin = new TPlugin();
    plugin.ConfigureServices(services);              // плагин регистрирует свои сервисы
    services.AddSingleton<IGeneratorPlugin>(plugin); // регистрируем в реестре
    return services;
}

// ValIO/Program.cs
var services = new ServiceCollection();
services.AddValIOCore();

// Регистрация плагинов — всё что нужно для работы CLI
services.AddGeneratorPlugin<TypeScriptGeneratorPlugin>();
services.AddGeneratorPlugin<CSharpGeneratorPlugin>();
services.AddGeneratorPlugin<CppGeneratorPlugin>();

// Новые языки добавляются одной строкой:
// services.AddGeneratorPlugin<PhpGeneratorPlugin>();
// services.AddGeneratorPlugin<PythonGeneratorPlugin>();
// services.AddGeneratorPlugin<JavaGeneratorPlugin>();
```

---

## 12. Карта файлов и изменений

### Файлы к удалению

| Файл | Причина |
|------|---------|
| `ValIO.Core/Models/GenerationLanguage.cs` | Заменяется строками |
| `ValIO.Core/Models/ValidationLibrary.cs` | Заменяется строками |
| `ValIO.Core/Models/QueryWrapperLibrary.cs` | Заменяется строками |
| `ValIO.Core/Models/GenerationOptions.cs` | Заменяется `PluginOptions` в `GenerationConfiguration` |
| `ValIO/Factories/CodeGeneratorFactory.cs` | Заменяется `GeneratorRegistry` |
| `ValIO/Factories/TypeMapperFactory.cs` | Заменяется DI |
| `ValIO.Configuration/Models/ConfigOptions.cs` | Заменяется `Dictionary<string, object?>` |
| `ValIO.Configuration/Models/ConfigCSharpOptions.cs` | Заменяется парсером плагина |
| `ValIO.Configuration/Models/ConfigTypeScriptOptions.cs` | Заменяется парсером плагина |
| `ValIO.Configuration/Models/ConfigCppOptions.cs` | Заменяется парсером плагина |

### Файлы к созданию (Core)

| Файл | Назначение |
|------|-----------|
| `ValIO.Core/Plugins/IGeneratorPlugin.cs` | Контракт плагина |
| `ValIO.Core/Plugins/IGeneratorOptionsDescriptor.cs` | Контракт парсера опций |
| `ValIO.Core/Plugins/ICodeGenerationPipeline.cs` | Контракт pipeline |
| `ValIO.Core/Plugins/ICodeGenerationStep.cs` | Контракт шага |
| `ValIO.Core/Plugins/CodeGenerationPipeline.cs` | Реализация pipeline |
| `ValIO.Core/Plugins/IGeneratorRegistry.cs` | Контракт реестра |
| `ValIO.Core/Plugins/GeneratorRegistry.cs` | Реализация реестра |
| `ValIO.Core/Plugins/GenerationContext.cs` | Контекст генерации |
| `ValIO.Core/Plugins/ITransportDescriptor.cs` | Описание транспорта |
| `ValIO.Core/Plugins/OptionDescription.cs` | Описание опции |

### Файлы к изменению

| Файл | Изменение |
|------|-----------|
| `ValIO.Core/Models/GenerationConfiguration.cs` | Убрать `Language` (enum), добавить `LanguageId` (string) + `PluginOptions` |
| `ValIO.Configuration/Models/ProjectConfig.cs` | `Options` → `Dictionary<string, object?>` |
| `ValIO.Configuration/Services/ConfigurationLoader.cs` | Убрать всё языко-специфичное |
| `ValIO/ServiceRegistration/ServiceCollectionExtensions.cs` | Переход на plugin registration |
| `ValIO/CLI/GenerateCommand.cs` | Использовать `IGeneratorRegistry` вместо factory |
| `ValIO/Program.cs` | Регистрация плагинов |

### Новые файлы в каждом генераторе

| Файл | Назначение |
|------|-----------|
| `ValIO.Generators.TypeScript/TypeScriptGeneratorPlugin.cs` | Plugin impl |
| `ValIO.Generators.TypeScript/Options/TypeScriptOptionsDescriptor.cs` | Options parsing |
| `ValIO.Generators.TypeScript/Steps/TypeScriptModelsStep.cs` | Models step |
| `ValIO.Generators.TypeScript/Steps/TypeScriptEnumsStep.cs` | Enums step |
| `ValIO.Generators.TypeScript/Steps/FetchClientStep.cs` | Fetch step |
| `ValIO.Generators.TypeScript/Steps/AxiosClientStep.cs` | Axios step |
| (аналогично для CSharp, Cpp) | |

---

## Итог

После рефакторинга:
- ❌ `GenerationLanguage` enum — удалён
- ❌ `ValidationLibrary` enum — удалён  
- ❌ `QueryWrapperLibrary` enum — удалён
- ❌ `CodeGeneratorFactory` switch — удалён
- ❌ `ConfigurationLoader` с языко-специфичным парсингом — удалён
- ✅ `IGeneratorPlugin` — каждый генератор регистрирует себя
- ✅ `GeneratorRegistry` — resolver без switch
- ✅ `IGeneratorOptionsDescriptor` — плагин сам парсит свои опции
- ✅ `ICodeGenerationPipeline` + `ICodeGenerationStep` — декомпозиция God Object
- ✅ Добавление нового языка = один новый проект + `services.AddGeneratorPlugin<>()`
