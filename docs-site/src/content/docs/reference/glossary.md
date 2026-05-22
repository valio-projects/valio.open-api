---
title: Glossary
description: Terms used across the ValIO docs.
---

## AST

*Abstract Syntax Tree.* A language-specific tree of nodes
(declarations, statements, expressions, types) that a renderer walks
to emit source text. Each ValIO plugin owns its own AST under
`ValIO.Generators.<Lang>/Ast/`. Common shapes live in
`ValIO.Core/Ast/Nodes/`.

## Frontend

One of the three user-facing surfaces over the shared engine: the CLI
(`ValIO.Cli`), the Web UI (`ValIO.Web`), or the Desktop app
(`ValIO.Desktop`). See [Frontends](/frontends/).

## Generator plugin

An implementation of `IGeneratorPlugin` — one per target language.
Owns a `LanguageId`, an options descriptor, and a pipeline factory.
See [Plugin contract](/architecture/plugin-contract/).

## IR

*Intermediate Representation.* The language-agnostic shape ValIO
builds from a parsed OpenAPI document. Lives in `ValIO.Core/IR/` as
`IrDocument`, `IrModel`, `IrEnum`, `IrClient`, `IrOperation`,
`IrProperty`, and the `IrType` hierarchy. Every plugin consumes the
same IR.

## OpenAPI 3.x

The specification ValIO parses (both 3.0 and 3.1). Parsing is handled
by `Microsoft.OpenApi`. Schemas may be supplied as JSON or YAML.

## Orchestrator

`IGenerationOrchestrator` in `ValIO.Application`. Coordinates parsing,
IR construction, plugin pipeline execution, and result wrapping.
Stateless — safe to invoke concurrently.

## Output sink

An implementation of `IGenerationOutputSink` that takes the
`GeneratedFile` list returned by the pipeline and writes it
somewhere (disk, zip, stream, memory). See
[Output sinks](/architecture/output-sinks/).

## Pipeline

`ICodeGenerationPipeline` — the ordered list of `ICodeGenerationStep`
instances a plugin runs. Each step is gated by `ShouldExecute(context)`
and mutates `context.GeneratedFiles`. See
[Generation pipeline](/architecture/pipeline/).

## Plugin ID / Language ID

The canonical string used by CLI's `-l` / config's `language` to
select a generator. Lower-case, hyphenated where needed
(`typescript`, `csharp`, `fsharp`). Aliases are accepted (`ts`, `cs`,
`py`, …) — see [Plugin IDs](/reference/plugin-ids/).

## Renderer

A visitor that walks an AST and produces source text. Each plugin
defines its own (`TypeScriptAstRenderer`, `CSharpAstRenderer`, …).
The output is wrapped in `GeneratedFile(RelativePath, Content)`
records.

## Schema

In this project, "schema" almost always means an OpenAPI document
(JSON/YAML). When we mean an individual type schema inside the
document, we say "type schema" or refer to the JSON-Schema subset
explicitly.

## Sink

Short for *output sink*. See above.

## Step

A unit of work inside a pipeline. Implements `ICodeGenerationStep`
with a `Name`, an `Order` for sorting, a `ShouldExecute` predicate,
and an `ExecuteAsync` method.

## Transport

A specific HTTP client library backing a generated client. The TS
plugin supports `fetch`, `axios`, `ky`, `undici`, `ofetch`, `got`,
`angular-rxjs`, `angular-promise`, `solid`. Other plugins ship one
canonical transport per language (`OkHttp` for Java/Kotlin,
`httpx` for Python, `Guzzle` for PHP, `URLSession` for Swift, …).
