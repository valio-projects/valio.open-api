using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.CLI;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Services;
using ValIO.Core.Factories;
using ValIO.Core.Interfaces;
using ValIO.Core.Services;
using ValIO.Generators.CSharp;
using ValIO.Parsers.Interfaces;
using ValIO.Parsers.Services;
using System.Reflection;

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
        
        // Template Cache
        services.AddSingleton<ITemplateCache, TemplateCache>();
        
        // Template Registry - для C# генератора используем embedded resources
        services.AddSingleton<ITemplateRegistry>(sp =>
        {
            var assembly = typeof(CSharpCodeGenerator).Assembly;
            var resourceNamespace = $"{assembly.GetName().Name}.Templates";
            var cache = sp.GetRequiredService<ITemplateCache>();
            var logger = sp.GetRequiredService<ILogger<EmbeddedResourceTemplateRegistry>>();
            return new EmbeddedResourceTemplateRegistry(assembly, resourceNamespace, cache, logger);
        });
        
        // Note: Extractors создаются внутри генераторов, так как они требуют SchemaResolver,
        // который создается для каждого документа. Генераторы создают их самостоятельно.

        // Parsers
        services.AddSingleton<IOpenApiDocumentParser, OpenApiDocumentParser>();

        // Configuration
        services.AddSingleton<IConfigurationLoader, ConfigurationLoader>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

        // Code Generators - Type Mappers
        services.AddSingleton<ITypeMapper, CSharpTypeMapper>();

        // Code Generators
        services.AddSingleton<ICodeGenerator, CSharpCodeGenerator>();
        // services.AddSingleton<ICodeGenerator, TypeScriptCodeGenerator>();

        // Factory
        services.AddSingleton<CodeGeneratorFactory>();

        // CLI
        services.AddSingleton<RootCommand>();
    }
}
