using System;
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
        
        // Note: Extractors создаются внутри генераторов, так как они требуют SchemaResolver,
        // который создается для каждого документа. Генераторы создают их самостоятельно.

        // Parsers
        services.AddSingleton<IOpenApiDocumentParser, OpenApiDocumentParser>();

        // Configuration
        services.AddSingleton<IConfigurationLoader, ConfigurationLoader>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

        // Renderers для TypeScript (опциональные, используются через DI для избежания циклических зависимостей)
        // Регистрируем только если проекты доступны (через условную компиляцию или проверку типов)
        try
        {
            // Регистрируем TanStack Query renderer
            var tanStackQueryRendererType = Type.GetType("ValIO.Generators.TypeScript.TanStackQuery.TanStackQueryRenderer, ValIO.Generators.TypeScript.TanStackQuery");
            if (tanStackQueryRendererType != null)
            {
                services.AddSingleton(typeof(IQueryWrapperRenderer), sp =>
                {
                    var namingConverter = sp.GetRequiredService<INamingConverter>();
                    return Activator.CreateInstance(tanStackQueryRendererType, namingConverter)!;
                });
            }
            
            // Регистрируем Zod renderers
            var zodTypeMapperType = Type.GetType("ValIO.Generators.TypeScript.Zod.ZodTypeMapper, ValIO.Generators.TypeScript.Zod");
            var zodSchemaRendererType = Type.GetType("ValIO.Generators.TypeScript.Zod.ZodSchemaRenderer, ValIO.Generators.TypeScript.Zod");
            if (zodSchemaRendererType != null && zodTypeMapperType != null)
            {
                services.AddSingleton(typeof(IModelFormatRenderer), sp =>
                {
                    var namingConverter = sp.GetRequiredService<INamingConverter>();
                    var zodTypeMapper = Activator.CreateInstance(zodTypeMapperType, namingConverter)!;
                    return Activator.CreateInstance(zodSchemaRendererType, namingConverter, zodTypeMapper)!;
                });
            }
            
            var zodEnumSchemaRendererType = Type.GetType("ValIO.Generators.TypeScript.Zod.Renderers.ZodEnumSchemaRenderer, ValIO.Generators.TypeScript.Zod");
            if (zodEnumSchemaRendererType != null)
            {
                services.AddSingleton(typeof(IEnumFormatRenderer), sp =>
                {
                    var namingConverter = sp.GetRequiredService<INamingConverter>();
                    return Activator.CreateInstance(zodEnumSchemaRendererType, namingConverter)!;
                });
            }
        }
        catch
        {
            // Если проекты не доступны, просто пропускаем регистрацию
            // Renderers будут null и функциональность будет отключена
        }

        // Factories
        services.AddSingleton<TypeMapperFactory>();
        services.AddSingleton<CodeGeneratorFactory>();
        services.AddSingleton<SchemaGeneratorFactory>();

        // CLI
        services.AddSingleton<RootCommand>();
    }
}
