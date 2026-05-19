namespace ValIO.Desktop.Services;

public interface ISettingsStore
{
    DesktopSettings Load();
    void Save(DesktopSettings settings);
}

public sealed class DesktopSettings
{
    public string CultureName { get; set; } = "en";
    public string? LastOutputFolder { get; set; }
    public string? LastSchemaFile { get; set; }
    public string? LastLanguageId { get; set; }
}
