using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace ValIO.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty] private ObservableObject _currentPage;

    // Cache the GenerationViewModel so navigating Settings → Generate restores
    // the same form (recent files, schema preview, selected plugin, etc).
    public GenerationViewModel GenerationViewModel { get; }

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
        GenerationViewModel = _services.GetRequiredService<GenerationViewModel>();
        _currentPage = GenerationViewModel;
    }

    public void ShowGenerate() => CurrentPage = GenerationViewModel;
    public void ShowSettings() => CurrentPage = _services.GetRequiredService<SettingsViewModel>();
}
