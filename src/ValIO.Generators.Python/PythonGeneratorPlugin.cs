using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Python.Ast.Renderers;
using ValIO.Generators.Python.Options;
using ValIO.Generators.Python.Steps;

namespace ValIO.Generators.Python;

/// <summary>
/// Плагин генерации Python кода из IR.
/// Регистрирует себя через <c>AddGeneratorPlugin&lt;PythonGeneratorPlugin&gt;()</c>.
/// </summary>
public sealed class PythonGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "python";
    public string DisplayName => "Python";
    public IReadOnlyList<string> LanguageAliases => ["py", "python", "python3", "py3"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new PythonOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var renderer = services.GetRequiredService<IPythonAstRenderer>();
        var typeMapper = services.GetRequiredService<PythonIrTypeMapper>();
        var steps = new ICodeGenerationStep[]
        {
            new PythonModelsStep(namingConverter, renderer, typeMapper),
            new PythonEnumsStep(namingConverter, renderer),
            new HttpxClientStep(namingConverter, renderer, typeMapper),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<PythonIrTypeMapper>();
        services.AddSingleton<IPythonAstRenderer, PythonAstRenderer>();
    }
}
