using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.TypeScript.Options;

public sealed class TypeScriptOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new TypeScriptGenerationOptions();

        if (rawOptions.TryGetValue("transports", out var transports))
        {
            if (transports.ValueKind == JsonValueKind.Array)
                opts.Transports = [.. transports.EnumerateArray().Select(t => t.GetString() ?? "")];
            else if (transports.ValueKind == JsonValueKind.String)
                opts.Transports = [transports.GetString() ?? "fetch"];
        }

        if (rawOptions.TryGetValue("enumFormat", out var enumFmt) && enumFmt.ValueKind == JsonValueKind.String)
            opts.EnumFormat = enumFmt.GetString() ?? "union";

        if (rawOptions.TryGetValue("validation", out var validation) && validation.ValueKind == JsonValueKind.String)
            opts.ValidationLibrary = validation.GetString();

        if (rawOptions.TryGetValue("queryWrapper", out var qw) && qw.ValueKind == JsonValueKind.String)
            opts.QueryWrapper = qw.GetString();

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options)
    {
        if (options is not TypeScriptGenerationOptions opts) return ["Invalid options type"];
        var errors = new List<string>();
        if (opts.Transports.Count == 0) errors.Add("At least one transport must be specified");
        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new("transports", "HTTP transport(s) to generate: fetch, axios, ky, undici, ofetch, got, angular", "fetch", false),
        new("enumFormat", "TypeScript enum format: enum, union, constObject", "union", false),
        new("validation", "Validation library: zod3, zod4, valibot, typebox", null, false),
        new("queryWrapper", "Query wrapper: tanstack-react, tanstack-vue, tanstack-angular, swr", null, false),
    ];
}
