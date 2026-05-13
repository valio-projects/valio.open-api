using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.CLI;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Services;
using ValIO.Core.Interfaces;
using ValIO.Core.Services;
using ValIO.Parsers.Interfaces;
using ValIO.Parsers.Services;
using ValIO.ServiceRegistration;

namespace ValIO;

// generate --schema "C:\Workspace\ValIO\\out\\swagger.json" --output C:\\Workspace\\ValIO\\out --config "C:\\Workspace\\ValIO\\valio.config.json"

class Program
{
    static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();
        var rootCommand = serviceProvider.GetRequiredService<RootCommand>();

        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }

    static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // HTTP Client
        services.AddHttpClient();

        // Core services
        services.AddSingleton<INamingConverter, NamingConverter>();
        services.AddSingleton<IFileWriter, FileWriter>();
        
        // Note: Extractors создаются внутри генераторов, так как они требуют SchemaResolver,
        // который создается для каждого документа. Генераторы создают их самостоятельно.

        // Parsers
        services.AddSingleton<IOpenApiDocumentParser, OpenApiDocumentParser>();

        // Configuration
        services.AddSingleton<IConfigurationLoader, ConfigurationLoader>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

        // Plugin registry и core plugin infrastructure
        services.AddValIOCore();
        // Когда плагины будут готовы:
        // services.AddGeneratorPlugin<TypeScriptGeneratorPlugin>();
        // services.AddGeneratorPlugin<CSharpGeneratorPlugin>();
        // services.AddGeneratorPlugin<CppGeneratorPlugin>();

        // CLI
        services.AddSingleton<RootCommand>();
    }
}
