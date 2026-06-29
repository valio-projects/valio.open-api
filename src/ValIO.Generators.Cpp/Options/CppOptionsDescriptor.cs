using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Cpp.Options;

public sealed class CppOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        errors = Array.Empty<string>();
        var opts = new Models.CppGenerationOptions();

        // Each option falls back to the model's default when absent or of the wrong JSON kind.
        string? Str(string key) =>
            rawOptions.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        bool? Bool(string key) =>
            rawOptions.TryGetValue(key, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? v.GetBoolean() : null;

        opts.ModuleApiMacro = Str("moduleApiMacro") ?? opts.ModuleApiMacro;
        opts.BlueprintCategory = Str("blueprintCategory") ?? opts.BlueprintCategory;
        opts.Copyright = Str("copyright") ?? opts.Copyright;
        opts.AddUnrealEngineIncludesInHeaders = Bool("addUnrealEngineIncludesInHeaders") ?? opts.AddUnrealEngineIncludesInHeaders;
        opts.BaseAsyncActionClassName = Str("baseAsyncActionClassName") ?? opts.BaseAsyncActionClassName;
        opts.BaseAsyncActionIncludePath = Str("baseAsyncActionIncludePath") ?? opts.BaseAsyncActionIncludePath;
        opts.ValIOClientIncludePath = Str("valIOClientIncludePath") ?? opts.ValIOClientIncludePath;
        opts.ValIORequestBuilderIncludePath = Str("valIORequestBuilderIncludePath") ?? opts.ValIORequestBuilderIncludePath;
        opts.ValIOMultipartFormDataIncludePath = Str("valIOMultipartFormDataIncludePath") ?? opts.ValIOMultipartFormDataIncludePath;
        opts.ValIOTypesIncludePath = Str("valIOTypesIncludePath") ?? opts.ValIOTypesIncludePath;
        opts.ValIOResponseIncludePath = Str("valIOResponseIncludePath") ?? opts.ValIOResponseIncludePath;
        opts.GeneratedHeaderSuffix = Str("generatedHeaderSuffix") ?? opts.GeneratedHeaderSuffix;
        opts.OutputDirectory = Str("outputDirectory") ?? opts.OutputDirectory;

        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options) => Array.Empty<string>();
    public IReadOnlyList<OptionDescription> GetOptionDescriptions() => Array.Empty<OptionDescription>();
}
