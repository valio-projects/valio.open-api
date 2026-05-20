using ValIO.Core.Plugins;

namespace ValIO.Web.Endpoints;

/// <summary>
/// GET /api/options/{language} — describe every plugin option that the
/// generator accepts. Used by the Web/Desktop dynamic-form rendering and by
/// 3rd-party tooling that wants to build its own UI on top of ValIO.
/// </summary>
public static class OptionsEndpoint
{
    public sealed record OptionChoiceDto(string Id, string DisplayName, string? DescriptionKey);

    public sealed record OptionDto(
        string Name,
        string Description,
        string? DefaultValue,
        bool IsRequired,
        string Kind,
        IReadOnlyList<OptionChoiceDto>? Choices,
        string? DescriptionKey,
        string? Group,
        string? DependsOn);

    public static IResult Handle(string language, IGeneratorRegistry registry)
    {
        var plugin = registry.FindPlugin(language);
        if (plugin is null)
            return Results.NotFound(new { error = $"Unknown language: {language}" });

        var options = plugin.OptionsDescriptor.GetOptionDescriptions()
            .Select(d => new OptionDto(
                d.Name,
                d.Description,
                d.DefaultValue,
                d.IsRequired,
                d.Kind.ToString(),
                d.Choices?.Select(c => new OptionChoiceDto(c.Id, c.DisplayName, c.DescriptionKey)).ToArray(),
                d.DescriptionKey,
                d.Group,
                d.DependsOn))
            .ToArray();
        return Results.Ok(options);
    }
}
