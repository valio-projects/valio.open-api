using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Generators.CSharp;
using ValIO.Generators.CSharp.Renderers;
using ValIO.Generators.TypeScript;
using ValIO.Generators.TypeScript.Renderers;
using ValIO.Generators.TypeScript.Zod;
using ValIO.Generators.TypeScript.Zod.Models;
using ValIO.Generators.TypeScript.Zod.Renderers;
using ValIO.Generators.TypeScript.TanStackQuery;

namespace ValIO.ServiceRegistration;

/// <summary>
/// Расширения для регистрации сервисов в DI контейнер
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует все генераторы, мапперы и рендеры для C#
    /// </summary>
    public static IServiceCollection AddCSharpGenerators(this IServiceCollection services)
    {
        // Type Mappers (регистрируем и как интерфейс, и как конкретный тип для фабрики)
        services.AddSingleton<CSharpTypeMapper>();
        services.AddSingleton<ITypeMapper>(sp => sp.GetRequiredService<CSharpTypeMapper>());
        
        // Code Generators
        services.AddSingleton<CSharpCodeGenerator>();
        services.AddSingleton<ICodeGenerator>(sp => sp.GetRequiredService<CSharpCodeGenerator>());
        
        // Code Renderers
        services.AddSingleton<CSharpCodeRenderer>();
        services.AddSingleton<ICodeRenderer>(sp => sp.GetRequiredService<CSharpCodeRenderer>());
        
        // Model Format Renderers
        services.AddSingleton<ClassModelRenderer>();
        services.AddSingleton<RecordModelRenderer>();
        services.AddSingleton<IModelFormatRenderer>(sp => sp.GetRequiredService<ClassModelRenderer>());
        services.AddSingleton<IModelFormatRenderer>(sp => sp.GetRequiredService<RecordModelRenderer>());
        
        // Enum Format Renderers
        services.AddSingleton<CSharpEnumRenderer>();
        services.AddSingleton<IEnumFormatRenderer>(sp => sp.GetRequiredService<CSharpEnumRenderer>());
        
        return services;
    }
    
    /// <summary>
    /// Регистрирует все генераторы, мапперы и рендеры для TypeScript
    /// </summary>
    public static IServiceCollection AddTypeScriptGenerators(this IServiceCollection services)
    {
        // Type Mappers (регистрируем и как интерфейс, и как конкретный тип для фабрики)
        services.AddSingleton<TypeScriptTypeMapper>();
        services.AddSingleton<ITypeMapper>(sp => sp.GetRequiredService<TypeScriptTypeMapper>());
        
        // Code Generators (используем фабрику для поддержки опциональных зависимостей)
        services.AddSingleton<TypeScriptCodeGenerator>(sp =>
        {
            var typeMapper = sp.GetRequiredService<TypeScriptTypeMapper>();
            var namingConverter = sp.GetRequiredService<INamingConverter>();
            var fileWriter = sp.GetRequiredService<IFileWriter>();
            var logger = sp.GetRequiredService<ILogger<TypeScriptCodeGenerator>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            
            // Опциональные renderers (могут быть не зарегистрированы)
            var queryWrapperRenderer = sp.GetService<IQueryWrapperRenderer>();
            var zodModelRenderer = sp.GetServices<IModelFormatRenderer>()
                .FirstOrDefault(r => r.Format == ValIO.Core.Models.TemplateData.ModelFormat.ValidationSchema && r.Language == GenerationLanguage.TypeScript);
            var zodEnumRenderer = sp.GetServices<IEnumFormatRenderer>()
                .FirstOrDefault(r => r.Format == ValIO.Core.Models.TemplateData.EnumFormat.ValidationSchema && r.Language == GenerationLanguage.TypeScript);
            
            return new TypeScriptCodeGenerator(
                typeMapper,
                namingConverter,
                fileWriter,
                logger,
                loggerFactory,
                queryWrapperRenderer,
                zodModelRenderer,
                zodEnumRenderer);
        });
        services.AddSingleton<ICodeGenerator>(sp => sp.GetRequiredService<TypeScriptCodeGenerator>());
        
        // Code Renderers
        services.AddSingleton<TypeScriptCodeRenderer>();
        services.AddSingleton<ICodeRenderer>(sp => sp.GetRequiredService<TypeScriptCodeRenderer>());
        
        // Model Format Renderers
        services.AddSingleton<InterfaceModelRenderer>();
        services.AddSingleton<IModelFormatRenderer>(sp => sp.GetRequiredService<InterfaceModelRenderer>());
        
        // Enum Format Renderers
        services.AddSingleton<TypeScriptEnumRenderer>();
        services.AddSingleton<TypeScriptUnionTypeRenderer>();
        services.AddSingleton<IEnumFormatRenderer>(sp => sp.GetRequiredService<TypeScriptEnumRenderer>());
        services.AddSingleton<IEnumFormatRenderer>(sp => sp.GetRequiredService<TypeScriptUnionTypeRenderer>());
        
        return services;
    }
    
    /// <summary>
    /// Регистрирует Zod генераторы и рендеры для TypeScript
    /// </summary>
    public static IServiceCollection AddZodGenerators(this IServiceCollection services)
    {
        // Zod Type Mapper (не реализует ITypeMapper, это внутренний компонент ZodSchemaRenderer)
        services.AddSingleton<ZodTypeMapper>();
        
        // Model Format Renderers (Zod schemas)
        services.AddSingleton<ZodSchemaRenderer>();
        services.AddSingleton<IModelFormatRenderer>(sp => sp.GetRequiredService<ZodSchemaRenderer>());
        
        // Enum Format Renderers (Zod schemas)
        services.AddSingleton<ZodEnumSchemaRenderer>();
        services.AddSingleton<IEnumFormatRenderer>(sp => sp.GetRequiredService<ZodEnumSchemaRenderer>());
        
        return services;
    }
    
    /// <summary>
    /// Регистрирует TanStack Query рендеры для TypeScript
    /// </summary>
    public static IServiceCollection AddTanStackQueryRenderers(this IServiceCollection services)
    {
        // Query Wrapper Renderers
        services.AddSingleton<TanStackQueryRenderer>();
        services.AddSingleton<IQueryWrapperRenderer>(sp => sp.GetRequiredService<TanStackQueryRenderer>());
        
        return services;
    }
    
    /// <summary>
    /// Регистрирует все доступные генераторы, мапперы и рендеры
    /// </summary>
    public static IServiceCollection AddAllGenerators(this IServiceCollection services)
    {
        services.AddCSharpGenerators();
        services.AddTypeScriptGenerators();
        services.AddZodGenerators();
        services.AddTanStackQueryRenderers();
        
        return services;
    }
}
