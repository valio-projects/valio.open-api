using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Steps;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Plugin для генерации TypeScript кода из IR.
/// Регистрирует себя через AddGeneratorPlugin&lt;TypeScriptGeneratorPlugin&gt;().
/// </summary>
public sealed class TypeScriptGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "typescript";
    public string DisplayName => "TypeScript";
    public IReadOnlyList<string> LanguageAliases => ["ts", "typescript"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new TypeScriptOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var steps = new ICodeGenerationStep[]
        {
            new TypeScriptModelsStep(namingConverter),
            new TypeScriptEnumsStep(namingConverter),
            new TypeScriptParamsStep(namingConverter),
            new TypeScriptResultsStep(namingConverter),
            new ZodSchemasStep(namingConverter),
            new ZodParamsSchemasStep(namingConverter),
            new ValibotSchemasStep(namingConverter),
            new TypeBoxSchemasStep(namingConverter),
            new FetchClientStep(namingConverter),
            new AxiosClientStep(namingConverter),
            new KyClientStep(namingConverter),
            new UndiciClientStep(namingConverter),
            new OFetchClientStep(namingConverter),
            new TanStackQueryStep(namingConverter),
            new TanStackVueStep(namingConverter),
            new TypeScriptIndexStep(),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Дополнительные сервисы TypeScript-плагина при необходимости
    }
}
