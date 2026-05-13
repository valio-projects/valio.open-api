using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Options;
using ValIO.Generators.CSharp.Steps;

namespace ValIO.Generators.CSharp;

/// <summary>
/// Plugin для генерации C# кода из IR.
/// Регистрирует себя через AddGeneratorPlugin&lt;CSharpGeneratorPlugin&gt;().
/// </summary>
public sealed class CSharpGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "csharp";
    public string DisplayName => "C#";
    public IReadOnlyList<string> LanguageAliases => ["cs", "csharp", "c#"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new CSharpOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var renderer = services.GetRequiredService<ICSharpAstRenderer>();
        var steps = new ICodeGenerationStep[]
        {
            new CSharpModelsStep(namingConverter, renderer),
            new CSharpEnumsStep(namingConverter, renderer),
            new CSharpClientStep(namingConverter, renderer),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Дополнительные сервисы CSharp-плагина при необходимости
    }
}
