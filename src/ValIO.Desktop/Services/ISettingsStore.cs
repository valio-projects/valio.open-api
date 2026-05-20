namespace ValIO.Desktop.Services;

public interface ISettingsStore
{
    DesktopSettings Load();
    void Save(DesktopSettings settings);
}

public sealed class DesktopSettings
{
    public string CultureName { get; set; } = "en";
    public bool IsDarkTheme { get; set; }
    public string? LastOutputFolder { get; set; }
    public string? LastSchemaFile { get; set; }
    public string? LastLanguageId { get; set; }

    /// <summary>Most-recently-used schema paths, newest first. Capped to 8.</summary>
    public List<string> RecentSchemas { get; set; } = new();
}
