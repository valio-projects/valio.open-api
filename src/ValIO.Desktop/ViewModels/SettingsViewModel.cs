using System.Globalization;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using ValIO.Desktop.Services;

namespace ValIO.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ISettingsStore _store;

    public IReadOnlyList<CultureInfo> SupportedCultures => _localization.Supported;

    [ObservableProperty] private CultureInfo _selectedCulture;
    [ObservableProperty] private bool _isDarkMode;

    public SettingsViewModel(LocalizationService localization, ISettingsStore store)
    {
        _localization = localization;
        _store = store;

        var settings = store.Load();
        _selectedCulture = localization.Culture;
        _isDarkMode = settings.IsDarkTheme;

        // Apply the persisted theme on first construction (idempotent if already set
        // by App startup); the app-startup path already applies the theme, but
        // re-creating the SettingsViewModel is the right place to enforce it.
        ApplyTheme(_isDarkMode);
    }

    partial void OnSelectedCultureChanged(CultureInfo value)
    {
        _localization.Culture = value;
        var settings = _store.Load();
        settings.CultureName = value.Name;
        _store.Save(settings);
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        ApplyTheme(value);
        var settings = _store.Load();
        settings.IsDarkTheme = value;
        _store.Save(settings);
    }

    private static void ApplyTheme(bool dark)
    {
        // Disambiguate from ValIO.Application namespace via global::.
        var app = global::Avalonia.Application.Current;
        if (app is null) return;
        app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
