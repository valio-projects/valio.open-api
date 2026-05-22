using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Cli.Init;
using ValIO.Configuration.Models;
using ValIO.Core.Plugins;

namespace ValIO.Cli.Commands;

/// <summary>
/// `valio init` — generate a new valio.config.json file.
///
/// Two modes:
///   1. **Interactive** (no args, or --interactive): walks user through language, schema,
///      output path, and per-plugin options.
///   2. **Non-interactive** (any of --language, --project-name, --schema, --output-dir,
///      --output, --minimal): generates a config straight from the args with sensible
///      defaults derived from the plugin's OptionsDescriptor.
/// </summary>
public sealed class InitCommand : Command
{
    public InitCommand(IServiceProvider serviceProvider)
        : base("init", "Initialize a new valio.config.json file")
    {
        var languageOption = new Option<string?>("--language", "-l")
        {
            Description = "Target language id (e.g. typescript, csharp). Default: typescript."
        };
        var projectNameOption = new Option<string?>("--project-name")
        {
            Description = "Project name used as the key in the projects map. Default: MyApi."
        };
        var schemaOption = new Option<string?>("--schema", "-s")
        {
            Description = "OpenAPI schema URL or path placeholder. Default: ./openapi.yaml."
        };
        var outputDirOption = new Option<string?>("--output-dir")
        {
            Description = "Output directory for generated code. Default: ./out."
        };
        var configPathOption = new Option<string?>("--output", "-o")
        {
            Description = "Where to write the config file. Default: ./valio.config.json."
        };
        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing config file without prompting."
        };
        var minimalOption = new Option<bool>("--minimal")
        {
            Description = "Emit only essential keys (skip fileOrganization/namingConvention defaults)."
        };
        var interactiveOption = new Option<bool>("--interactive", "-i")
        {
            Description = "Force interactive mode even when other options are supplied."
        };

        Options.Add(languageOption);
        Options.Add(projectNameOption);
        Options.Add(schemaOption);
        Options.Add(outputDirOption);
        Options.Add(configPathOption);
        Options.Add(forceOption);
        Options.Add(minimalOption);
        Options.Add(interactiveOption);

        SetAction(async (ParseResult parseResult) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<InitCommand>>();
            var registry = serviceProvider.GetRequiredService<IGeneratorRegistry>();
            var prompter = serviceProvider.GetService<IInitPrompter>() ?? new ConsoleInitPrompter();

            var language = parseResult.GetValue(languageOption);
            var projectName = parseResult.GetValue(projectNameOption);
            var schema = parseResult.GetValue(schemaOption);
            var outputDir = parseResult.GetValue(outputDirOption);
            var configPath = parseResult.GetValue(configPathOption);
            var force = parseResult.GetValue(forceOption);
            var minimal = parseResult.GetValue(minimalOption);
            var interactive = parseResult.GetValue(interactiveOption);

            var hasAnyArgs = language is not null || projectName is not null || schema is not null
                          || outputDir is not null || configPath is not null || minimal;
            var isInteractive = interactive || !hasAnyArgs;

            try
            {
                if (isInteractive)
                {
                    var result = RunInteractive(prompter, registry);
                    return await WriteConfigAsync(result.Config, result.ConfigPath, force, prompter, logger);
                }

                var nonInteractiveConfig = BuildNonInteractive(
                    language ?? "typescript",
                    projectName ?? "MyApi",
                    schema ?? "./openapi.yaml",
                    outputDir ?? "./out",
                    registry,
                    minimal);

                var path = configPath ?? "./valio.config.json";
                return await WriteConfigAsync(nonInteractiveConfig, path, force, prompter, logger);
            }
            catch (ArgumentException ex)
            {
                logger.LogError("{Message}", ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize config");
                return 1;
            }
        });
    }

    private sealed record InteractiveResult(ValIOConfig Config, string ConfigPath);

    private static InteractiveResult RunInteractive(IInitPrompter prompter, IGeneratorRegistry registry)
    {
        prompter.WriteLine(string.Empty);
        prompter.WriteLine("valio init — interactive configuration");
        prompter.WriteLine(string.Empty);

        var projectName = prompter.AskString("Project name", "MyApi");

        var plugins = registry.AllPlugins
            .OrderBy(p => p.LanguageId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (plugins.Count == 0)
            throw new InvalidOperationException("No generator plugins registered.");

        var langChoices = plugins.Select(p => $"{p.LanguageId} — {p.DisplayName}").ToList();
        var defaultLangIdx = Math.Max(0, plugins.FindIndex(p =>
            p.LanguageId.Equals("typescript", StringComparison.OrdinalIgnoreCase)));
        var langIdx = prompter.AskChoice("Target language", langChoices, defaultLangIdx);
        var plugin = plugins[langIdx];

        var schema = prompter.AskString("OpenAPI schema URL or path", "./openapi.yaml");
        var outputPath = prompter.AskString("Output directory", "./out");
        var generateModels = prompter.AskBool("Generate models", true);
        var generateClients = prompter.AskBool("Generate clients", true);

        var options = new Dictionary<string, JsonElement>();
        var descriptions = plugin.OptionsDescriptor.GetOptionDescriptions();
        if (descriptions.Count > 0)
        {
            prompter.WriteLine(string.Empty);
            prompter.WriteLine($"-- {plugin.DisplayName} options --");
            foreach (var opt in descriptions)
            {
                var value = PromptForOption(prompter, opt);
                if (value.HasValue) options[opt.Name] = value.Value;
            }
        }

        var configPath = prompter.AskString("Save config to", "./valio.config.json");

        var projectConfig = new ProjectConfig
        {
            Language = plugin.LanguageId,
            Schema = schema,
            OutputPath = outputPath,
            GenerateModels = generateModels,
            GenerateClients = generateClients,
            Options = options.Count > 0 ? options : null,
            FileOrganization = DefaultFileOrganization(),
            NamingConvention = DefaultNamingConventions(plugin.LanguageId)
        };

        var config = new ValIOConfig
        {
            Projects = new Dictionary<string, ProjectConfig> { [projectName] = projectConfig }
        };

        return new InteractiveResult(config, configPath);
    }

    private static JsonElement? PromptForOption(IInitPrompter prompter, OptionDescription opt)
    {
        var label = string.IsNullOrEmpty(opt.Description) ? opt.Name : $"{opt.Name} — {opt.Description}";

        switch (opt.Kind)
        {
            case OptionKind.Bool:
            {
                var def = bool.TryParse(opt.DefaultValue, out var parsed) && parsed;
                var val = prompter.AskBool(label, def);
                return ToJsonElement(val);
            }

            case OptionKind.Int:
            {
                var raw = prompter.AskString(label, opt.DefaultValue);
                if (string.IsNullOrEmpty(raw)) return null;
                if (int.TryParse(raw, out var n)) return ToJsonElement(n);
                return null;
            }

            case OptionKind.EnumSingle:
            {
                if (opt.Choices is null || opt.Choices.Count == 0) return null;
                var choices = opt.Choices.Select(c => $"{c.Id} — {c.DisplayName}").ToList();
                var defIdx = opt.Choices.ToList().FindIndex(c =>
                    string.Equals(c.Id, opt.DefaultValue, StringComparison.OrdinalIgnoreCase));
                if (defIdx < 0) defIdx = 0;
                var idx = prompter.AskChoice(label, choices, defIdx);
                return ToJsonElement(opt.Choices[idx].Id);
            }

            case OptionKind.EnumMulti:
            {
                if (opt.Choices is null || opt.Choices.Count == 0) return null;
                var choices = opt.Choices.Select(c => $"{c.Id} — {c.DisplayName}").ToList();
                var indices = prompter.AskMultiChoice(label, choices, defaultIndices: null);
                if (indices.Count == 0) return null;
                var picked = indices.Select(i => opt.Choices[i].Id).ToList();
                return ToJsonElement(picked);
            }

            case OptionKind.String:
            default:
            {
                var s = prompter.AskString(label, opt.DefaultValue);
                if (string.IsNullOrEmpty(s)) return null;
                return ToJsonElement(s);
            }
        }
    }

    private static ValIOConfig BuildNonInteractive(
        string language,
        string projectName,
        string schema,
        string outputDir,
        IGeneratorRegistry registry,
        bool minimal)
    {
        var plugin = registry.FindPlugin(language)
            ?? throw new ArgumentException(
                $"Unknown language '{language}'. Available: " +
                string.Join(", ", registry.AllPlugins.Select(p => p.LanguageId)));

        var projectConfig = new ProjectConfig
        {
            Language = plugin.LanguageId,
            Schema = schema,
            OutputPath = outputDir,
            GenerateModels = true,
            GenerateClients = true,
        };

        if (!minimal)
        {
            projectConfig.FileOrganization = DefaultFileOrganization();
            projectConfig.NamingConvention = DefaultNamingConventions(plugin.LanguageId);

            var opts = new Dictionary<string, JsonElement>();
            foreach (var d in plugin.OptionsDescriptor.GetOptionDescriptions())
            {
                if (d.DefaultValue is null) continue;
                switch (d.Kind)
                {
                    case OptionKind.Bool:
                        if (bool.TryParse(d.DefaultValue, out var b)) opts[d.Name] = ToJsonElement(b);
                        break;
                    case OptionKind.Int:
                        if (int.TryParse(d.DefaultValue, out var n)) opts[d.Name] = ToJsonElement(n);
                        break;
                    case OptionKind.String:
                    case OptionKind.EnumSingle:
                        opts[d.Name] = ToJsonElement(d.DefaultValue);
                        break;
                    case OptionKind.EnumMulti:
                        // EnumMulti defaults aren't expressible in a single string — skip.
                        break;
                }
            }
            if (opts.Count > 0) projectConfig.Options = opts;
        }

        return new ValIOConfig
        {
            Projects = new Dictionary<string, ProjectConfig> { [projectName] = projectConfig }
        };
    }

    private static ConfigFileOrganization DefaultFileOrganization() => new()
    {
        GroupingStrategy = "SingleFile",
        SeparateModels = true,
        SeparateClients = true,
        ModelsPath = "models",
        ClientsPath = "api"
    };

    private static ConfigNamingConventions DefaultNamingConventions(string languageId)
    {
        var snake = languageId is "python" or "ruby" or "php";
        var snakeMethods = languageId is "python" or "ruby";
        return new ConfigNamingConventions
        {
            Models = "PascalCase",
            Properties = snake ? "snake_case" : "camelCase",
            Methods = snakeMethods ? "snake_case" : "camelCase",
            Enums = "PascalCase"
        };
    }

    private static async Task<int> WriteConfigAsync(
        ValIOConfig config,
        string path,
        bool force,
        IInitPrompter prompter,
        ILogger logger)
    {
        if (File.Exists(path) && !force)
        {
            var overwrite = prompter.AskBool($"File {path} already exists. Overwrite", false);
            if (!overwrite)
            {
                prompter.WriteLine("Aborted.");
                return 2;
            }
        }

        var json = JsonSerializer.Serialize(config, SerializerOptions);
        await File.WriteAllTextAsync(path, json + Environment.NewLine);

        logger.LogInformation("Wrote {Path}", Path.GetFullPath(path));
        prompter.WriteLine(string.Empty);
        prompter.WriteLine($"Created {path}");
        prompter.WriteLine("Next steps:");
        prompter.WriteLine($"  valio generate --config {path}");
        return 0;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static JsonElement ToJsonElement<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }
}
