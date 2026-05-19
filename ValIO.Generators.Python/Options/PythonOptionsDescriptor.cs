using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Python.Options;

/// <summary>
/// Парсит и валидирует опции Python-плагина из конфигурации.
/// </summary>
public sealed class PythonOptionsDescriptor : IGeneratorOptionsDescriptor
{
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
        new("pythonVersion", "Target Python version (3.11+)", "3.11", false),
        new("httpxMode", "HTTPX client mode: async, sync", "async", false),
        new("validator", "Model validator library: pydantic", "pydantic", false),
        new("rootModule", "Root Python module name for generated package", "generated", false),
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
