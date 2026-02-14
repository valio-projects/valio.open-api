using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Generators.Cpp;
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
    /// Создает маппер типов для указанного языка из зарегистрированных в DI
    /// </summary>
    public ITypeMapper CreateMapper(GenerationLanguage language)
    {
        return language switch
        {
            GenerationLanguage.CSharp => _serviceProvider.GetRequiredService<CSharpTypeMapper>(),
            GenerationLanguage.TypeScript => _serviceProvider.GetRequiredService<TypeScriptTypeMapper>(),
            GenerationLanguage.Cpp => _serviceProvider.GetRequiredService<CppTypeMapper>(),
            _ => throw new NotSupportedException($"Type mapper for language {language} is not supported")
        };
    }
}
