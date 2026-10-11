using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Plugins;
<<<<<<< HEAD
using ValIO.Generators.Cpp.Ast.Renderers;
=======
>>>>>>> gitlab/feature/cubemap_generator_mpv1
using ValIO.Generators.Cpp.Options;
using ValIO.Generators.Cpp.Steps;

namespace ValIO.Generators.Cpp;

/// <summary>
<<<<<<< HEAD
/// Plugin для генерации C++ кода из IR. Два runtime-профиля (опция "runtime"):
/// UE (USTRUCT/UCLASS + внешний UE-runtime, по умолчанию) и std
/// (cpp-httplib + nlohmann_json, самодостаточный ValIO-runtime).
/// Пайплайн содержит шаги обоих профилей; выбор делает каждый шаг в
/// ShouldExecute по context.PluginOptions (паттерн TS-транспортов).
/// Регистрируется через AddGeneratorPlugin&lt;CppGeneratorPlugin&gt;().
=======
/// Generator plugin for C++ / Unreal Engine 5 output.
/// Registered via AddAllGeneratorPlugins() in ValIO.Plugins.Bootstrap.
>>>>>>> gitlab/feature/cubemap_generator_mpv1
/// </summary>
public sealed class CppGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "cpp";
<<<<<<< HEAD
    public string DisplayName => "C++";
    public IReadOnlyList<string> LanguageAliases => ["cpp", "c++", "cpp-ue", "cpp-std"];
=======
    public string DisplayName => "C++ (Unreal Engine)";
    public IReadOnlyList<string> LanguageAliases => ["cpp", "c++", "ue", "unreal"];
>>>>>>> gitlab/feature/cubemap_generator_mpv1
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new CppOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
<<<<<<< HEAD
        var renderer = services.GetRequiredService<ICppAstRenderer>();
        var steps = new ICodeGenerationStep[]
        {
            // UE runtime profile (default) — активны при Runtime == Ue или отсутствии опций.
            new CppUeModelsStep(renderer),
            new CppUeEnumsStep(renderer),
            new CppUeEndpointsStep(renderer),

            // std runtime profile (cpp-httplib + nlohmann_json) — активны при Runtime == Std.
            new CppStdEnumsStep(renderer),
            new CppStdModelsStep(renderer),
            new CppStdSerializersStep(renderer),
            new CppStdRuntimeStep(renderer),
            new CppStdClientStep(renderer),
            new CppStdCmakeStep(renderer),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ICppAstRenderer, CppAstRenderer>();
    }
=======
        return new CodeGenerationPipeline([new CppGenerationStep()]);
    }

    public void ConfigureServices(IServiceCollection services) { }
>>>>>>> gitlab/feature/cubemap_generator_mpv1
}
