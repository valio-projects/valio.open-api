using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Rust.Ast.Renderers;
using ValIO.Generators.Rust.Options;
using ValIO.Generators.Rust.Steps;

namespace ValIO.Generators.Rust;

/// <summary>
/// Plugin that generates Rust code (serde structs + reqwest async clients)
/// from an IR document. Registered via <c>AddGeneratorPlugin&lt;RustGeneratorPlugin&gt;()</c>.
/// </summary>
public sealed class RustGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "rust";
    public string DisplayName => "Rust";
    public IReadOnlyList<string> LanguageAliases => ["rs", "rust-reqwest"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new RustOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var renderer = services.GetRequiredService<IRustAstRenderer>();
        var typeMapper = services.GetRequiredService<RustIrTypeMapper>();
        var steps = new ICodeGenerationStep[]
        {
            new RustModelsStep(namingConverter, renderer, typeMapper),
            new RustEnumsStep(namingConverter, renderer),
            new ReqwestClientStep(namingConverter, renderer, typeMapper),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<RustIrTypeMapper>();
        services.AddSingleton<IRustAstRenderer, RustAstRenderer>();
    }
}
