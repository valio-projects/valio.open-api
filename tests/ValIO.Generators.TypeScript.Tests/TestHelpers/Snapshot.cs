using NUnit.Framework;

namespace ValIO.Generators.TypeScript.Tests.TestHelpers;

/// <summary>
/// Minimal golden-file ("snapshot") helper. Compares <paramref name="actual"/>
/// against a file stored at <c>__snapshots__/{snapshotName}.txt</c> relative to
/// the source file that's calling. Set the <c>UPDATE_SNAPSHOTS</c> env var to
/// <c>1</c> to (re)write the snapshot.
///
/// Usage:
/// <code>
/// Snapshot.Match(generatedTypeScript, "TypeScriptModelsStep/userInterface");
/// </code>
/// </summary>
public static class Snapshot
{
    public static void Match(string actual, string snapshotName)
    {
        var snapshotsDir = ResolveSnapshotsDir();
        var snapshotPath = Path.Combine(snapshotsDir, snapshotName + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);

        var update = string.Equals(
            Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"),
            "1", StringComparison.Ordinal);

        if (update || !File.Exists(snapshotPath))
        {
            File.WriteAllText(snapshotPath, NormalizeLineEndings(actual));
            // First-write counts as a passing test — no expected file existed yet.
            Assert.Pass($"Snapshot written: {snapshotPath}");
            return;
        }

        var expected = NormalizeLineEndings(File.ReadAllText(snapshotPath));
        var actualNormalized = NormalizeLineEndings(actual);

        if (expected != actualNormalized)
        {
            Assert.Fail($"Snapshot mismatch for {snapshotName}.\n" +
                        $"Run with UPDATE_SNAPSHOTS=1 to regenerate.\n" +
                        $"--- expected ---\n{expected}\n" +
                        $"--- actual ---\n{actualNormalized}");
        }
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static string ResolveSnapshotsDir()
    {
        // Walk up from the test binary to find ValIO.Generators.TypeScript.Tests source root.
        var binDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(binDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ValIO.Generators.TypeScript.Tests.csproj")))
        {
            dir = dir.Parent;
        }
        if (dir == null)
            throw new InvalidOperationException("Couldn't locate ValIO.Generators.TypeScript.Tests source root from " + binDir);
        return Path.Combine(dir.FullName, "__snapshots__");
    }
}
