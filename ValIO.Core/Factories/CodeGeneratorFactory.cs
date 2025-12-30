using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

namespace ValIO.Core.Factories;

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
        // Получаем все зарегистрированные генераторы
        var generators = _serviceProvider.GetServices<ICodeGenerator>();

        // Находим подходящий генератор
        var generator = generators.FirstOrDefault(g => 
            g.SupportsLanguage(language) && 
            (options == null || g.SupportsOptions(options)));

        if (generator == null)
            throw new NotSupportedException($"Generator for language {language} with options {options} is not supported");

        return generator;
    }
}

