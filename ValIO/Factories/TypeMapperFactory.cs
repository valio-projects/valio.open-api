using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Generators.CSharp;
using ValIO.Generators.TypeScript;

namespace ValIO.Factories;

/// <summary>
/// Фабрика для создания мапперов типов (Factory Pattern)
/// </summary>
public class TypeMapperFactory
{
    private readonly IServiceProvider _serviceProvider;

    public TypeMapperFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Создает маппер типов для указанного языка
    /// </summary>
    public ITypeMapper CreateMapper(GenerationLanguage language)
    {
        var namingConverter = _serviceProvider.GetRequiredService<INamingConverter>();

        return language switch
        {
            GenerationLanguage.CSharp => new CSharpTypeMapper(namingConverter),
            GenerationLanguage.TypeScript => new TypeScriptTypeMapper(namingConverter),
            _ => throw new NotSupportedException($"Type mapper for language {language} is not supported")
        };
    }
}
