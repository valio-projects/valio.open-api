using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Generators.Cpp;
using ValIO.Generators.CSharp;
using ValIO.Generators.TypeScript;

namespace ValIO.Factories;

/// <summary>
/// Фабрика для создания генераторов кода (Factory Pattern)
/// </summary>
public class CodeGeneratorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public CodeGeneratorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ICodeGenerator CreateGenerator(GenerationLanguage language, GenerationOptions? options)
    {
        return language switch
        {
            GenerationLanguage.CSharp => _serviceProvider.GetRequiredService<CSharpCodeGenerator>(),
            GenerationLanguage.TypeScript => _serviceProvider.GetRequiredService<TypeScriptCodeGenerator>(),
            GenerationLanguage.Cpp => _serviceProvider.GetRequiredService<CppCodeGenerator>(),
            _ => throw new NotSupportedException($"Generator for language {language} is not supported")
        };
    }
}
