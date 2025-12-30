using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Configuration.Interfaces;
using ValIO.Core.Factories;
using ValIO.Core.Models;
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
        var schemaOption = new Option<string>(
            aliases: new[] { "--schema", "-s" },
            description: "Path or URL to OpenAPI schema file")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output directory path")
        {
            IsRequired = true
        };

        var languageOption = new Option<GenerationLanguage>(
            aliases: new[] { "--language", "-l" },
            description: "Target programming language")
        {
            IsRequired = true
        };

        var configOption = new Option<FileInfo?>(
            aliases: new[] { "--config", "-c" },
            description: "Path to configuration file (JSON or YAML)");

        this.AddOption(schemaOption);
        this.AddOption(outputOption);
        this.AddOption(languageOption);
        this.AddOption(configOption);

        this.SetHandler(async (schema, output, language, config) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<GenerateCommand>>();
            var parser = serviceProvider.GetRequiredService<IOpenApiDocumentParser>();
            var configLoader = serviceProvider.GetRequiredService<IConfigurationLoader>();
            var configValidator = serviceProvider.GetRequiredService<IConfigurationValidator>();
            var factory = serviceProvider.GetRequiredService<CodeGeneratorFactory>();

            try
            {
                // Загрузка конфигурации
                GenerationConfiguration generationConfig;
                if (config != null && config.Exists)
                {
                    generationConfig = await configLoader.LoadFromFileAsync(config.FullName);
                    logger.LogInformation("Configuration loaded from: {ConfigPath}", config.FullName);
                }
                else
                {
                    // Создание конфигурации по умолчанию
                    generationConfig = new GenerationConfiguration
                    {
                        Language = language,
                        OutputPath = output,
                        GenerateEnums = true,
                        GenerateModels = true,
                        GenerateClients = true
                    };
                    logger.LogInformation("Using default configuration");
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
                var document = Uri.TryCreate(schema, UriKind.Absolute, out _)
                    ? await parser.ParseFromUrlAsync(schema)
                    : await parser.ParseFromFileAsync(schema);

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
        }, schemaOption, outputOption, languageOption, configOption);
    }
}

