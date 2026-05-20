using NUnit.Framework;

namespace ValIO.Generators.Swift.Tests.TestHelpers;

public static class Snapshot
{
    public static void Match(string actual, string snapshotName)
    {
        var snapshotsDir = ResolveSnapshotsDir();
        var snapshotPath = Path.Combine(snapshotsDir, snapshotName + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);

        var update = string.Equals(Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"), "1", StringComparison.Ordinal);

        if (update || !File.Exists(snapshotPath))
        {
            File.WriteAllText(snapshotPath, Normalize(actual));
            Assert.Pass($"Snapshot written: {snapshotPath}");
            return;
        }

        var expected = Normalize(File.ReadAllText(snapshotPath));
        var actualN = Normalize(actual);
        if (expected != actualN)
            Assert.Fail($"Snapshot mismatch for {snapshotName}.\nRun with UPDATE_SNAPSHOTS=1 to regenerate.\n--- expected ---\n{expected}\n--- actual ---\n{actualN}");
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static string ResolveSnapshotsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ValIO.Generators.Swift.Tests.csproj")))
            dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("Swift.Tests source root not found");
        return Path.Combine(dir.FullName, "__snapshots__");
    }
}
