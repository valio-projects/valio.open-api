using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Models;
using ValIO.Core.Models;
using ValIO.Services;

namespace ValIO.CLI;

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
            var configLoader = serviceProvider.GetRequiredService<IConfigurationLoader>();
            var orchestrator = serviceProvider.GetRequiredService<GenerationOrchestrator>();

            try
            {
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
                        var generationConfig = configLoader.MapToGenerationConfiguration(projectConfig);
                        if (!string.IsNullOrWhiteSpace(output))
                            generationConfig.OutputPath = output;

                        var success = await orchestrator.GenerateAsync(generationConfig, projSchema ?? "");
                        if (!success) hasErrors = true;
                    }

                    return hasErrors ? 1 : 0;
                }

                if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(languageId))
                {
                    logger.LogError("--schema, --output, and --language are required when not using --config");
                    return 1;
                }

                var quickConfig = new GenerationConfiguration
                {
                    LanguageId = languageId,
                    OutputPath = output,
                    GenerateModels = true,
                    GenerateClients = true,
                    GenerateEnums = true
                };

                return await orchestrator.GenerateAsync(quickConfig, schema) ? 0 : 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during code generation");
                return 1;
            }
        });
    }
}
