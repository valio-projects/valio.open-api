using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Python.Options;

public sealed class PythonOptionsDescriptor : IGeneratorOptionsDescriptor
{
    private static readonly OptionChoice[] HttpxModeChoices =
    [
        new("async", "httpx.AsyncClient (async)", "python.httpxMode.async.desc"),
        new("sync",  "httpx.Client (sync)",       "python.httpxMode.sync.desc"),
    ];

    private static readonly OptionChoice[] ValidatorChoices =
    [
        new("pydantic", "Pydantic v2", "python.validator.pydantic.desc"),
        new("msgspec",  "msgspec",      "python.validator.msgspec.desc"),
    ];

    private static readonly OptionChoice[] PythonVersionChoices =
    [
        new("3.10", "Python 3.10", null),
        new("3.11", "Python 3.11", null),
        new("3.12", "Python 3.12", null),
        new("3.13", "Python 3.13", null),
    ];

    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new PythonGenerationOptions();

        if (rawOptions.TryGetValue("pythonVersion", out var pyVersion) && pyVersion.ValueKind == JsonValueKind.String)
        {
            var value = pyVersion.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                opts.PythonVersion = value.Trim();
        }

        if (rawOptions.TryGetValue("httpxMode", out var mode) && mode.ValueKind == JsonValueKind.String)
        {
            var normalized = NormalizeHttpxMode(mode.GetString(), errorList);
            if (normalized != null)
                opts.HttpxMode = normalized;
        }

        if (rawOptions.TryGetValue("validator", out var validator) && validator.ValueKind == JsonValueKind.String)
        {
            var normalized = NormalizeValidator(validator.GetString(), errorList);
            if (normalized != null)
                opts.Validator = normalized;
        }

        if (rawOptions.TryGetValue("rootModule", out var rootModule) && rootModule.ValueKind == JsonValueKind.String)
        {
            var value = rootModule.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                opts.RootModule = value.Trim();
        }

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options)
    {
        if (options is not PythonGenerationOptions opts)
            return ["Invalid options type"];

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(opts.PythonVersion))
            errors.Add("pythonVersion must be set");

        if (opts.HttpxMode != PythonOptionIds.HttpxMode.Async &&
            opts.HttpxMode != PythonOptionIds.HttpxMode.Sync)
            errors.Add($"Unknown httpxMode: '{opts.HttpxMode}'");

        if (opts.Validator != PythonOptionIds.Validator.Pydantic)
            errors.Add($"Validator '{opts.Validator}' is not yet supported");

        if (string.IsNullOrWhiteSpace(opts.RootModule))
            errors.Add("rootModule must be set");

        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new(
            Name:           "pythonVersion",
            Description:    "Target Python version. Generated syntax assumes 3.10+ features.",
            DefaultValue:   "3.11",
            IsRequired:     true,
            Kind:           OptionKind.EnumSingle,
            Choices:        PythonVersionChoices,
            DescriptionKey: "python.version.desc",
            Group:          "Runtime"),

        new(
            Name:           "httpxMode",
            Description:    "Generate async or sync httpx clients.",
            DefaultValue:   "async",
            IsRequired:     true,
            Kind:           OptionKind.EnumSingle,
            Choices:        HttpxModeChoices,
            DescriptionKey: "python.httpxMode.desc",
            Group:          "Clients"),

        new(
            Name:           "validator",
            Description:    "Model validator library (Pydantic v2 fully supported).",
            DefaultValue:   "pydantic",
            IsRequired:     true,
            Kind:           OptionKind.EnumSingle,
            Choices:        ValidatorChoices,
            DescriptionKey: "python.validator.desc",
            Group:          "Models"),

        new(
            Name:           "rootModule",
            Description:    "Top-level Python module name for generated package.",
            DefaultValue:   "generated",
            IsRequired:     true,
            Kind:           OptionKind.String,
            DescriptionKey: "python.rootModule.desc",
            Group:          "Project"),
    ];

    private static string? NormalizeHttpxMode(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("httpxMode cannot be empty");
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "async" => PythonOptionIds.HttpxMode.Async,
            "sync" => PythonOptionIds.HttpxMode.Sync,
            var unknown => AddError(errors, $"Unknown httpxMode: '{unknown}'")
        };
    }

    private static string? NormalizeValidator(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("validator cannot be empty");
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "pydantic" or "pydantic2" or "pydantic-v2" => PythonOptionIds.Validator.Pydantic,
            "msgspec" => PythonOptionIds.Validator.Msgspec,
            var unknown => AddError(errors, $"Unknown validator: '{unknown}'")
        };
    }

    private static string? AddError(List<string> errors, string message)
    {
        errors.Add(message);
        return null;
    }
}
