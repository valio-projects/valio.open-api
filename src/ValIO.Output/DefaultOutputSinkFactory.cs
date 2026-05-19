using ValIO.Output.Abstractions;
using ValIO.Output.Sinks;

namespace ValIO.Output;

public sealed class DefaultOutputSinkFactory : IOutputSinkFactory
{
    public IGenerationOutputSink Create(OutputTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Kind switch
        {
            OutputTargetKind.Disk     => new DiskOutputSink(Require(target.Path, nameof(target.Path))),
            OutputTargetKind.ZipFile  => new ZipFileOutputSink(Require(target.Path, nameof(target.Path))),
            OutputTargetKind.ZipStream => new ZipStreamOutputSink(target.Stream ?? throw new ArgumentException("Stream required for ZipStream"), target.LeaveStreamOpen),
            OutputTargetKind.InMemory => new InMemoryOutputSink(),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target.Kind, "Unknown OutputTargetKind")
        };
    }

    private static string Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Path is required for this output kind", name);
        return value;
    }
}
