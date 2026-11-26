using System.Reflection;

namespace ValIO.Core.Plugins;

/// <summary>Loads a fixed UTF-8 artifact packaged together with its generator.</summary>
public static class SourceResource
{
    /// <summary>Reads an embedded text file without substitutions or newline changes.</summary>
    /// <param name="assembly">The generator assembly that owns the artifact.</param>
    /// <param name="name">The embedded resource's fully qualified logical name.</param>
    /// <returns>The original UTF-8 text.</returns>
    /// <exception cref="InvalidOperationException">The generator package does not include the required resource.</exception>
    public static string Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The generator package is missing '{name}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
