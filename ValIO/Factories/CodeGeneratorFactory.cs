using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Core.Factories;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Generators.CSharp;
using ValIO.Generators.TypeScript;

namespace ValIO.Factories;

/// <summary>
/// Фабрика для создания генераторов кода (Factory Pattern)
/// </summary>
public class CodeGeneratorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TypeMapperFactory _typeMapperFactory;

    public CodeGeneratorFactory(IServiceProvider serviceProvider, TypeMapperFactory typeMapperFactory)
    {
        _serviceProvider = serviceProvider;
        _typeMapperFactory = typeMapperFactory;
    }

    public ICodeGenerator CreateGenerator(GenerationLanguage language, GenerationOptions? options)
    {
        var namingConverter = _serviceProvider.GetRequiredService<INamingConverter>();
        var fileWriter = _serviceProvider.GetRequiredService<IFileWriter>();

        return language switch
        {
            GenerationLanguage.CSharp => CreateCSharpGenerator(namingConverter, fileWriter),
            GenerationLanguage.TypeScript => CreateTypeScriptGenerator(namingConverter, fileWriter),
            _ => throw new NotSupportedException($"Generator for language {language} is not supported")
        };
    }

    private ICodeGenerator CreateCSharpGenerator(
        INamingConverter namingConverter,
        IFileWriter fileWriter)
    {
        var typeMapper = _typeMapperFactory.CreateMapper(GenerationLanguage.CSharp);
        var logger = _serviceProvider.GetRequiredService<ILogger<CSharpCodeGenerator>>();

        return new CSharpCodeGenerator(
            typeMapper,
            namingConverter,
            fileWriter,
            logger);
    }

    private ICodeGenerator CreateTypeScriptGenerator(
        INamingConverter namingConverter,
        IFileWriter fileWriter)
    {
        var typeMapper = _typeMapperFactory.CreateMapper(GenerationLanguage.TypeScript);
        var logger = _serviceProvider.GetRequiredService<ILogger<TypeScriptCodeGenerator>>();
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();

        // Получаем опциональные renderers через DI (могут быть не зарегистрированы)
        var queryWrapperRenderer = _serviceProvider.GetService<IQueryWrapperRenderer>();
        var zodModelRenderer = _serviceProvider.GetServices<IModelFormatRenderer>()
            .FirstOrDefault(r => r.Format == ModelFormat.ValidationSchema && r.Language == GenerationLanguage.TypeScript);
        var zodEnumRenderer = _serviceProvider.GetServices<IEnumFormatRenderer>()
            .FirstOrDefault(r => r.Format == EnumFormat.ValidationSchema && r.Language == GenerationLanguage.TypeScript);

        return new TypeScriptCodeGenerator(
            typeMapper,
            namingConverter,
            fileWriter,
            logger,
            loggerFactory,
            queryWrapperRenderer,
            zodModelRenderer,
            zodEnumRenderer);
    }
}
