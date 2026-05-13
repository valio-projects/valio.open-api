using System.Text.Json;

namespace ValIO.Core.Plugins;

public interface IGeneratorOptionsDescriptor
{
    object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors);
    IReadOnlyList<string> ValidateOptions(object options);
    IReadOnlyList<OptionDescription> GetOptionDescriptions();
}
