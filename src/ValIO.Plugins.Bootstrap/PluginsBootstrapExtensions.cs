using Microsoft.Extensions.DependencyInjection;
using ValIO.Application.DependencyInjection;
using ValIO.Generators.CSharp;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.Java;
using ValIO.Generators.Kotlin;
using ValIO.Generators.Php;
using ValIO.Generators.Python;
using ValIO.Generators.Swift;
using ValIO.Generators.TypeScript;

namespace ValIO.Plugins.Bootstrap;

/// <summary>
/// Single helper used by every front-end to register the full set of generator plugins
/// without duplicating the list in three places.
/// </summary>
public static class PluginsBootstrapExtensions
{
    public static IServiceCollection AddAllGeneratorPlugins(this IServiceCollection services)
    {
        // C# renderer dependency is needed before CSharp plugin is registered
        services.AddSingleton<ICSharpAstRenderer, CSharpAstRenderer>();

        services.AddGeneratorPlugin<TypeScriptGeneratorPlugin>();
        services.AddGeneratorPlugin<CSharpGeneratorPlugin>();
        services.AddGeneratorPlugin<PythonGeneratorPlugin>();
        services.AddGeneratorPlugin<PhpGeneratorPlugin>();
        services.AddGeneratorPlugin<JavaGeneratorPlugin>();
        services.AddGeneratorPlugin<KotlinGeneratorPlugin>();
        services.AddGeneratorPlugin<SwiftGeneratorPlugin>();
        return services;
    }
}
