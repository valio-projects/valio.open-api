using System.IO.Compression;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Output.Sinks;

namespace ValIO.Application.Tests;

[TestFixture]
public sealed class ZipStreamOutputSinkTests
{
    [Test]
    public async Task WriteAsync_WritesZip_ToCallerStream_LeavesItOpen()
    {
        using var ms = new MemoryStream();
        var files = new[] { new GeneratedFile("a.txt", "hello") };

        await using (var sink = new ZipStreamOutputSink(ms, leaveOpen: true))
        {
            await sink.WriteAsync(files);
        }

        ms.CanRead.Should().BeTrue("sink was constructed with leaveOpen=true");
        ms.Position = 0;
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
        archive.Entries.Should().ContainSingle().Which.FullName.Should().Be("a.txt");
    }

    [Test]
    public void Ctor_RejectsReadOnlyStream()
    {
        using var readonlyStream = new MemoryStream(new byte[] { 1, 2, 3 }, writable: false);
        Action act = () => _ = new ZipStreamOutputSink(readonlyStream);
        act.Should().Throw<ArgumentException>();
    }
}
