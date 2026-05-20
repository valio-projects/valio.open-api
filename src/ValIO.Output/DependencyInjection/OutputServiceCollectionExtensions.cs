using Microsoft.Extensions.DependencyInjection;
using ValIO.Output.Abstractions;

namespace ValIO.Output.DependencyInjection;

public static class OutputServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IOutputSinkFactory"/>. Idempotent.</summary>
    public static IServiceCollection AddValIOOutputs(this IServiceCollection services)
    {
        services.AddSingleton<IOutputSinkFactory, DefaultOutputSinkFactory>();
        return services;
    }
}
