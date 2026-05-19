using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Core.Interfaces;
using ValIO.Generators.Cpp;
using ValIO.Generators.CSharp;
using ValIO.Generators.TypeScript;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Axios.Ast.Renderers;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Renderers;
using ValIO.Generators.TypeScript.TypeBox.Ast.Renderers;
using ValIO.Generators.TypeScript.Valibot.Ast.Renderers;
using ValIO.Generators.TypeScript.Zod.Ast.Renderers;

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
        
        // Code Generators (используем фабрику для явного указания CSharpTypeMapper)
        services.AddSingleton<CSharpCodeGenerator>(sp =>
        {
            var typeMapper = sp.GetRequiredService<CSharpTypeMapper>();
            var namingConverter = sp.GetRequiredService<INamingConverter>();
            var fileWriter = sp.GetRequiredService<IFileWriter>();
            var logger = sp.GetRequiredService<ILogger<CSharpCodeGenerator>>();
            
            return new CSharpCodeGenerator(
                typeMapper,
                namingConverter,
                fileWriter,
                logger);
        });
        services.AddSingleton<ICodeGenerator>(sp => sp.GetRequiredService<CSharpCodeGenerator>());
        
        // Старые рендеры удалены - теперь используется IR → AST → Code pipeline
        // ClassModelRenderer, RecordModelRenderer, CSharpEnumRenderer больше не нужны
        
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

        // Цепочка ответственности (ITypeScriptNodeRenderer): звенья опрашиваются
        // монолитным TypeScriptAstRenderer в порядке убывания Priority. Каждое
        // звено отвечает за рендеринг стек-специфичных узлов (Zod, Valibot,
        // TypeBox, Axios, TanStack Query) и подменяет дефолтную диспетчеризацию
        // visitor'а. Регистрируется AddSingleton — звенья stateless либо
        // оборачивают stateless внутренние рендерера.
        services.AddSingleton<ITypeScriptNodeRenderer, ZodNodeRenderer>();
        services.AddSingleton<ITypeScriptNodeRenderer, ValibotNodeRenderer>();
        services.AddSingleton<ITypeScriptNodeRenderer, TypeBoxNodeRenderer>();
        services.AddSingleton<ITypeScriptNodeRenderer, AxiosNodeRenderer>();
        services.AddSingleton<ITypeScriptNodeRenderer, TanStackNodeRenderer>();

        // Code Generators
        services.AddSingleton<TypeScriptCodeGenerator>(sp =>
        {
            var typeMapper = sp.GetRequiredService<TypeScriptTypeMapper>();
            var namingConverter = sp.GetRequiredService<INamingConverter>();
            var fileWriter = sp.GetRequiredService<IFileWriter>();
            var logger = sp.GetRequiredService<ILogger<TypeScriptCodeGenerator>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var nodeRenderers = sp.GetServices<ITypeScriptNodeRenderer>();

            return new TypeScriptCodeGenerator(
                typeMapper,
                namingConverter,
                fileWriter,
                logger,
                loggerFactory,
                nodeRenderers);
        });
        services.AddSingleton<ICodeGenerator>(sp => sp.GetRequiredService<TypeScriptCodeGenerator>());

        return services;
    }
    
    /*
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
        
        // Code Renderers (Obsolete - будут удалены)
        // services.AddSingleton<TypeScriptCodeRenderer>();
        // services.AddSingleton<ICodeRenderer>(sp => sp.GetRequiredService<TypeScriptCodeRenderer>());
        
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
    }*/
    
    /// <summary>
    /// Регистрирует все генераторы, мапперы для C++/Unreal Engine
    /// </summary>
    public static IServiceCollection AddCppGenerators(this IServiceCollection services)
    {
        // Type Mappers
        services.AddSingleton<CppTypeMapper>();
        services.AddSingleton<ITypeMapper>(sp => sp.GetRequiredService<CppTypeMapper>());
        
        // Code Generators
        services.AddSingleton<CppCodeGenerator>(sp =>
        {
            var typeMapper = sp.GetRequiredService<CppTypeMapper>();
            var namingConverter = sp.GetRequiredService<INamingConverter>();
            var fileWriter = sp.GetRequiredService<IFileWriter>();
            var logger = sp.GetRequiredService<ILogger<CppCodeGenerator>>();
            
            return new CppCodeGenerator(
                typeMapper,
                namingConverter,
                fileWriter,
                logger);
        });
        services.AddSingleton<ICodeGenerator>(sp => sp.GetRequiredService<CppCodeGenerator>());
        
        return services;
    }
    
    /// <summary>
    /// Регистрирует все доступные генераторы, мапперы и рендеры
    /// </summary>
    public static IServiceCollection AddAllGenerators(this IServiceCollection services)
    {
        services.AddCSharpGenerators();
        services.AddTypeScriptGenerators();
        services.AddCppGenerators();
        
        return services;
    }
}
