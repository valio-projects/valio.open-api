using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

namespace ValIO.Core.Factories;

/// <summary>
/// Фабрика для получения генераторов схем валидации (Factory Pattern)
/// </summary>
public class SchemaGeneratorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public SchemaGeneratorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Получает генератор схем для указанной библиотеки валидации
    /// </summary>
    public ISchemaGenerator? GetSchemaGenerator(ValidationLibrary? validationLibrary)
    {
        if (validationLibrary == null)
            return null;

        // Получаем все зарегистрированные генераторы схем
        var generators = _serviceProvider.GetServices<ISchemaGenerator>();
        
        // Находим генератор для указанной библиотеки
        return generators.FirstOrDefault(g => g.ValidationLibrary == validationLibrary.Value);
    }
}
