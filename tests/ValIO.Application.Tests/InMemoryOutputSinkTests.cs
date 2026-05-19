using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Output.Sinks;

namespace ValIO.Application.Tests;

[TestFixture]
public sealed class InMemoryOutputSinkTests
{
    [Test]
    public async Task WriteAsync_StoresAllFiles_KeyedByRelativePath()
    {
        await using var sink = new InMemoryOutputSink();
        await sink.WriteAsync(new[]
        {
            new GeneratedFile("a.txt", "AAA"),
            new GeneratedFile("dir/b.txt", "BBB"),
        });

        sink.Files.Should().HaveCount(2);
        sink.Files["a.txt"].Should().Be("AAA");
        sink.Files["dir/b.txt"].Should().Be("BBB");
    }
}
