using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Cpp.Options;

public sealed class CppOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        errors = Array.Empty<string>();
        return null;
    }

    public IReadOnlyList<string> ValidateOptions(object options) => Array.Empty<string>();
    public IReadOnlyList<OptionDescription> GetOptionDescriptions() => Array.Empty<OptionDescription>();
}
