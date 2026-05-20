using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Dart.Ast.Renderers;
using ValIO.Generators.Dart.Options;
using ValIO.Generators.Dart.Steps;

namespace ValIO.Generators.Dart;

/// <summary>
/// Plugin that generates Dart code (immutable classes + Dio service clients).
/// </summary>
public sealed class DartGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "dart";
    public string DisplayName => "Dart";
    public IReadOnlyList<string> LanguageAliases => ["dart", "dart-dio"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new DartOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var renderer = services.GetRequiredService<IDartAstRenderer>();
        var typeMapper = services.GetRequiredService<DartIrTypeMapper>();
        var steps = new ICodeGenerationStep[]
        {
            new DartModelsStep(namingConverter, renderer, typeMapper),
            new DartEnumsStep(namingConverter, renderer),
            new DioClientStep(namingConverter, renderer, typeMapper),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DartIrTypeMapper>();
        services.AddSingleton<IDartAstRenderer, DartAstRenderer>();
    }
}
