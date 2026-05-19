using Microsoft.Extensions.DependencyInjection;
using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Php.Ast.Renderers;
using ValIO.Generators.Php.Options;
using ValIO.Generators.Php.Steps;

namespace ValIO.Generators.Php;

/// <summary>
/// Plugin для генерации PHP-кода из IR. Регистрирует себя через <c>AddGeneratorPlugin&lt;PhpGeneratorPlugin&gt;()</c>.
/// </summary>
public sealed class PhpGeneratorPlugin : IGeneratorPlugin
{
    public string LanguageId => "php";
    public string DisplayName => "PHP";
    public IReadOnlyList<string> LanguageAliases => ["php", "php8", "php-guzzle"];
    public IGeneratorOptionsDescriptor OptionsDescriptor { get; } = new PhpOptionsDescriptor();

    public ICodeGenerationPipeline CreatePipeline(IServiceProvider services)
    {
        var namingConverter = services.GetRequiredService<INamingConverter>();
        var renderer = new PhpAstRenderer();
        var typeMapper = new PhpIrTypeMapper();
        var steps = new ICodeGenerationStep[]
        {
            new PhpModelsStep(namingConverter, renderer, typeMapper),
            new PhpEnumsStep(namingConverter, renderer),
            new GuzzleClientStep(namingConverter, renderer, typeMapper),
        };
        return new CodeGenerationPipeline(steps);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // PHP plugin не имеет дополнительных сервисов.
    }
}
