using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Java.Ast.Renderers;
using ValIO.Generators.Java.Options;
using ValIO.Generators.Java.Steps;

namespace ValIO.Generators.Java;

/// <summary>
/// Plugin для генерации Java-кода из IR. Регистрируется через <c>AddGeneratorPlugin&lt;JavaGeneratorPlugin&gt;()</c>.
/// </summary>
public sealed class JavaGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "java";
    public string DisplayName => "Java";
    public IReadOnlyList<string> LanguageAliases => ["java", "java21", "java-okhttp"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new JavaOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var renderer = new JavaAstRenderer();
        var typeMapper = new JavaIrTypeMapper();
        var steps = new ICodeGenerationStep[]
        {
            new JavaModelsStep(namingConverter, renderer, typeMapper),
            new JavaEnumsStep(namingConverter, renderer),
            new OkHttpClientStep(namingConverter, renderer, typeMapper),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Java plugin не имеет дополнительных сервисов.
    }
}
