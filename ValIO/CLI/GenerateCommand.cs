using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Models;
using ValIO.Core.Models;
using ValIO.Factories;
using ValIO.Parsers.Interfaces;

namespace ValIO.CLI;

/// <summary>
/// Команда генерации кода
/// </summary>
public class GenerateCommand : Command
{
    public GenerateCommand(IServiceProvider serviceProvider) 
        : base("generate", "Generate code from OpenAPI schema")
    {
        // Опциональные аргументы для прямого указания параметров (без конфига)
        var schemaOption = new Option<string?>("--schema", "-s")
        {
            Description = "Path or URL to OpenAPI schema file (required if not using config)"
        };

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Output directory path (required if not using config)"
        };

        var languageOption = new Option<GenerationLanguage?>("--language", "-l")
        {
            Description = "Target programming language (required if not using config)"
        };

        // Обязательный аргумент - путь к конфигу или название проекта
        var configOption = new Option<FileInfo?>("--config", "-c")
        {
            Description = "Path to configuration file (JSON or YAML)"
        };

        var projectsOption = new Option<string[]>("--projects", "-p")
        {
            Description = "Project name(s) from configuration file. If omitted, all projects from config are run.",
            AllowMultipleArgumentsPerToken = true
        };

        Options.Add(schemaOption);
        Options.Add(outputOption);
        Options.Add(languageOption);
        Options.Add(configOption);
        Options.Add(projectsOption);

        SetAction(async (ParseResult parseResult) =>
        {
            var schema = parseResult.GetValue(schemaOption);
            var output = parseResult.GetValue(outputOption);
            var language = parseResult.GetValue(languageOption);
            var configFile = parseResult.GetValue(configOption);
            var projectNames = parseResult.GetValue(projectsOption) ?? Array.Empty<string>();

            var logger = serviceProvider.GetRequiredService<ILogger<GenerateCommand>>();
            var parser = serviceProvider.GetRequiredService<IOpenApiDocumentParser>();
            var configLoader = serviceProvider.GetRequiredService<IConfigurationLoader>();
            var configValidator = serviceProvider.GetRequiredService<IConfigurationValidator>();
            var factory = serviceProvider.GetRequiredService<CodeGeneratorFactory>();

            try
            {
                ProjectConfig? projectConfig = null;
                GenerationConfiguration generationConfig;

                // Если указан конфиг, загружаем его
                if (configFile != null && configFile.Exists)
                {
                    var config = await configLoader.LoadConfigFromFileAsync(configFile.FullName);
                    logger.LogInformation("Configuration loaded from: {ConfigPath}", configFile.FullName);

                    if (config.Projects.Count == 0)
                    {
                        logger.LogError("Configuration file contains no projects");
                        return 1;
                    }

                    // Определяем список проектов для генерации
                    var projectsToRun = projectNames.Length > 0
                        ? projectNames.Where(name => config.Projects.ContainsKey(name)).ToList()
                        : config.Projects.Keys.ToList();

                    var notFound = projectNames.Where(name => !config.Projects.ContainsKey(name)).ToList();
                    if (notFound.Count > 0)
                    {
                        logger.LogError("Project(s) not found: {Projects}. Available: {Available}",
                            string.Join(", ", notFound), string.Join(", ", config.Projects.Keys));
                        return 1;
                    }

                    var totalFiles = 0;
                    var hasErrors = false;

                    foreach (var projName in projectsToRun)
                    {
                        projectConfig = config.Projects[projName];
                        logger.LogInformation("Using project: {ProjectName}", projName);

                        var projSchema = string.IsNullOrWhiteSpace(schema) ? projectConfig.Schema : schema;
                        var projOutput = string.IsNullOrWhiteSpace(output) ? projectConfig.OutputPath : output;

                        generationConfig = configLoader.MapToGenerationConfiguration(projectConfig);
                        generationConfig.OutputPath = projOutput;

                        var projValidation = configValidator.Validate(generationConfig);
                        if (!projValidation.IsValid)
                        {
                            logger.LogError("Configuration validation failed for {Project}: {Errors}",
                                projName, string.Join(", ", projValidation.Errors));
                            hasErrors = true;
                            continue;
                        }

                        foreach (var warning in projValidation.Warnings)
                            logger.LogWarning(warning);

                        logger.LogInformation("Parsing OpenAPI schema from: {Schema}", projSchema);
                        OpenApiDocument doc;
                        if (Uri.TryCreate(projSchema, UriKind.Absolute, out var schemaUri) &&
                            (schemaUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                             schemaUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                             schemaUri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase)))
                        {
                            doc = await parser.ParseFromUrlAsync(projSchema);
                        }
                        else
                        {
                            doc = await parser.ParseFromFileAsync(projSchema);
                        }

                        logger.LogInformation("OpenAPI document parsed successfully for {Project}", projName);
                        var gen = factory.CreateGenerator(generationConfig.Language, generationConfig.Options);
                        logger.LogInformation("Using generator for language: {Language}", generationConfig.Language);
                        logger.LogInformation("Generating code to: {Output}", projOutput);
                        var genResult = await gen.GenerateAsync(doc, generationConfig);

                        if (genResult.Success)
                        {
                            totalFiles += genResult.GeneratedFiles.Count;
                            logger.LogInformation("Code generation completed for {Project}. Generated {FileCount} files",
                                projName, genResult.GeneratedFiles.Count);
                            foreach (var w in genResult.Warnings)
                                logger.LogWarning(w);
                        }
                        else
                        {
                            logger.LogError("Code generation failed for {Project}: {Errors}",
                                projName, string.Join(", ", genResult.Errors));
                            hasErrors = true;
                        }
                    }

                    if (hasErrors)
                        return 1;
                    logger.LogInformation("Code generation completed successfully. Total generated {FileCount} files", totalFiles);
                    return 0;
                }
                else
                {
                    // Работа без конфига - все параметры обязательны
                    if (string.IsNullOrWhiteSpace(schema))
                    {
                        logger.LogError("Schema path or URL is required (use --schema or --config)");
                        return 1;
                    }

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        logger.LogError("Output directory path is required (use --output or --config)");
                        return 1;
                    }

                    if (!language.HasValue)
                    {
                        logger.LogError("Language is required (use --language or --config)");
                        return 1;
                    }

                    // Создание конфигурации по умолчанию
                    generationConfig = new GenerationConfiguration
                    {
                        Language = language.Value,
                        OutputPath = output,
                        GenerateModels = true,
                        GenerateClients = true,
                        GenerateEnums = true
                    };
                    logger.LogInformation("Using default configuration");
                }

                // Переопределение output path при указании -o
                if (!string.IsNullOrWhiteSpace(output))
                {
                    generationConfig.OutputPath = output;
                }

                // Валидация конфигурации
                var validationResult = configValidator.Validate(generationConfig);
                if (!validationResult.IsValid)
                {
                    logger.LogError("Configuration validation failed: {Errors}", 
                        string.Join(", ", validationResult.Errors));
                    return 1;
                }

                if (validationResult.Warnings.Any())
                {
                    foreach (var warning in validationResult.Warnings)
                    {
                        logger.LogWarning(warning);
                    }
                }

                // Парсинг OpenAPI документа
                logger.LogInformation("Parsing OpenAPI schema from: {Schema}", schema);
                
                OpenApiDocument document;
                // Проверяем, является ли путь URL (http/https) или локальным файлом
                if (Uri.TryCreate(schema, UriKind.Absolute, out var uri) && 
                    (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || 
                     uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                     uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase)))
                {
                    document = await parser.ParseFromUrlAsync(schema);
                }
                else
                {
                    // Локальный файл
                    document = await parser.ParseFromFileAsync(schema);
                }

                logger.LogInformation("OpenAPI document parsed successfully");

                // Создание генератора
                var generator = factory.CreateGenerator(generationConfig.Language, generationConfig.Options);
                logger.LogInformation("Using generator for language: {Language}", generationConfig.Language);

                // Генерация кода
                logger.LogInformation("Generating code to: {Output}", output);
                var result = await generator.GenerateAsync(document, generationConfig);

                if (result.Success)
                {
                    logger.LogInformation("Code generation completed successfully. Generated {FileCount} files", 
                        result.GeneratedFiles.Count);
                    
                    if (result.Warnings.Any())
                    {
                        foreach (var warning in result.Warnings)
                        {
                            logger.LogWarning(warning);
                        }
                    }

                    return 0;
                }
                else
                {
                    logger.LogError("Code generation failed: {Errors}", 
                        string.Join(", ", result.Errors));
                    return 1;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during code generation");
                return 1;
            }
        });
    }
}
