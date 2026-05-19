using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace ValIO.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty] private ObservableObject _currentPage;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
        _currentPage = _services.GetRequiredService<GenerationViewModel>();
    }

    public void ShowGenerate() => CurrentPage = _services.GetRequiredService<GenerationViewModel>();
    public void ShowSettings() => CurrentPage = _services.GetRequiredService<SettingsViewModel>();
}
