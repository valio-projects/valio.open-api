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
                opts.Transports =
                [
                    .. transports.EnumerateArray()
                        .Select(t => NormalizeTransport(t.GetString(), errorList))
                        .Where(t => t != null)
                        .Cast<string>()
                ];
            else if (transports.ValueKind == JsonValueKind.String)
            {
                var transport = NormalizeTransport(transports.GetString(), errorList);
                opts.Transports = transport != null ? [transport] : [];
            }
            else
            {
                errorList.Add("Option 'transports' must be a string or array of strings");
            }
        }

        if (rawOptions.TryGetValue("enumFormat", out var enumFmt) && enumFmt.ValueKind == JsonValueKind.String)
            opts.EnumFormat = enumFmt.GetString() ?? "union";

        if (rawOptions.TryGetValue("validation", out var validation) && validation.ValueKind == JsonValueKind.String)
            opts.ValidationLibrary = NormalizeValidation(validation.GetString(), errorList);

        if (rawOptions.TryGetValue("queryWrapper", out var qw) && qw.ValueKind == JsonValueKind.String)
            opts.QueryWrapper = NormalizeQueryWrapper(qw.GetString(), errorList);

        // Accept both "mocks" and "mockLibrary" config keys for symmetry with
        // "validation" / "validationLibrary"-style aliases the user may already use.
        if (rawOptions.TryGetValue("mocks", out var mocks) && mocks.ValueKind == JsonValueKind.String)
            opts.MockLibrary = NormalizeMockLibrary(mocks.GetString(), errorList);
        else if (rawOptions.TryGetValue("mockLibrary", out var mockLibrary) && mockLibrary.ValueKind == JsonValueKind.String)
            opts.MockLibrary = NormalizeMockLibrary(mockLibrary.GetString(), errorList);

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
        new("transports", "HTTP transport(s) to generate: fetch, axios, ky, undici, ofetch, got, angular, solid", "fetch", false),
        new("enumFormat", "TypeScript enum format: enum, union, constObject", "union", false),
        new("validation", "Validation library: zod3, zod4, valibot, typebox", null, false),
        new("queryWrapper", "Query wrapper: tanstack-react, tanstack-vue, tanstack-angular, swr", null, false),
        new("mocks", "Mock library to emit: msw (writes single mocks/handlers.ts). Alias: mockLibrary", null, false),
    ];

    private static string? NormalizeTransport(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("Transport cannot be empty");
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "fetch" => TypeScriptOptionIds.Transport.Fetch,
            "axios" => TypeScriptOptionIds.Transport.Axios,
            "ky" => TypeScriptOptionIds.Transport.Ky,
            "undici" => TypeScriptOptionIds.Transport.Undici,
            "ofetch" => TypeScriptOptionIds.Transport.OFetch,
            "got" => TypeScriptOptionIds.Transport.Got,
            "angular" or "angular-rxjs" => TypeScriptOptionIds.Transport.AngularRxjs,
            "angular-promise" or "angular-async" => TypeScriptOptionIds.Transport.AngularPromise,
            "solid" or "solidstart" or "solid-start" => TypeScriptOptionIds.Transport.Solid,
            var unknown => AddError(errors, $"Unknown transport: '{unknown}'")
        };
    }

    private static string? NormalizeValidation(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "zod" or "zod3" => TypeScriptOptionIds.Validation.Zod3,
            "zod4" => TypeScriptOptionIds.Validation.Zod4,
            "valibot" => TypeScriptOptionIds.Validation.Valibot,
            "typebox" or "@sinclair/typebox" => TypeScriptOptionIds.Validation.TypeBox,
            var unknown => AddError(errors, $"Unknown validation library: '{unknown}'")
        };
    }

    private static string? NormalizeQueryWrapper(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "tanstack" or "tanstack-react" or "react-query" => TypeScriptOptionIds.QueryWrapper.TanStackReact,
            "tanstack-vue" => TypeScriptOptionIds.QueryWrapper.TanStackVue,
            "tanstack-angular" => TypeScriptOptionIds.QueryWrapper.TanStackAngular,
            "tanstack-solid" => TypeScriptOptionIds.QueryWrapper.TanStackSolid,
            "tanstack-svelte" => TypeScriptOptionIds.QueryWrapper.TanStackSvelte,
            "swr" => TypeScriptOptionIds.QueryWrapper.Swr,
            var unknown => AddError(errors, $"Unknown queryWrapper: '{unknown}'")
        };
    }

    private static string? NormalizeMockLibrary(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "msw" => TypeScriptOptionIds.MockLibrary.Msw,
            var unknown => AddError(errors, $"Unknown mock library: '{unknown}'")
        };
    }

    private static string? AddError(List<string> errors, string message)
    {
        errors.Add(message);
        return null;
    }
}
