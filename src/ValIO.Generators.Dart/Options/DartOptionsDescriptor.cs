using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Dart.Options;

/// <summary>
/// Parses and validates Dart plugin options from configuration JSON.
/// </summary>
public sealed class DartOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new DartGenerationOptions();

        if (rawOptions.TryGetValue("packageName", out var pkg) && pkg.ValueKind == JsonValueKind.String)
        {
            var value = pkg.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                opts.PackageName = value.Trim();
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
        if (options is not DartGenerationOptions opts)
            return ["Invalid options type"];

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(opts.PackageName))
            errors.Add("packageName must be set");

        if (opts.AnyMapping != DartOptionIds.AnyMapping.Dynamic
            && opts.AnyMapping != DartOptionIds.AnyMapping.Object
            && opts.AnyMapping != DartOptionIds.AnyMapping.String)
        {
            errors.Add($"anyMapping '{opts.AnyMapping}' is not supported (allowed: dynamic, object, string)");
        }

        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new("packageName", "Top-level Dart package name", "generated", false),
        new("anyMapping", "How to map IR Any: dynamic (default), object, or string", "dynamic", false),
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
            "dynamic" => DartOptionIds.AnyMapping.Dynamic,
            "object"  => DartOptionIds.AnyMapping.Object,
            "string"  => DartOptionIds.AnyMapping.String,
            var unknown => Add(errors, $"Unknown anyMapping: '{unknown}'")
        };
    }

    private static string? Add(List<string> errors, string msg)
    {
        errors.Add(msg);
        return null;
    }
}
