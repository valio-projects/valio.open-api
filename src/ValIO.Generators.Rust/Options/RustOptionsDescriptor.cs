using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Rust.Options;

/// <summary>
/// Parses and validates Rust plugin options from configuration.
/// </summary>
public sealed class RustOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new RustGenerationOptions();

        if (rawOptions.TryGetValue("crateName", out var crate) && crate.ValueKind == JsonValueKind.String)
        {
            var value = crate.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                opts.CrateName = value.Trim();
        }

        if (rawOptions.TryGetValue("anyMapping", out var any) && any.ValueKind == JsonValueKind.String)
        {
            var normalized = NormalizeAnyMapping(any.GetString(), errorList);
            if (normalized != null)
                opts.AnyMapping = normalized;
        }

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options)
    {
        if (options is not RustGenerationOptions opts)
            return ["Invalid options type"];

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(opts.CrateName))
            errors.Add("crateName must be set");

        if (opts.AnyMapping != RustOptionIds.AnyMapping.JsonValue &&
            opts.AnyMapping != RustOptionIds.AnyMapping.String)
        {
            errors.Add($"anyMapping '{opts.AnyMapping}' is not supported (allowed: json-value, string)");
        }

        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new("crateName", "Output crate/folder name for generated Rust files", "generated", false),
        new("anyMapping", "How to map IR Any: json-value (default) or string", "json-value", false),
    ];

    private static string? NormalizeAnyMapping(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("anyMapping cannot be empty");
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "json-value" or "jsonvalue" or "serde-json-value" => RustOptionIds.AnyMapping.JsonValue,
            "string" => RustOptionIds.AnyMapping.String,
            var unknown => AddError(errors, $"Unknown anyMapping: '{unknown}'")
        };
    }

    private static string? AddError(List<string> errors, string message)
    {
        errors.Add(message);
        return null;
    }
}
