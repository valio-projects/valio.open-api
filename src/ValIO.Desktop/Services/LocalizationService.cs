using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ValIO.Desktop.Services;

/// <summary>
/// Holds the current UI culture for the desktop app and notifies when it changes.
/// Views can subscribe (or rebind manually) to refresh translated bindings.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private CultureInfo _culture = CultureInfo.GetCultureInfo("en");

    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            if (Equals(_culture, value)) return;
            _culture = value;
            CultureInfo.DefaultThreadCurrentCulture = value;
            CultureInfo.DefaultThreadCurrentUICulture = value;
            OnPropertyChanged(nameof(Culture));
        }
    }

    public IReadOnlyList<CultureInfo> Supported { get; } = new[]
    {
        CultureInfo.GetCultureInfo("en"),
        CultureInfo.GetCultureInfo("ru"),
        CultureInfo.GetCultureInfo("de"),
        CultureInfo.GetCultureInfo("zh-CN"),
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
