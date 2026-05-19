using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Swift.Ast.Renderers;
using ValIO.Generators.Swift.Options;
using ValIO.Generators.Swift.Steps;

namespace ValIO.Generators.Swift;

/// <summary>
/// Plugin that generates Swift code (Codable structs + URLSession async clients)
/// from an IR document. Registered via <c>AddGeneratorPlugin&lt;SwiftGeneratorPlugin&gt;()</c>.
/// </summary>
public sealed class SwiftGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "swift";
    public string DisplayName => "Swift";
    public IReadOnlyList<string> LanguageAliases => ["swift"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new SwiftOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var renderer = services.GetRequiredService<ISwiftAstRenderer>();
        var typeMapper = services.GetRequiredService<SwiftIrTypeMapper>();
        var steps = new ICodeGenerationStep[]
        {
            new SwiftModelsStep(namingConverter, renderer, typeMapper),
            new SwiftEnumsStep(namingConverter, renderer),
            new URLSessionClientStep(namingConverter, renderer, typeMapper),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SwiftIrTypeMapper>();
        services.AddSingleton<ISwiftAstRenderer, SwiftAstRenderer>();
    }
}
