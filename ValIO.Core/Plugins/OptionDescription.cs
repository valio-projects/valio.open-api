namespace ValIO.Core.Plugins;

public sealed record OptionDescription(
    string Name,
    string Description,
    string? DefaultValue,
    bool IsRequired);
