using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.FSharp.Ast.Renderers;
using ValIO.Generators.FSharp.Options;
using ValIO.Generators.FSharp.Steps;

namespace ValIO.Generators.FSharp;

/// <summary>
/// Plugin that generates F# code (records + DUs + RestSharp clients) from an IR
/// document. Registered via <c>AddGeneratorPlugin&lt;FSharpGeneratorPlugin&gt;()</c>.
/// </summary>
public sealed class FSharpGeneratorPlugin : IGeneratorPlugin
{
    public string Id => "fsharp";
    public string LanguageId => "fsharp";
    public string DisplayName => "F#";
    public IReadOnlyList<string> LanguageAliases => ["fs", "f#"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new FSharpOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var renderer = services.GetRequiredService<IFSharpAstRenderer>();
        var typeMapper = services.GetRequiredService<FSharpIrTypeMapper>();
        var steps = new ICodeGenerationStep[]
        {
            new FSharpModelsStep(namingConverter, renderer, typeMapper),
            new FSharpEnumsStep(namingConverter, renderer),
            new RestSharpClientStep(namingConverter, renderer, typeMapper),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<FSharpIrTypeMapper>();
        services.AddSingleton<IFSharpAstRenderer, FSharpAstRenderer>();
    }
}
