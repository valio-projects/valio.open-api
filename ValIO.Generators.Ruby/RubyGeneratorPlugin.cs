using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Ruby.Ast.Renderers;
using ValIO.Generators.Ruby.Options;
using ValIO.Generators.Ruby.Steps;

namespace ValIO.Generators.Ruby;

/// <summary>
/// Plugin that generates Ruby code (plain classes + Faraday-based clients)
/// from an IR document. Registered via <c>AddGeneratorPlugin&lt;RubyGeneratorPlugin&gt;()</c>.
/// </summary>
public sealed class RubyGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => RubyOptionIds.LanguageId;
    public string DisplayName => "Ruby";
    public IReadOnlyList<string> LanguageAliases => ["rb", "ruby-faraday"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new RubyOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var renderer = services.GetRequiredService<IRubyAstRenderer>();
        var typeMapper = services.GetRequiredService<RubyIrTypeMapper>();
        var steps = new ICodeGenerationStep[]
        {
            new RubyModelsStep(namingConverter, renderer, typeMapper),
            new RubyEnumsStep(namingConverter, renderer),
            new FaradayClientStep(namingConverter, renderer, typeMapper),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<RubyIrTypeMapper>();
        services.AddSingleton<IRubyAstRenderer, RubyAstRenderer>();
    }
}
