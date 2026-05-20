using System.Globalization;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Application.DependencyInjection;
using ValIO.Desktop.Localization;
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

        // Wire the XAML {loc:Tr} markup to the runtime LocalizationService.
        // Must happen BEFORE MainWindow is constructed so first render uses the right culture.
        var locService = Services.GetRequiredService<LocalizationService>();
        var settingsStore = Services.GetRequiredService<ISettingsStore>();
        var savedSettings = settingsStore.Load();
        if (!string.IsNullOrWhiteSpace(savedSettings.CultureName))
        {
            try { locService.Culture = CultureInfo.GetCultureInfo(savedSettings.CultureName); }
            catch (CultureNotFoundException) { /* ignore unknown culture, keep default */ }
        }
        Localizer.Instance.Attach(locService);

        // Apply persisted theme before MainWindow is created so first paint matches.
        RequestedThemeVariant = savedSettings.IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;

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
        services.AddSingleton<ISchemaPreviewer, SchemaPreviewer>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<GenerationViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
