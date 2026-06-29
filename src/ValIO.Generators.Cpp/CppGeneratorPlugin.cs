using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Options;
using ValIO.Generators.Cpp.Steps;

namespace ValIO.Generators.Cpp;

/// <summary>
/// Generator plugin for C++ / Unreal Engine 5 output.
/// Registered via AddAllGeneratorPlugins() in ValIO.Plugins.Bootstrap.
/// </summary>
public sealed class CppGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "cpp";
    public string DisplayName => "C++ (Unreal Engine)";
    public IReadOnlyList<string> LanguageAliases => ["cpp", "c++", "ue", "unreal"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new CppOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        return new CodeGenerationPipeline([new CppGenerationStep()]);
    }

    public void ConfigureServices(IServiceCollection services) { }
}
