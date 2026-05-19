using System.IO.Compression;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Output.Sinks;

namespace ValIO.Application.Tests;

[TestFixture]
public sealed class ZipFileOutputSinkTests
{
    private string _zip = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _zip = Path.Combine(Path.GetTempPath(), "valio-zip-" + Guid.NewGuid().ToString("N") + ".zip");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_zip)) File.Delete(_zip);
    }

    [Test]
    public async Task WriteAsync_CreatesValidZip_WithAllEntries()
    {
        var files = new[]
        {
            new GeneratedFile("models/User.ts", "export interface User {}"),
            new GeneratedFile("api/Users.ts", "export class Users {}"),
        };

        await using (var sink = new ZipFileOutputSink(_zip))
        {
            await sink.WriteAsync(files);
        }

        File.Exists(_zip).Should().BeTrue();

        using var archive = ZipFile.OpenRead(_zip);
        archive.Entries.Should().HaveCount(2);
        archive.Entries.Select(e => e.FullName)
            .Should().BeEquivalentTo("models/User.ts", "api/Users.ts");

        var user = archive.GetEntry("models/User.ts")!;
        using var reader = new StreamReader(user.Open(), Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        content.Should().Be("export interface User {}");
    }

    [Test]
    public async Task WriteAsync_NormalizesWindowsBackslashes()
    {
        var files = new[] { new GeneratedFile(@"models\User.ts", "x") };

        await using (var sink = new ZipFileOutputSink(_zip))
        {
            await sink.WriteAsync(files);
        }

        using var archive = ZipFile.OpenRead(_zip);
        archive.Entries.Single().FullName.Should().Be("models/User.ts");
    }
}
