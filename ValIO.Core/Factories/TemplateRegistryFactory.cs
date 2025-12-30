using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Core.Interfaces;
using ValIO.Core.Services;

namespace ValIO.Core.Factories;

/// <summary>
/// Фабрика для создания TemplateRegistry для разных генераторов
/// </summary>
public class TemplateRegistryFactory
{
    private readonly IServiceProvider _serviceProvider;

    public TemplateRegistryFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Создает TemplateRegistry для указанной сборки
    /// </summary>
    public ITemplateRegistry CreateRegistry(Assembly assembly)
    {
        var resourceNamespace = $"{assembly.GetName().Name}.Templates";
        var cache = _serviceProvider.GetRequiredService<ITemplateCache>();
        var logger = _serviceProvider.GetRequiredService<ILogger<EmbeddedResourceTemplateRegistry>>();
        return new EmbeddedResourceTemplateRegistry(assembly, resourceNamespace, cache, logger);
    }
}
