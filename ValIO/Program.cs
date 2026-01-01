using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.CLI;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Services;
using ValIO.Core.Factories;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Factories;
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
        
        // Template Cache
        services.AddSingleton<ITemplateCache, TemplateCache>();
        
        // Template Registry Factory
        services.AddSingleton<TemplateRegistryFactory>();
        
        // Note: Extractors создаются внутри генераторов, так как они требуют SchemaResolver,
        // который создается для каждого документа. Генераторы создают их самостоятельно.

        // Parsers
        services.AddSingleton<IOpenApiDocumentParser, OpenApiDocumentParser>();

        // Configuration
        services.AddSingleton<IConfigurationLoader, ConfigurationLoader>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

        // Schema Generators (Zod, Yup, Valli, etc.)
        // Регистрируем через фабрику, так как требуются зависимости, которые создаются динамически
        services.AddSingleton<ValIO.Generators.TypeScript.Zod.ZodTypeMapper>(sp => 
            new ValIO.Generators.TypeScript.Zod.ZodTypeMapper(sp.GetRequiredService<INamingConverter>()));
        
        services.AddSingleton<ValIO.Core.Interfaces.ISchemaGenerator>(sp =>
        {
            var namingConverter = sp.GetRequiredService<INamingConverter>();
            var typeMapperFactory = sp.GetRequiredService<TypeMapperFactory>();
            var templateRegistryFactory = sp.GetRequiredService<TemplateRegistryFactory>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            
            // Создаем TypeScriptTypeMapper
            var typeMapper = typeMapperFactory.CreateMapper(GenerationLanguage.TypeScript) as ValIO.Generators.TypeScript.TypeScriptTypeMapper;
            if (typeMapper == null)
                throw new InvalidOperationException("Failed to create TypeScriptTypeMapper");
            
            // Создаем ZodTypeMapper
            var zodTypeMapper = sp.GetRequiredService<ValIO.Generators.TypeScript.Zod.ZodTypeMapper>();
            
            // Создаем TemplateRegistry для Zod шаблонов
            var zodAssembly = typeof(ValIO.Generators.TypeScript.Zod.ZodSchemaGenerator).Assembly;
            var zodTemplateRegistry = templateRegistryFactory.CreateRegistry(zodAssembly);
            
            // Предзагружаем шаблоны Zod (синхронно, так как это singleton)
            zodTemplateRegistry.PreloadTemplatesAsync().GetAwaiter().GetResult();
            
            // Создаем logger
            var logger = loggerFactory.CreateLogger<ValIO.Generators.TypeScript.Zod.ZodSchemaGenerator>();
            
            return new ValIO.Generators.TypeScript.Zod.ZodSchemaGenerator(
                namingConverter,
                typeMapper,
                zodTypeMapper,
                zodTemplateRegistry,
                logger);
        });

        // Factories
        services.AddSingleton<TypeMapperFactory>();
        services.AddSingleton<CodeGeneratorFactory>();
        services.AddSingleton<SchemaGeneratorFactory>();

        // CLI
        services.AddSingleton<RootCommand>();
    }
}
