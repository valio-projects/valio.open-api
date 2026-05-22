---
title: Parsing & IR construction
description: From OpenAPI bytes to a language-agnostic intermediate representation.
---

ValIO never lets a generator plugin touch raw OpenAPI. The parser
loads the document, a schema resolver follows `$ref` chains, and an
IR builder produces a `IrDocument` that every plugin consumes.

## Parsing

`IOpenApiDocumentParser` accepts a path, URL, or stream:

```csharp
public interface IOpenApiDocumentParser
{
    Task<OpenApiDocument> ParseFromFileAsync(string filePath);
    Task<OpenApiDocument> ParseFromUrlAsync(string url);
    Task<OpenApiDocument> ParseFromStreamAsync(Stream stream, string? format);
}
```

(Source: `src/ValIO.Parsers/Interfaces/IOpenApiDocumentParser.cs`.)

The implementation is a thin wrapper around `Microsoft.OpenApi`'s
`OpenApiDocument.LoadAsync`. It auto-detects format:

- `.json` / `.yaml` / `.yml` from the path's extension.
- Content type from the HTTP response when fetching by URL.
- A peek at the first non-whitespace byte (`{` or `[` ⇒ JSON, else
  YAML) when format is still unknown.

Both OpenAPI 3.0 and 3.1 are accepted. Parsing failures throw
`InvalidOperationException` with the underlying diagnostic; warnings
get logged but don't fail the run.

## Schema resolution

OpenAPI documents are heavy with `$ref`. `ISchemaResolver` flattens
that:

```csharp
public interface ISchemaResolver
{
    IOpenApiSchema ResolveReference(BaseOpenApiReference reference);
    IOpenApiSchema ResolveSchema(IOpenApiSchema schema);
    string GetSchemaName(IOpenApiSchema schema, string defaultName);
}
```

The default implementation is `SchemaResolver` (one-shot resolution).
The orchestrator wraps it in `CachingSchemaResolver` so repeated
lookups during a generation share work. Composition lives in
`ValIO.Application/Services/GenerationOrchestrator.cs`.

## IR construction

`IrBuilder.BuildFromOpenApi(doc, warnings)` produces an `IrDocument`:

- `Metadata` — title, version, description, first server URL.
- `Models` — every `components.schemas` entry that isn't an enum,
  plus inline models hoisted from request/response bodies. Inline
  models get synthesized names.
- `Enums` — schemas whose `enum` keyword is set and whose type is
  `string` or `integer` exclusively.
- `Clients` — one `IrClient` per OpenAPI tag (or per route, depending
  on grouping). Each contains a list of `IrOperation`s with their
  params, request body, and response types.
- `TypeDependencies` — a graph used by plugins that need to emit
  imports/uses in dependency order.

The builder also collects non-fatal warnings (duplicate parameters,
unsupported keywords) into the caller's collection — the orchestrator
forwards them to the front-end's reporter.

## What's downstream

Once `IrDocument` is built, the pipeline takes over. See
[Pipeline](/architecture/pipeline/) for what happens next.

## Where to look

- `src/ValIO.Parsers/Services/OpenApiDocumentParser.cs`
- `src/ValIO.Core/Interfaces/ISchemaResolver.cs`
- `src/ValIO.Core/Services/SchemaResolver.cs` and `CachingSchemaResolver.cs`
- `src/ValIO.Core/IR/Builders/IrBuilder.cs`
- `src/ValIO.Application/Services/GenerationOrchestrator.cs`
