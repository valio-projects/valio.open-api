using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Swift.Options;

public sealed class SwiftOptionsDescriptor : IGeneratorOptionsDescriptor
{
    private static readonly OptionChoice[] AnyMappingChoices =
    [
        new("anycodable", "AnyCodable",            "swift.anyMapping.anyCodable.desc"),
        new("string",     "String (lossy)",        "swift.anyMapping.string.desc"),
    ];

    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new SwiftGenerationOptions();

        if (rawOptions.TryGetValue("moduleName", out var module) && module.ValueKind == JsonValueKind.String)
        {
            var value = module.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                opts.ModuleName = value.Trim();
        }

        if (rawOptions.TryGetValue("anyMapping", out var any) && any.ValueKind == JsonValueKind.String)
        {
            var normalized = NormalizeAnyMapping(any.GetString(), errorList);
            if (normalized != null)
                opts.AnyMapping = normalized;
        }

        if (rawOptions.TryGetValue("generateBuildFiles", out var gen))
        {
            if (gen.ValueKind == JsonValueKind.True) opts.GenerateBuildFiles = true;
            else if (gen.ValueKind == JsonValueKind.False) opts.GenerateBuildFiles = false;
            else errorList.Add("generateBuildFiles must be boolean");
        }

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options)
    {
        if (options is not SwiftGenerationOptions opts)
            return ["Invalid options type"];

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(opts.ModuleName))
            errors.Add("moduleName must be set");

        if (opts.AnyMapping != SwiftOptionIds.AnyMapping.AnyCodable &&
            opts.AnyMapping != SwiftOptionIds.AnyMapping.String)
        {
            errors.Add($"anyMapping '{opts.AnyMapping}' is not supported (allowed: anycodable, string)");
        }

        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new(
            Name:           "moduleName",
            Description:    "Output module/folder name for generated Swift files.",
            DefaultValue:   "Generated",
            IsRequired:     true,
            Kind:           OptionKind.String,
            DescriptionKey: "swift.module.desc",
            Group:          "Project"),

        new(
            Name:           "anyMapping",
            Description:    "How IR Any type maps onto Swift.",
            DefaultValue:   "anycodable",
            IsRequired:     true,
            Kind:           OptionKind.EnumSingle,
            Choices:        AnyMappingChoices,
            DescriptionKey: "swift.anyMapping.desc",
            Group:          "Models"),

        new(
            Name:           "generateBuildFiles",
            Description:    "Emit Package.swift alongside sources.",
            DefaultValue:   "false",
            IsRequired:     false,
            Kind:           OptionKind.Bool,
            DescriptionKey: "swift.buildFiles.desc",
            Group:          "Project"),
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
            "anycodable" or "any-codable" => SwiftOptionIds.AnyMapping.AnyCodable,
            "string" => SwiftOptionIds.AnyMapping.String,
            var unknown => AddError(errors, $"Unknown anyMapping: '{unknown}'")
        };
    }

    private static string? AddError(List<string> errors, string message)
    {
        errors.Add(message);
        return null;
    }
}
