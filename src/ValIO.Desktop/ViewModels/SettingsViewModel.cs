using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ValIO.Desktop.Services;

namespace ValIO.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ISettingsStore _store;

    public IReadOnlyList<CultureInfo> SupportedCultures => _localization.Supported;

    [ObservableProperty] private CultureInfo _selectedCulture;

    public SettingsViewModel(LocalizationService localization, ISettingsStore store)
    {
        _localization = localization;
        _store = store;
        _selectedCulture = localization.Culture;
    }

    partial void OnSelectedCultureChanged(CultureInfo value)
    {
        _localization.Culture = value;
        var settings = _store.Load();
        settings.CultureName = value.Name;
        _store.Save(settings);
    }
}
