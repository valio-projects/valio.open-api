using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Java.Options;

public sealed class JavaOptionsDescriptor : IGeneratorOptionsDescriptor
{
    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new JavaGenerationOptions();

        if (rawOptions.TryGetValue("package", out var pkg) && pkg.ValueKind == JsonValueKind.String)
        {
            var raw = pkg.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
                opts.Package = raw.Trim().Trim('.');
        }

        if (rawOptions.TryGetValue("useHttpClient", out var http) && http.ValueKind == JsonValueKind.String)
        {
            var normalized = NormalizeHttpClient(http.GetString(), errorList);
            if (normalized != null)
                opts.UseHttpClient = normalized;
        }

        if (rawOptions.TryGetValue("generateBuildFiles", out var gen))
        {
            opts.GenerateBuildFiles = gen.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(gen.GetString(), out var b) => b,
                _ => opts.GenerateBuildFiles,
            };
        }

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options)
    {
        if (options is not JavaGenerationOptions opts) return ["Invalid options type"];
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(opts.Package))
            errors.Add("Option 'package' must not be empty");
        if (string.IsNullOrWhiteSpace(opts.UseHttpClient))
            errors.Add("Option 'useHttpClient' must not be empty");
        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new("package", "Root package for generated Java classes", "com.example.generated", false),
        new("useHttpClient", "HTTP client: okhttp (default)", "okhttp", false),
        new("generateBuildFiles", "Also emit build.gradle.kts + gradle.properties with required deps", "false", false),
    ];

    private static string? NormalizeHttpClient(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("Option 'useHttpClient' must not be empty");
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "okhttp" or "okhttp3" or "ok-http" => JavaOptionIds.HttpClient.OkHttp,
            var unknown => AddError(errors, $"Unknown Java HTTP client: '{unknown}'"),
        };
    }

    private static string? AddError(List<string> errors, string message)
    {
        errors.Add(message);
        return null;
    }
}
