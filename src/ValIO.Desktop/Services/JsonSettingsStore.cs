using System.Text.Json;

namespace ValIO.Desktop.Services;

/// <summary>
/// Stores DesktopSettings as JSON under %APPDATA%/ValIO/desktop-settings.json
/// (or ~/.config/valio/desktop-settings.json on non-Windows).
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public JsonSettingsStore()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData,
                                            Environment.SpecialFolderOption.Create);
        if (string.IsNullOrEmpty(dir))
            dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        var valioDir = Path.Combine(dir, "ValIO");
        Directory.CreateDirectory(valioDir);
        _path = Path.Combine(valioDir, "desktop-settings.json");
    }

    public DesktopSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(_path)) ?? new();
        }
        catch
        {
            // settings corrupt: fall back to defaults
        }
        return new DesktopSettings();
    }

    public void Save(DesktopSettings settings)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // best-effort; UI continues even if disk is read-only
        }
    }
}
