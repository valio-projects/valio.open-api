using System.Text.Json;

namespace ValIO.Tests.Fixtures;

/// <summary>Reads fixture files relative to the test output directory.</summary>
internal static class FixtureFile
{
    /// <summary>Reads the complete text of a fixture.</summary>
    /// <param name="relativePath">The path relative to the shared fixture directory.</param>
    /// <returns>The stored text.</returns>
    /// <exception cref="IOException">The fixture cannot be read.</exception>
    public static string Read(string relativePath)
    {
        using var stream = Open(relativePath);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Reads a named value from a JSON fixture containing multiple test cases.</summary>
    /// <param name="relativePath">The path to the fixture file.</param>
    /// <param name="caseName">The property that identifies the case.</param>
    /// <returns>The original JSON value, preserving numeric and string spelling.</returns>
    public static string ReadJsonCase(string relativePath, string caseName)
    {
        using var stream = Open(relativePath);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty(caseName).GetRawText();
    }

    /// <summary>Opens a read-only stream over a fixture.</summary>
    /// <param name="relativePath">The path relative to the shared fixture directory.</param>
    /// <returns>The stream owned by the caller.</returns>
    /// <exception cref="ArgumentException">The path points outside the fixture directory.</exception>
    /// <exception cref="FileNotFoundException">The fixture was not copied to the test output directory.</exception>
    public static FileStream Open(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var segments = relativePath.Replace('\\', '/').Split('/');
        if (Path.IsPathRooted(relativePath) || segments.Contains("..", StringComparer.Ordinal))
            throw new ArgumentException("A fixture path must stay within Fixtures.", nameof(relativePath));

        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", Path.Combine(segments));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Build the test project to copy the fixture '{relativePath}'.", path);

        return File.OpenRead(path);
    }
}
