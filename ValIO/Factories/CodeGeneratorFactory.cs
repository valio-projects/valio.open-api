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
        var templateRegistryFactory = _serviceProvider.GetRequiredService<TemplateRegistryFactory>();

        return language switch
        {
            GenerationLanguage.CSharp => CreateCSharpGenerator(namingConverter, fileWriter, templateRegistryFactory),
            GenerationLanguage.TypeScript => CreateTypeScriptGenerator(namingConverter, fileWriter, templateRegistryFactory),
            _ => throw new NotSupportedException($"Generator for language {language} is not supported")
        };
    }

    private ICodeGenerator CreateCSharpGenerator(
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        TemplateRegistryFactory templateRegistryFactory)
    {
        var typeMapper = _typeMapperFactory.CreateMapper(GenerationLanguage.CSharp);
        var templateRegistry = templateRegistryFactory.CreateRegistry(typeof(CSharpCodeGenerator).Assembly);
        var logger = _serviceProvider.GetRequiredService<ILogger<CSharpCodeGenerator>>();

        return new CSharpCodeGenerator(
            typeMapper,
            namingConverter,
            fileWriter,
            templateRegistry,
            logger);
    }

    private ICodeGenerator CreateTypeScriptGenerator(
        INamingConverter namingConverter,
        IFileWriter fileWriter,
        TemplateRegistryFactory templateRegistryFactory)
    {
        var typeMapper = _typeMapperFactory.CreateMapper(GenerationLanguage.TypeScript);
        var templateRegistry = templateRegistryFactory.CreateRegistry(typeof(TypeScriptCodeGenerator).Assembly);
        var logger = _serviceProvider.GetRequiredService<ILogger<TypeScriptCodeGenerator>>();
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        var schemaGeneratorFactory = _serviceProvider.GetRequiredService<SchemaGeneratorFactory>();

        return new TypeScriptCodeGenerator(
            typeMapper,
            namingConverter,
            fileWriter,
            templateRegistry,
            logger,
            loggerFactory,
            schemaGeneratorFactory,
            templateRegistryFactory);
    }
}
