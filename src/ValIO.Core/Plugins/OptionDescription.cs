namespace ValIO.Core.Plugins;

public enum OptionKind
{
    Bool,
    String,
    Int,
    EnumSingle,
    EnumMulti
}

public sealed record OptionChoice(string Id, string DisplayName, string? DescriptionKey = null);

/// <summary>
/// Description of a single plugin option, used by UI frontends to render an input control
/// and by validators to check raw values.
/// </summary>
/// <param name="Name">The option key as it appears in config/CLI (e.g. <c>"transports"</c>).</param>
/// <param name="Description">Plain-text English description. UI frontends should prefer <see cref="DescriptionKey"/> when set.</param>
/// <param name="DefaultValue">Default value as a string (may be null).</param>
/// <param name="IsRequired">Whether the value must be supplied explicitly.</param>
/// <param name="Kind">Type of input — used by UI to pick a control.</param>
/// <param name="Choices">Allowed values for <see cref="OptionKind.EnumSingle"/> / <see cref="OptionKind.EnumMulti"/>.</param>
/// <param name="DescriptionKey">Resource key in <c>PluginStrings.resx</c> used by UI for localized description.</param>
/// <param name="Group">Logical group label for grouping related options in UI.</param>
/// <param name="DependsOn">Name of another option whose truthy value makes this option visible/enabled.</param>
public sealed record OptionDescription(
    string Name,
    string Description,
    string? DefaultValue,
    bool IsRequired,
    OptionKind Kind = OptionKind.String,
    IReadOnlyList<OptionChoice>? Choices = null,
    string? DescriptionKey = null,
    string? Group = null,
    string? DependsOn = null);
