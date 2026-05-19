using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Application.DependencyInjection;
using ValIO.Cli.Commands;
using ValIO.Localization.DependencyInjection;
using ValIO.Output.DependencyInjection;
using ValIO.Plugins.Bootstrap;

namespace ValIO.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // --lang must be parsed BEFORE DI is built so localized formatters see the right culture.
        var lang = PickLanguage(args);
        if (!string.IsNullOrWhiteSpace(lang))
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(lang);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
            }
            catch (CultureNotFoundException)
            {
                Console.Error.WriteLine($"Unknown culture: {lang}");
            }
        }

        var services = new ServiceCollection();
        ConfigureServices(services);

        await using var serviceProvider = services.BuildServiceProvider();
        var rootCommand = serviceProvider.GetRequiredService<RootCommand>();

        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }

    private static string? PickLanguage(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--lang")
            {
                if (i + 1 < args.Length) return args[i + 1];
            }
            else if (args[i].StartsWith("--lang=", StringComparison.Ordinal))
            {
                return args[i]["--lang=".Length..];
            }
        }
        return Environment.GetEnvironmentVariable("VALIO_LANG");
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

        services.AddSingleton<RootCommand>();
    }
}
