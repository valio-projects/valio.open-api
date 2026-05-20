using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.TypeScript.Options;

public sealed class TypeScriptOptionsDescriptor : IGeneratorOptionsDescriptor
{
    private static readonly OptionChoice[] TransportChoices =
    [
        new("fetch",            "Fetch API",            "ts.transports.fetch.desc"),
        new("axios",            "Axios",                "ts.transports.axios.desc"),
        new("ky",               "Ky",                   "ts.transports.ky.desc"),
        new("undici",           "Undici",               "ts.transports.undici.desc"),
        new("ofetch",           "ofetch",               "ts.transports.ofetch.desc"),
        new("got",              "got",                  "ts.transports.got.desc"),
        new("angular-rxjs",     "Angular HttpClient (RxJS)", "ts.transports.angularRxjs.desc"),
        new("angular-promise",  "Angular HttpClient (Promise)", "ts.transports.angularPromise.desc"),
        new("solid",            "SolidStart fetch",     "ts.transports.solid.desc"),
    ];

    private static readonly OptionChoice[] EnumFormatChoices =
    [
        new("union",       "String union",   "ts.enumFormat.union.desc"),
        new("enum",        "TS enum",        "ts.enumFormat.enum.desc"),
        new("constObject", "const object",   "ts.enumFormat.constObject.desc"),
    ];

    private static readonly OptionChoice[] ValidationChoices =
    [
        new("zod3",    "Zod 3",   "ts.validation.zod3.desc"),
        new("zod4",    "Zod 4",   "ts.validation.zod4.desc"),
        new("valibot", "Valibot", "ts.validation.valibot.desc"),
        new("typebox", "TypeBox", "ts.validation.typebox.desc"),
    ];

    private static readonly OptionChoice[] QueryWrapperChoices =
    [
        new("tanstack-react",   "TanStack Query — React",   "ts.queryWrapper.tanstackReact.desc"),
        new("tanstack-vue",     "TanStack Query — Vue",     "ts.queryWrapper.tanstackVue.desc"),
        new("tanstack-angular", "TanStack Query — Angular", "ts.queryWrapper.tanstackAngular.desc"),
        new("tanstack-solid",   "TanStack Query — Solid",   "ts.queryWrapper.tanstackSolid.desc"),
        new("tanstack-svelte",  "TanStack Query — Svelte",  "ts.queryWrapper.tanstackSvelte.desc"),
        new("swr",              "SWR",                      "ts.queryWrapper.swr.desc"),
    ];

    private static readonly OptionChoice[] MockChoices =
    [
        new("msw", "Mock Service Worker", "ts.mocks.msw.desc"),
    ];

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
        new(
            Name:           "transports",
            Description:    "HTTP transport library to generate clients for. At least one is required.",
            DefaultValue:   "fetch",
            IsRequired:     true,
            Kind:           OptionKind.EnumMulti,
            Choices:        TransportChoices,
            DescriptionKey: "ts.transports.desc",
            Group:          "Clients"),

        new(
            Name:           "enumFormat",
            Description:    "How OpenAPI enums are represented in TypeScript.",
            DefaultValue:   "union",
            IsRequired:     false,
            Kind:           OptionKind.EnumSingle,
            Choices:        EnumFormatChoices,
            DescriptionKey: "ts.enumFormat.desc",
            Group:          "Models"),

        new(
            Name:           "validation",
            Description:    "Validation library to emit runtime schemas for. Leave empty to skip.",
            DefaultValue:   null,
            IsRequired:     false,
            Kind:           OptionKind.EnumSingle,
            Choices:        ValidationChoices,
            DescriptionKey: "ts.validationLibrary.desc",
            Group:          "Validation"),

        new(
            Name:           "queryWrapper",
            Description:    "Optional client wrapper integration (TanStack Query, SWR, ...).",
            DefaultValue:   null,
            IsRequired:     false,
            Kind:           OptionKind.EnumSingle,
            Choices:        QueryWrapperChoices,
            DescriptionKey: "ts.queryWrapper.desc",
            Group:          "Query wrappers",
            DependsOn:      "transports"),

        new(
            Name:           "mocks",
            Description:    "Generate mock handlers alongside clients.",
            DefaultValue:   null,
            IsRequired:     false,
            Kind:           OptionKind.EnumSingle,
            Choices:        MockChoices,
            DescriptionKey: "ts.mockLibrary.desc",
            Group:          "Testing"),
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
