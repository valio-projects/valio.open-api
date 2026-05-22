---
title: Output sinks
description: Disk, Zip, Stream, and In-memory sinks.
---

The pipeline returns an `IReadOnlyList<GeneratedFile>` — a list of
`(RelativePath, Content)` records. An `IGenerationOutputSink` decides
*where* those bytes go. Splitting it out keeps the orchestrator free of
I/O.

## Contract

```csharp
public interface IGenerationOutputSink : IAsyncDisposable
{
    Task WriteAsync(IReadOnlyList<GeneratedFile> files, CancellationToken ct = default);
}
```

(Source: `src/ValIO.Output/Abstractions/IGenerationOutputSink.cs`.)

Four implementations ship in `ValIO.Output`:

| Sink                  | Target                                      | Used by    |
|-----------------------|---------------------------------------------|------------|
| `DiskOutputSink`      | A directory tree on disk.                   | CLI, Desktop |
| `ZipFileOutputSink`   | A `.zip` archive on disk.                   | CLI, Desktop |
| `ZipStreamOutputSink` | A `.zip` archive into a caller-owned `Stream`. | Web (HTTP response body) |
| `InMemoryOutputSink`  | An in-memory `Dictionary<string,string>`.   | Tests, IDE plugins |

## Choosing a sink

The CLI maps `--output` and `--format` through `IOutputSinkFactory`:

```csharp
public IGenerationOutputSink Create(OutputTarget target) => target.Kind switch
{
    OutputTargetKind.Disk      => new DiskOutputSink(target.Path!),
    OutputTargetKind.ZipFile   => new ZipFileOutputSink(target.Path!),
    OutputTargetKind.ZipStream => new ZipStreamOutputSink(target.Stream!, target.LeaveStreamOpen),
    OutputTargetKind.InMemory  => new InMemoryOutputSink()
};
```

(Source: `src/ValIO.Output/DefaultOutputSinkFactory.cs`.)

Built-in `OutputTarget` factory methods:

```csharp
OutputTarget.Folder("./out")          // Disk
OutputTarget.ZipPath("./out.zip")     // ZipFile
OutputTarget.ToStream(httpResponse)   // ZipStream
OutputTarget.Memory()                 // InMemory
```

## DiskOutputSink

Writes each file under `RootPath / GeneratedFile.RelativePath`,
creating intermediate directories as needed. Encoding is UTF-8 without
BOM (so Git stops flagging line-ending churn on the first commit).

## ZipFileOutputSink

Streams to a single `.zip` on disk. Overwrites if the file exists.
Parent directory is created automatically.

## ZipStreamOutputSink

Same archive layout as `ZipFileOutputSink`, but writes into a
caller-provided `Stream`. The Web UI uses this to pipe the archive
straight into the HTTP response body:

```csharp
var sink = new ZipStreamOutputSink(httpContext.Response.Body, leaveOpen: true);
await sink.WriteAsync(result.Files, ct);
```

The `leaveOpen` flag matters when the stream is owned by the host
(ASP.NET disposes the response body itself).

## InMemoryOutputSink

Useful in tests, IDE plugins, and the Web UI's preview-before-download
path:

```csharp
var sink = new InMemoryOutputSink();
await sink.WriteAsync(result.Files, ct);
foreach (var (path, content) in sink.Files)
    Console.WriteLine($"-- {path} --\n{content}");
```

## Writing your own sink

Implement `IGenerationOutputSink` and you can ship to anywhere — S3,
a git repository, an HTTP webhook. Register it through DI or call
`new YourSink()` directly; nothing in the orchestrator is sink-aware.

## Where to look

- `src/ValIO.Output/Abstractions/IGenerationOutputSink.cs`
- `src/ValIO.Output/Abstractions/OutputTarget.cs`
- `src/ValIO.Output/Sinks/*.cs`
- `src/ValIO.Output/DefaultOutputSinkFactory.cs`
