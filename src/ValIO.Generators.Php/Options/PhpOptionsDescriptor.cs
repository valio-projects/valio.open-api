using System.Text.Json;
using ValIO.Core.Plugins;

namespace ValIO.Generators.Php.Options;

public sealed class PhpOptionsDescriptor : IGeneratorOptionsDescriptor
{
    private static readonly OptionChoice[] HttpClientChoices =
    [
        new("guzzle",  "Guzzle",                "php.httpClient.guzzle.desc"),
        new("laravel", "Laravel HTTP (reserved)", "php.httpClient.laravel.desc"),
    ];

    public object? ParseOptions(Dictionary<string, JsonElement> rawOptions, out IReadOnlyList<string> errors)
    {
        var errorList = new List<string>();
        var opts = new PhpGenerationOptions();

        if (rawOptions.TryGetValue("namespace", out var ns) && ns.ValueKind == JsonValueKind.String)
        {
            var raw = ns.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
                opts.Namespace = raw.Trim().Trim('\\');
        }

        if (rawOptions.TryGetValue("useHttpClient", out var http) && http.ValueKind == JsonValueKind.String)
        {
            var normalized = NormalizeHttpClient(http.GetString(), errorList);
            if (normalized != null)
                opts.UseHttpClient = normalized;
        }

        if (rawOptions.TryGetValue("generateComposerJson", out var gen))
        {
            opts.GenerateComposerJson = gen.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(gen.GetString(), out var b) => b,
                _ => opts.GenerateComposerJson,
            };
        }

        errors = errorList.AsReadOnly();
        return opts;
    }

    public IReadOnlyList<string> ValidateOptions(object options)
    {
        if (options is not PhpGenerationOptions opts) return ["Invalid options type"];
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(opts.Namespace))
            errors.Add("Option 'namespace' must not be empty");
        if (string.IsNullOrWhiteSpace(opts.UseHttpClient))
            errors.Add("Option 'useHttpClient' must not be empty");
        return errors.AsReadOnly();
    }

    public IReadOnlyList<OptionDescription> GetOptionDescriptions() =>
    [
        new(
            Name:           "namespace",
            Description:    "Root PHP namespace for generated classes.",
            DefaultValue:   "App\\Generated",
            IsRequired:     true,
            Kind:           OptionKind.String,
            DescriptionKey: "php.namespace.desc",
            Group:          "Project"),

        new(
            Name:           "useHttpClient",
            Description:    "HTTP client to wrap.",
            DefaultValue:   "guzzle",
            IsRequired:     true,
            Kind:           OptionKind.EnumSingle,
            Choices:        HttpClientChoices,
            DescriptionKey: "php.httpClient.desc",
            Group:          "Clients"),

        new(
            Name:           "generateComposerJson",
            Description:    "Also emit composer.json with required dependencies.",
            DefaultValue:   "false",
            IsRequired:     false,
            Kind:           OptionKind.Bool,
            DescriptionKey: "php.composer.desc",
            Group:          "Project"),
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
            "guzzle" or "guzzlehttp" => PhpOptionIds.HttpClient.Guzzle,
            "laravel" or "illuminate" => PhpOptionIds.HttpClient.Laravel,
            var unknown => AddError(errors, $"Unknown PHP HTTP client: '{unknown}'")
        };
    }

    private static string? AddError(List<string> errors, string message)
    {
        errors.Add(message);
        return null;
    }
}
