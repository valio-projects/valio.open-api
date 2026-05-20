using Microsoft.Extensions.DependencyInjection;

namespace ValIO.Core.Plugins;

public interface IGeneratorPlugin
{
    string LanguageId { get; }
    string DisplayName { get; }
    IReadOnlyList<string> LanguageAliases { get; }
    IGeneratorOptionsDescriptor OptionsDescriptor { get; }
    ICodeGenerationPipeline CreatePipeline(IServiceProvider services);
    void ConfigureServices(IServiceCollection services);
}
