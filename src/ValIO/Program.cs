using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Application.DependencyInjection;
using ValIO.CLI;
using ValIO.Localization.DependencyInjection;
using ValIO.Output.DependencyInjection;
using ValIO.Plugins.Bootstrap;

namespace ValIO;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();
        var rootCommand = serviceProvider.GetRequiredService<RootCommand>();

        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
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
