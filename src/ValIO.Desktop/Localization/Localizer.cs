using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using ValIO.Desktop.Services;

namespace ValIO.Desktop.Localization;

/// <summary>
/// Singleton that XAML <c>{loc:Tr key}</c> markup binds to.
/// Holds a <see cref="ResourceManager"/> for the desktop UI strings; raises a
/// PropertyChanged on the indexer when the active culture flips so every bound
/// element re-reads its key on the fly.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();

    private readonly ResourceManager _rm = new(
        baseName: "ValIO.Desktop.Resources.Strings",
        assembly: typeof(Localizer).Assembly);

    private LocalizationService? _service;

    public void Attach(LocalizationService service)
    {
        if (_service is not null) _service.PropertyChanged -= OnServiceChanged;
        _service = service;
        _service.PropertyChanged += OnServiceChanged;
    }

    /// <summary>
    /// XAML accessor. Returns the localized string for <paramref name="key"/>,
    /// falling back to the key itself when the resource is missing — so a
    /// stray <c>{loc:Tr foo}</c> shows up as literal "foo" instead of crashing.
    /// </summary>
    public string this[string key] => _rm.GetString(key, CurrentCulture) ?? key;

    public CultureInfo CurrentCulture => _service?.Culture ?? CultureInfo.GetCultureInfo("en");

    private void OnServiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Force every binding using us to re-read its key.
        OnPropertyChanged("Item[]");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
