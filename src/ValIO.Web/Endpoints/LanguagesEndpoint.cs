using ValIO.Core.Plugins;

namespace ValIO.Web.Endpoints;

/// <summary>
/// GET /api/languages — list every plugin known to the registry.
/// Drives the language picker in the UI and any 3rd-party client (CLI scripts,
/// "valio" Slack bot, etc.) that needs the catalog without bundling all the
/// generator DLLs themselves.
/// </summary>
public static class LanguagesEndpoint
{
    public sealed record LanguageDto(
        string LanguageId,
        string DisplayName,
        IReadOnlyList<string> Aliases,
        int OptionsCount);

    public static IResult Handle(IGeneratorRegistry registry)
    {
        var langs = registry.AllPlugins
            .OrderBy(p => p.DisplayName, StringComparer.Ordinal)
            .Select(p => new LanguageDto(
                p.LanguageId,
                p.DisplayName,
                p.LanguageAliases.ToArray(),
                p.OptionsDescriptor.GetOptionDescriptions().Count))
            .ToArray();
        return Results.Ok(langs);
    }
}
