using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Kotlin.Ast.Renderers;
using ValIO.Generators.Kotlin.Options;
using ValIO.Generators.Kotlin.Steps;

namespace ValIO.Generators.Kotlin;

/// <summary>
/// Plugin that generates Kotlin code (models + OkHttp/coroutines clients) from an IR document.
/// Registered via <c>AddGeneratorPlugin&lt;KotlinGeneratorPlugin&gt;()</c>.
/// </summary>
public sealed class KotlinGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "kotlin";
    public string DisplayName => "Kotlin";
    public IReadOnlyList<string> LanguageAliases => ["kt", "kotlin"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new KotlinOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var renderer = services.GetRequiredService<IKotlinAstRenderer>();
        var typeMapper = services.GetRequiredService<KotlinIrTypeMapper>();
        var steps = new ICodeGenerationStep[]
        {
            new KotlinModelsStep(namingConverter, renderer, typeMapper),
            new KotlinEnumsStep(namingConverter, renderer),
            new OkHttpClientStep(namingConverter, renderer, typeMapper),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<KotlinIrTypeMapper>();
        services.AddSingleton<IKotlinAstRenderer, KotlinAstRenderer>();
    }
}
