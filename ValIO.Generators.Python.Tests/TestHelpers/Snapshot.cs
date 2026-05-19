using NUnit.Framework;

namespace ValIO.Generators.Python.Tests.TestHelpers;

/// <summary>
/// Minimal golden-file ("snapshot") helper. Compares actual against
/// <c>__snapshots__/{snapshotName}.txt</c>. Set <c>UPDATE_SNAPSHOTS=1</c>
/// to (re)write the snapshot.
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
        var binDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(binDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ValIO.Generators.Python.Tests.csproj")))
        {
            dir = dir.Parent;
        }
        if (dir == null)
            throw new InvalidOperationException("Couldn't locate Python.Tests source root from " + binDir);
        return Path.Combine(dir.FullName, "__snapshots__");
    }
}
