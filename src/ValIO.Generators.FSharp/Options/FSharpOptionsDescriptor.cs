using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.FSharp.Options;

/// <summary>
/// Parses and validates F# plugin options from configuration.
/// </summary>
public sealed class FSharpOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new FSharpGenerationOptions();

        if (rawOptions.TryGetValue("namespace", out var ns) && ns.ValueKind == JsonValueKind.String)
        {
            var value = ns.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                opts.Namespace = value.Trim();
        }

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options)
    {
        if (options is not FSharpGenerationOptions opts)
            return ["Invalid options type"];

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(opts.Namespace))
            errors.Add("namespace must be set");

        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new("namespace", "Top-level F# namespace/module prefix for generated files", FSharpOptionIds.DefaultNamespace, false),
    ];
}
