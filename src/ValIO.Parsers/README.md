# ValIO.Parsers

Reads an **OpenAPI 3.x** document (from a URL or a file, JSON or YAML) and exposes it to the rest of
the pipeline via `IOpenApiDocumentParser`. The parsed document is then turned into ValIO's
language-agnostic IR by `IrBuilder` (in `ValIO.Core`).

This is the only project that deals with the raw OpenAPI shape; everything downstream works against the
IR instead.
