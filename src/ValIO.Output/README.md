# ValIO.Output

Output sinks — where generated files are written. Implements `IGenerationOutputSink` so the engine
stays agnostic of the destination.

- **Disk** — write files to an output directory.
- **Zip (file)** — pack the result into a `.zip` on disk.
- **Zip (stream)** — stream a `.zip` over HTTP (used by `ValIO.Web`).
- **In-memory** — keep files in memory (used by tests and previews).
