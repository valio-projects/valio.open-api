using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Kotlin.Options;

/// <summary>
/// Parses and validates Kotlin plugin options from configuration.
/// </summary>
public sealed class KotlinOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new KotlinGenerationOptions();

        if (rawOptions.TryGetValue("package", out var pkg) && pkg.ValueKind == JsonValueKind.String)
        {
            var value = pkg.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                opts.Package = value.Trim();
        }

        if (rawOptions.TryGetValue("serialization", out var ser) && ser.ValueKind == JsonValueKind.String)
        {
            var normalized = NormalizeSerialization(ser.GetString(), errorList);
            if (normalized != null)
                opts.Serialization = normalized;
        }

        if (rawOptions.TryGetValue("generateBuildFiles", out var gen))
        {
            if (gen.ValueKind == JsonValueKind.True) opts.GenerateBuildFiles = true;
            else if (gen.ValueKind == JsonValueKind.False) opts.GenerateBuildFiles = false;
            else errorList.Add("generateBuildFiles must be boolean");
        }

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options)
    {
        if (options is not KotlinGenerationOptions opts)
            return ["Invalid options type"];

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(opts.Package))
            errors.Add("package must be set");

        if (opts.Serialization != KotlinOptionIds.Serialization.Kotlinx)
            errors.Add($"Serialization '{opts.Serialization}' is not yet supported");

        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new("package", "Root package name for generated Kotlin code", "com.example.generated", false),
        new("serialization", "Serialization library: kotlinx (only supported value today)", "kotlinx", false),
        new("generateBuildFiles", "Emit Gradle/build.kts files alongside sources", "false", false),
    ];

    private static string? NormalizeSerialization(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("serialization cannot be empty");
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "kotlinx" or "kotlinx-serialization" or "kotlinx.serialization" => KotlinOptionIds.Serialization.Kotlinx,
            "moshi" => KotlinOptionIds.Serialization.Moshi,
            "gson" => KotlinOptionIds.Serialization.Gson,
            var unknown => AddError(errors, $"Unknown serialization: '{unknown}'")
        };
    }

    private static string? AddError(List<string> errors, string message)
    {
        errors.Add(message);
        return null;
    }
}
