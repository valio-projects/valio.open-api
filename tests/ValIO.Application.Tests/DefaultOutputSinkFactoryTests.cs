using FluentAssertions;
using NUnit.Framework;
using ValIO.Output;
using ValIO.Output.Abstractions;
using ValIO.Output.Sinks;

namespace ValIO.Application.Tests;

[TestFixture]
public sealed class DefaultOutputSinkFactoryTests
{
    [Test]
    public void Create_Disk_ReturnsDiskSink()
    {
        var f = new DefaultOutputSinkFactory();
        f.Create(OutputTarget.Folder(Path.GetTempPath())).Should().BeOfType<DiskOutputSink>();
    }

    [Test]
    public void Create_ZipFile_ReturnsZipFileSink()
    {
        var f = new DefaultOutputSinkFactory();
        f.Create(OutputTarget.ZipPath(Path.Combine(Path.GetTempPath(), "x.zip")))
            .Should().BeOfType<ZipFileOutputSink>();
    }

    [Test]
    public void Create_ZipStream_ReturnsZipStreamSink()
    {
        using var ms = new MemoryStream();
        var f = new DefaultOutputSinkFactory();
        f.Create(OutputTarget.ToStream(ms)).Should().BeOfType<ZipStreamOutputSink>();
    }

    [Test]
    public void Create_InMemory_ReturnsInMemorySink()
    {
        var f = new DefaultOutputSinkFactory();
        f.Create(OutputTarget.Memory()).Should().BeOfType<InMemoryOutputSink>();
    }

    [Test]
    public void Create_DiskWithoutPath_Throws()
    {
        var f = new DefaultOutputSinkFactory();
        Action act = () => f.Create(new OutputTarget { Kind = OutputTargetKind.Disk });
        act.Should().Throw<ArgumentException>();
    }
}
