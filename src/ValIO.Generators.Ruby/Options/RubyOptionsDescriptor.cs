using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Ruby.Options;

/// <summary>
/// Parses and validates Ruby plugin options. The Ruby plugin currently has no
/// real options (Ruby is dynamically typed), so this descriptor always returns
/// the default <see cref="RubyGenerationOptions"/>.
/// </summary>
public sealed class RubyOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new RubyGenerationOptions();

        if (rawOptions.TryGetValue("moduleName", out var module) && module.ValueKind == JsonValueKind.String)
        {
            var value = module.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                opts.ModuleName = value.Trim();
        }

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options)
    {
        if (options is not RubyGenerationOptions opts)
            return ["Invalid options type"];

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(opts.ModuleName))
            errors.Add("moduleName must be set");

        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new("moduleName", "Output module/folder name for generated Ruby files", "generated", false),
    ];
}
