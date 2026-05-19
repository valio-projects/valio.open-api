using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Plugins;

namespace ValIO.ServiceRegistration;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers ValIO core services (registry, IR builder, naming, file writer).</summary>
    public static IServiceCollection AddValIOCore(this IServiceCollection services)
    {
        // GeneratorRegistry собирает все IGeneratorPlugin зарегистрированные в DI
        services.AddSingleton<IGeneratorRegistry>(sp =>
            new GeneratorRegistry(sp.GetServices<IGeneratorPlugin>()));

        return services;
    }

    /// <summary>
    /// Registers a generator plugin. The plugin registers its own services via ConfigureServices().
    /// </summary>
    public static IServiceCollection AddGeneratorPlugin<TPlugin>(this IServiceCollection services)
        where TPlugin : class, IGeneratorPlugin
    {
        // Регистрируем сам плагин как синглтон через интерфейс
        services.AddSingleton<IGeneratorPlugin, TPlugin>();
        // Также регистрируем как конкретный тип для прямого доступа если нужен
        services.AddSingleton<TPlugin>(sp => (TPlugin)sp.GetServices<IGeneratorPlugin>()
            .OfType<TPlugin>().First());

        // Плагин сам регистрирует свои внутренние сервисы
        var instance = Activator.CreateInstance<TPlugin>();
        instance.ConfigureServices(services);

        return services;
    }
}
