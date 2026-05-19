using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.CLI;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Services;
using ValIO.Core.Interfaces;
using ValIO.Core.Services;
using ValIO.Generators.CSharp;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.Kotlin;
using ValIO.Generators.Php;
using ValIO.Generators.Python;
using ValIO.Generators.TypeScript;
using ValIO.Parsers.Interfaces;
using ValIO.Parsers.Services;
using ValIO.ServiceRegistration;
using ValIO.Services;

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

        // Plugin registry и core plugin infrastructure
        services.AddValIOCore();
        services.AddGeneratorPlugin<TypeScriptGeneratorPlugin>();
        services.AddSingleton<ICSharpAstRenderer, CSharpAstRenderer>();
        services.AddGeneratorPlugin<CSharpGeneratorPlugin>();
        services.AddGeneratorPlugin<PythonGeneratorPlugin>();
        services.AddGeneratorPlugin<PhpGeneratorPlugin>();
        services.AddGeneratorPlugin<KotlinGeneratorPlugin>();

        // Orchestrator
        services.AddSingleton<GenerationOrchestrator>();

        // CLI
        services.AddSingleton<RootCommand>();
    }
}
