using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Generators.CSharp;

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
            //GenerationLanguage.TypeScript => _serviceProvider.GetRequiredService<TypeScriptCodeGenerator>(),
            _ => throw new NotSupportedException($"Generator for language {language} is not supported")
        };
    }
}
