using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.CSharp.Options;

public sealed class CSharpOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new CSharpGenerationOptions();

        if (rawOptions.TryGetValue("namespace", out var ns) && ns.ValueKind == JsonValueKind.String)
            opts.Namespace = ns.GetString() ?? "Generated";

        if (rawOptions.TryGetValue("nullableReferenceTypes", out var nrt) && nrt.ValueKind == JsonValueKind.True)
            opts.NullableReferenceTypes = true;

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options) => Array.Empty<string>();

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new(
            Name:           "namespace",
            Description:    "Root namespace for generated C# code (e.g. MyOrg.Api.Client).",
            DefaultValue:   "Generated",
            IsRequired:     false,
            Kind:           OptionKind.String,
            DescriptionKey: "csharp.namespace.desc",
            Group:          "Project"),

        new(
            Name:           "nullableReferenceTypes",
            Description:    "Emit nullable-reference annotations on generated types.",
            DefaultValue:   "false",
            IsRequired:     false,
            Kind:           OptionKind.Bool,
            DescriptionKey: "csharp.nullable.desc",
            Group:          "Language features"),
    ];
}
