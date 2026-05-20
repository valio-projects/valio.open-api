using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Output.Sinks;

namespace ValIO.Application.Tests;

[TestFixture]
public sealed class DiskOutputSinkTests
{
    private string _temp = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _temp = Path.Combine(Path.GetTempPath(), "valio-disk-" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
    }

    [Test]
    public async Task WriteAsync_WritesFiles_AndCreatesSubdirectories()
    {
        var files = new[]
        {
            new GeneratedFile("models/User.ts", "export interface User {}\n"),
            new GeneratedFile("api/UsersApi.ts", "export class UsersApi {}\n"),
            new GeneratedFile("index.ts", "export * from './models';\n"),
        };

        await using (var sink = new DiskOutputSink(_temp))
        {
            await sink.WriteAsync(files);
        }

        File.Exists(Path.Combine(_temp, "models", "User.ts")).Should().BeTrue();
        File.Exists(Path.Combine(_temp, "api", "UsersApi.ts")).Should().BeTrue();
        File.Exists(Path.Combine(_temp, "index.ts")).Should().BeTrue();

        (await File.ReadAllTextAsync(Path.Combine(_temp, "models", "User.ts")))
            .Should().Be("export interface User {}\n");
    }

    [Test]
    public void Ctor_RejectsEmptyPath()
    {
        Action act = () => _ = new DiskOutputSink("");
        act.Should().Throw<ArgumentException>();
    }
}
