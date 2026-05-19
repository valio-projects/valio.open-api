using Microsoft.Extensions.DependencyInjection;
using ValIO.Application.Services;
using ValIO.Configuration.Interfaces;
using ValIO.Configuration.Services;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Parsers.Interfaces;
using ValIO.Parsers.Services;

namespace ValIO.Application.DependencyInjection;

/// <summary>
/// One-stop DI extensions used by every front-end (Cli, Web, Desktop).
/// Methods are idempotent — safe to call from any composition root.
/// </summary>
public static class ValIOServiceCollectionExtensions
{
    /// <summary>
    /// Registers core stateless services: naming converter, schema parser, configuration loader/validator,
    /// and the generator registry. Plugins themselves are registered via <c>AddGeneratorPlugin&lt;T&gt;</c>
    /// (typically through <c>ValIO.Plugins.Bootstrap.AddAllGeneratorPlugins()</c>).
    /// </summary>
    public static IServiceCollection AddValIOCore(this IServiceCollection services)
    {
        services.AddSingleton<INamingConverter, NamingConverter>();
        services.AddSingleton<IOpenApiDocumentParser, OpenApiDocumentParser>();
        services.AddSingleton<IConfigurationLoader, ConfigurationLoader>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
        services.AddSingleton<IGeneratorRegistry>(sp =>
            new GeneratorRegistry(sp.GetServices<IGeneratorPlugin>()));
        return services;
    }

    /// <summary>Registers the orchestrator (singleton, stateless).</summary>
    public static IServiceCollection AddValIOApplication(this IServiceCollection services)
    {
        services.AddSingleton<IGenerationOrchestrator, GenerationOrchestrator>();
        return services;
    }

    /// <summary>
    /// Registers a generator plugin. The plugin also registers its own internal services
    /// via <see cref="IGeneratorPlugin.ConfigureServices"/>.
    /// </summary>
    public static IServiceCollection AddGeneratorPlugin<TPlugin>(this IServiceCollection services)
        where TPlugin : class, IGeneratorPlugin
    {
        services.AddSingleton<IGeneratorPlugin, TPlugin>();
        services.AddSingleton<TPlugin>(sp => (TPlugin)sp.GetServices<IGeneratorPlugin>()
            .OfType<TPlugin>().First());
        var instance = Activator.CreateInstance<TPlugin>();
        instance.ConfigureServices(services);
        return services;
    }
}
