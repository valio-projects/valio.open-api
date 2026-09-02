using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Options;
using ValIO.Generators.Cpp.Steps;

namespace ValIO.Generators.Cpp;

/// <summary>
/// Plugin для генерации C++ кода из IR. Два runtime-профиля (опция "runtime"):
/// UE (USTRUCT/UCLASS + внешний UE-runtime, по умолчанию) и std
/// (cpp-httplib + nlohmann_json, самодостаточный ValIO-runtime).
/// Пайплайн содержит шаги обоих профилей; выбор делает каждый шаг в
/// ShouldExecute по context.PluginOptions (паттерн TS-транспортов).
/// Регистрируется через AddGeneratorPlugin&lt;CppGeneratorPlugin&gt;().
/// </summary>
public sealed class CppGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "cpp";
    public string DisplayName => "C++";
    public IReadOnlyList<string> LanguageAliases => ["cpp", "c++", "cpp-ue", "cpp-std"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new CppOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var renderer = services.GetRequiredService<ICppAstRenderer>();
        var steps = new ICodeGenerationStep[]
        {
            // UE runtime profile (default) — активны при Runtime == Ue или отсутствии опций.
            new CppUeModelsStep(renderer),
            new CppUeEnumsStep(renderer),
            new CppUeEndpointsStep(renderer),

            // std runtime profile (cpp-httplib + nlohmann_json) — шаги подключаются волной W5.
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ICppAstRenderer, CppAstRenderer>();
    }
}
