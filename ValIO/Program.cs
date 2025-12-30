using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.CLI;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Services;
using ValIO.Core.Factories;
using ValIO.Core.Interfaces;
using ValIO.Core.Services;
using ValIO.Parsers.Interfaces;
using ValIO.Parsers.Services;

namespace ValIO;

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

        // Parsers
        services.AddSingleton<IOpenApiDocumentParser, OpenApiDocumentParser>();

        // Configuration
        services.AddSingleton<IConfigurationLoader, ConfigurationLoader>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

        // Factory
        services.AddSingleton<CodeGeneratorFactory>();

        // CLI
        services.AddSingleton<RootCommand>();

        // Note: Генераторы будут зарегистрированы позже, когда они будут реализованы
        // services.AddSingleton<ICodeGenerator, CSharpCodeGenerator>();
        // services.AddSingleton<ICodeGenerator, TypeScriptCodeGenerator>();
    }
}
