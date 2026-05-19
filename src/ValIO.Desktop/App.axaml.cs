using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Application.DependencyInjection;
using ValIO.Desktop.Services;
using ValIO.Desktop.ViewModels;
using ValIO.Desktop.Views;
using ValIO.Localization.DependencyInjection;
using ValIO.Output.DependencyInjection;
using ValIO.Plugins.Bootstrap;

namespace ValIO.Desktop;

public sealed class App : global::Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = default!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Information);
        });
        services.AddHttpClient();

        services.AddValIOCore();
        services.AddValIOLocalization();
        services.AddValIOOutputs();
        services.AddValIOApplication();
        services.AddAllGeneratorPlugins();

        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<GenerationViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
