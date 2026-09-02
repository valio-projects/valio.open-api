using System.Text.Json;
using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Models;

namespace ValIO.Generators.Cpp.Options;

/// <summary>
/// Дескриптор опций C++-генератора: runtime-профиль (ue | std), стандарт C++ (17|20|23),
/// стиль API (sync | async | both), форма клиента (class | interface+impl) и настройки
/// std-профиля (cpp-httplib + nlohmann_json).
/// </summary>
public sealed class CppOptionsDescriptor : IGeneratorOptionsDescriptor
{
    private static readonly OptionChoice[] RuntimeChoices =
    [
        new("ue",  "Unreal Engine (USTRUCT/UCLASS + UE runtime)", "cpp.runtime.ue.desc"),
        new("std", "Standalone C++ (cpp-httplib + nlohmann_json)", "cpp.runtime.std.desc"),
    ];

    private static readonly OptionChoice[] ApiStyleChoices =
    [
        new("sync",  "Synchronous methods (ValIO::Expected)",  "cpp.apiStyle.sync.desc"),
        new("async", "Asynchronous methods (std::future)",     "cpp.apiStyle.async.desc"),
        new("both",  "Synchronous methods + async wrappers",   "cpp.apiStyle.both.desc"),
    ];

    private static readonly OptionChoice[] ClientShapeChoices =
    [
        new("class",          "Single {Client}Client class",                   "cpp.clientShape.class.desc"),
        new("interface+impl", "Interface I{Client} + {Client}Client impl",     "cpp.clientShape.interfaceImpl.desc"),
    ];

    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new CppGenerationOptions();

        if (rawOptions.TryGetValue("runtime", out var runtime) && runtime.ValueKind == JsonValueKind.String)
            opts.Runtime = NormalizeRuntime(runtime.GetString(), errorList);

        if (rawOptions.TryGetValue("cxxStandard", out var standard))
        {
            if (standard.ValueKind == JsonValueKind.Number && standard.TryGetInt32(out var standardNumber))
                opts.CxxStandard = standardNumber;
            else if (standard.ValueKind == JsonValueKind.String && int.TryParse(standard.GetString(), out var standardParsed))
                opts.CxxStandard = standardParsed;
            else
                errorList.Add("Option 'cxxStandard' must be a number (17, 20 or 23)");
        }

        if (rawOptions.TryGetValue("apiStyle", out var apiStyle) && apiStyle.ValueKind == JsonValueKind.String)
            opts.ApiStyle = NormalizeApiStyle(apiStyle.GetString(), errorList);

        if (rawOptions.TryGetValue("clientShape", out var clientShape) && clientShape.ValueKind == JsonValueKind.String)
            opts.ClientShape = NormalizeClientShape(clientShape.GetString(), errorList);

        if (rawOptions.TryGetValue("namespace", out var ns) && ns.ValueKind == JsonValueKind.String)
            opts.Namespace = ns.GetString()?.Trim();

        if (rawOptions.TryGetValue("httpInclude", out var httpInclude) && httpInclude.ValueKind == JsonValueKind.String)
            opts.HttpInclude = httpInclude.GetString()?.Trim() ?? opts.HttpInclude;

        if (rawOptions.TryGetValue("jsonInclude", out var jsonInclude) && jsonInclude.ValueKind == JsonValueKind.String)
            opts.JsonInclude = jsonInclude.GetString()?.Trim() ?? opts.JsonInclude;

        if (rawOptions.TryGetValue("jsonFwdInclude", out var jsonFwdInclude) && jsonFwdInclude.ValueKind == JsonValueKind.String)
            opts.JsonFwdInclude = jsonFwdInclude.GetString()?.Trim() ?? opts.JsonFwdInclude;

        if (rawOptions.TryGetValue("errorModelName", out var errorModelName) && errorModelName.ValueKind == JsonValueKind.String)
            opts.ErrorModelName = errorModelName.GetString()?.Trim() ?? opts.ErrorModelName;

        if (rawOptions.TryGetValue("emitCmake", out var emitCmake))
        {
            if (emitCmake.ValueKind is JsonValueKind.True or JsonValueKind.False)
                opts.EmitCmake = emitCmake.GetBoolean();
            else
                errorList.Add("Option 'emitCmake' must be a boolean");
        }

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options)
    {
        if (options is not CppGenerationOptions opts)
            return ["Invalid options type"];

        var errors = new List<string>();
        if (opts.CxxStandard is not (17 or 20 or 23))
            errors.Add($"Option 'cxxStandard' must be 17, 20 or 23 (got {opts.CxxStandard})");
        if (!string.IsNullOrEmpty(opts.Namespace)
            && !System.Text.RegularExpressions.Regex.IsMatch(opts.Namespace, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            errors.Add("Option 'namespace' must be a valid C++ identifier");
        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new(
            Name:         "runtime",
            Description:  "Runtime profile: 'ue' emits Unreal Engine types (USTRUCT/UCLASS + external UE runtime); 'std' emits standalone C++ built on cpp-httplib + nlohmann_json with a self-contained ValIO runtime.",
            DefaultValue: "ue",
            IsRequired:   false,
            Kind:         OptionKind.EnumSingle,
            Choices:      RuntimeChoices,
            Group:        "General"),

        new(
            Name:         "cxxStandard",
            Description:  "Target C++ standard for the std profile: 17, 20 or 23. Generated code is C++17-clean; ValIO::Expected upgrades to std::expected at compile time when available.",
            DefaultValue: "17",
            IsRequired:   false,
            Kind:         OptionKind.Int,
            Choices:      null,
            Group:        "std runtime",
            DependsOn:    "runtime"),

        new(
            Name:         "apiStyle",
            Description:  "API style for the std profile: 'sync' (ValIO::Expected), 'async' (std::future) or 'both'.",
            DefaultValue: "sync",
            IsRequired:   false,
            Kind:         OptionKind.EnumSingle,
            Choices:      ApiStyleChoices,
            Group:        "std runtime",
            DependsOn:    "runtime"),

        new(
            Name:         "clientShape",
            Description:  "Client shape for the std profile: 'class' (single {Client}Client) or 'interface+impl' (I{Client} interface for mocking/DI + {Client}Client implementation).",
            DefaultValue: "class",
            IsRequired:   false,
            Kind:         OptionKind.EnumSingle,
            Choices:      ClientShapeChoices,
            Group:        "std runtime",
            DependsOn:    "runtime"),

        new(
            Name:         "namespace",
            Description:  "Root namespace for generated models/enums/clients (std profile). Defaults to the project namespace.",
            DefaultValue: null,
            IsRequired:   false,
            Kind:         OptionKind.String,
            Choices:      null,
            Group:        "std runtime",
            DependsOn:    "runtime"),

        new(
            Name:         "httpInclude",
            Description:  "Include path of cpp-httplib (override for consumers with wrapper headers).",
            DefaultValue: "httplib.h",
            IsRequired:   false,
            Kind:         OptionKind.String,
            Choices:      null,
            Group:        "std runtime",
            DependsOn:    "runtime"),

        new(
            Name:         "jsonInclude",
            Description:  "Include path of the full nlohmann/json header (used only in generated .cpp files).",
            DefaultValue: "nlohmann/json.hpp",
            IsRequired:   false,
            Kind:         OptionKind.String,
            Choices:      null,
            Group:        "std runtime",
            DependsOn:    "runtime"),

        new(
            Name:         "jsonFwdInclude",
            Description:  "Include path of the nlohmann forward-declaration header (used only in serializer headers).",
            DefaultValue: "nlohmann/json_fwd.hpp",
            IsRequired:   false,
            Kind:         OptionKind.String,
            Choices:      null,
            Group:        "std runtime",
            DependsOn:    "runtime"),

        new(
            Name:         "errorModelName",
            Description:  "Model name used to parse non-2xx response bodies; a minimal model + codec is emitted when the schema has none.",
            DefaultValue: "RestApiErrorResponse",
            IsRequired:   false,
            Kind:         OptionKind.String,
            Choices:      null,
            Group:        "std runtime",
            DependsOn:    "runtime"),

        new(
            Name:         "emitCmake",
            Description:  "Emit a CMakeLists snippet for the std profile output (file list + find_package for httplib and nlohmann_json).",
            DefaultValue: "false",
            IsRequired:   false,
            Kind:         OptionKind.Bool,
            Choices:      null,
            Group:        "std runtime",
            DependsOn:    "runtime"),
    ];

    private static CppRuntimeProfile NormalizeRuntime(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CppRuntimeProfile.Ue;

        return value.Trim().ToLowerInvariant() switch
        {
            "ue" or "unreal" or "unreal-engine" or "unrealengine" => CppRuntimeProfile.Ue,
            "std" or "standard" or "cpp-httplib" or "httplib" => CppRuntimeProfile.Std,
            var unknown => Unknown(errors, unknown, CppRuntimeProfile.Ue, "runtime", "ue, std"),
        };
    }

    private static CppApiStyle NormalizeApiStyle(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CppApiStyle.Sync;

        return value.Trim().ToLowerInvariant() switch
        {
            "sync" => CppApiStyle.Sync,
            "async" => CppApiStyle.Async,
            "both" => CppApiStyle.Both,
            var unknown => Unknown(errors, unknown, CppApiStyle.Sync, "apiStyle", "sync, async, both"),
        };
    }

    private static CppClientShape NormalizeClientShape(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CppClientShape.Class;

        return value.Trim().ToLowerInvariant() switch
        {
            "class" => CppClientShape.Class,
            "interface+impl" or "interfaceimpl" or "interface" => CppClientShape.InterfaceImpl,
            var unknown => Unknown(errors, unknown, CppClientShape.Class, "clientShape", "class, interface+impl"),
        };
    }

    private static TEnum Unknown<TEnum>(List<string> errors, string value, TEnum fallback, string optionName, string allowed)
        where TEnum : struct, Enum
    {
        errors.Add($"Unknown {optionName}: '{value}'. Must be one of: {allowed}.");
        return fallback;
    }
}
