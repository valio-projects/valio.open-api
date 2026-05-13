using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Models;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
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
        var schemaOption = new Option<string?>("--schema", "-s")
        {
            Description = "Path or URL to OpenAPI schema file (required if not using config)"
        };

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Output directory path (required if not using config)"
        };

        var languageOption = new Option<string?>("--language", "-l")
        {
            Description = "Target programming language id (e.g. typescript, csharp)"
        };

        var configOption = new Option<FileInfo?>("--config", "-c")
        {
            Description = "Path to configuration file (JSON or YAML)"
        };

        var projectsOption = new Option<string[]>("--projects", "-p")
        {
            Description = "Project name(s) from configuration file.",
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
            var languageId = parseResult.GetValue(languageOption);
            var configFile = parseResult.GetValue(configOption);
            var projectNames = parseResult.GetValue(projectsOption) ?? Array.Empty<string>();

            var logger = serviceProvider.GetRequiredService<ILogger<GenerateCommand>>();
            var parser = serviceProvider.GetRequiredService<IOpenApiDocumentParser>();
            var configLoader = serviceProvider.GetRequiredService<IConfigurationLoader>();
            var configValidator = serviceProvider.GetRequiredService<IConfigurationValidator>();
            var registry = serviceProvider.GetRequiredService<IGeneratorRegistry>();

            try
            {
                GenerationConfiguration generationConfig;

                if (configFile != null && configFile.Exists)
                {
                    var config = await configLoader.LoadConfigFromFileAsync(configFile.FullName);
                    logger.LogInformation("Configuration loaded from: {ConfigPath}", configFile.FullName);

                    if (config.Projects.Count == 0)
                    {
                        logger.LogError("Configuration file contains no projects");
                        return 1;
                    }

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

                    var hasErrors = false;
                    foreach (var projName in projectsToRun)
                    {
                        var projectConfig = config.Projects[projName];
                        logger.LogInformation("Using project: {ProjectName}", projName);

                        var projSchema = string.IsNullOrWhiteSpace(schema) ? projectConfig.Schema : schema;
                        var projOutput = string.IsNullOrWhiteSpace(output) ? projectConfig.OutputPath : output;

                        generationConfig = configLoader.MapToGenerationConfiguration(projectConfig);
                        generationConfig.OutputPath = projOutput;

                        var result = await RunGenerationAsync(generationConfig, projSchema, logger, parser, registry);
                        if (!result)
                            hasErrors = true;
                    }

                    return hasErrors ? 1 : 0;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(languageId))
                    {
                        logger.LogError("--schema, --output, and --language are required when not using --config");
                        return 1;
                    }

                    generationConfig = new GenerationConfiguration
                    {
                        LanguageId = languageId,
                        OutputPath = output,
                        GenerateModels = true,
                        GenerateClients = true,
                        GenerateEnums = true
                    };
                }

                var success = await RunGenerationAsync(generationConfig, schema!, logger, parser, registry);
                return success ? 0 : 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during code generation");
                return 1;
            }
        });
    }

    private static async Task<bool> RunGenerationAsync(
        GenerationConfiguration config,
        string schemaPath,
        ILogger logger,
        IOpenApiDocumentParser parser,
        IGeneratorRegistry registry)
    {
        var plugin = registry.FindPlugin(config.LanguageId);
        if (plugin == null)
        {
            logger.LogError("No generator found for language: {LanguageId}", config.LanguageId);
            return false;
        }

        logger.LogInformation("Using generator: {DisplayName}", plugin.DisplayName);
        logger.LogInformation("Parsing OpenAPI schema from: {Schema}", schemaPath);

        OpenApiDocument doc;
        if (Uri.TryCreate(schemaPath, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase)))
        {
            doc = await parser.ParseFromUrlAsync(schemaPath);
        }
        else
        {
            doc = await parser.ParseFromFileAsync(schemaPath);
        }

        logger.LogInformation("OpenAPI document parsed successfully");

        // TODO Phase 1: wire IrBuilder + plugin.CreatePipeline() + pipeline.ExecuteAsync()
        logger.LogWarning("Code generation pipeline not yet implemented (Phase 1)");
        return true;
    }
}
