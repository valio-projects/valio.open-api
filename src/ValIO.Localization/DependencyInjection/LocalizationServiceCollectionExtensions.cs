using Microsoft.Extensions.DependencyInjection;
using ValIO.Localization.Reporters;

namespace ValIO.Localization.DependencyInjection;

public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers ValIO localization: <c>AddLocalization</c> from Microsoft.Extensions.Localization
    /// (resources resolved from the <c>ValIO.Localization</c> assembly) + a default
    /// <see cref="IConsoleReporter"/> implementation.
    /// </summary>
    public static IServiceCollection AddValIOLocalization(this IServiceCollection services)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddSingleton<IConsoleReporter, StdoutConsoleReporter>();
        return services;
    }
}
