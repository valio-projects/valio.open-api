namespace ValIO.Core.Plugins;

public interface IGeneratorRegistry
{
    IGeneratorPlugin? FindPlugin(string languageId);
    IReadOnlyList<IGeneratorPlugin> AllPlugins { get; }
}
