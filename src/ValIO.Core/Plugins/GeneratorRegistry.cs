namespace ValIO.Core.Plugins;

public sealed class GeneratorRegistry : IGeneratorRegistry
{
    private readonly IReadOnlyList<IGeneratorPlugin> _plugins;
    private readonly Dictionary<string, IGeneratorPlugin> _byId;

    public IReadOnlyList<IGeneratorPlugin> AllPlugins => _plugins;

    public GeneratorRegistry(IEnumerable<IGeneratorPlugin> plugins)
    {
        _plugins = [.. plugins];
        _byId = new Dictionary<string, IGeneratorPlugin>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _plugins)
        {
            _byId[p.LanguageId] = p;
            foreach (var alias in p.LanguageAliases)
                _byId[alias] = p;
        }
    }

    public IGeneratorPlugin? FindPlugin(string languageId) =>
        _byId.TryGetValue(languageId, out var p) ? p : null;
}
