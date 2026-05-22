---
title: Generation pipeline
description: ICodeGenerationStep, IR shape, and the AST -> renderer flow.
---

Every generator plugin assembles its work into a
`CodeGenerationPipeline` — an ordered list of `ICodeGenerationStep`
instances that share a single `GenerationContext`.

## Step contract

```csharp
public interface ICodeGenerationStep
{
    string Name { get; }
    int Order { get; }
    bool ShouldExecute(GenerationContext context);
    Task ExecuteAsync(GenerationContext context, CancellationToken ct = default);
}
```

(Source: `src/ValIO.Core/Plugins/ICodeGenerationStep.cs`.)

`CodeGenerationPipeline` sorts steps by `Order` ascending and then
invokes each one whose `ShouldExecute` returns `true`. Steps share
state through `context.GeneratedFiles` (the running output list) and
`context.StepData` (an untyped dictionary for cross-step hand-offs).

## Generation context

```csharp
public sealed class GenerationContext
{
    public required IrDocument IrDocument { get; init; }
    public required GenerationConfiguration Configuration { get; init; }
    public object? PluginOptions { get; init; }
    public List<GeneratedFile> GeneratedFiles { get; } = [];
    public Dictionary<string, object> StepData { get; } = [];
}
```

The orchestrator builds this exactly once per call (so the orchestrator
itself stays stateless and safe to share between concurrent callers).

## Three layers: IR → AST → text

ValIO splits generation into three stages explicitly so plugins can
share the parts they care about:

1. **IR (language-agnostic).** `ValIO.Core/IR/IrDocument` contains
   `IrModel`, `IrEnum`, `IrClient`, `IrOperation`, `IrProperty`,
   `IrType` hierarchy, plus constraints (`IrPropertyConstraints`). All
   plugins consume the same IR.
2. **AST (language-specific).** Each plugin builds its own AST nodes
   under `ValIO.Generators.<Lang>/Ast/`. Common shapes (declarations,
   statements, expressions, HTTP nodes) live in
   `ValIO.Core/Ast/Nodes/` and are reused via shared visitors.
3. **Renderer.** A visitor (e.g. `ICSharpAstRenderer`,
   `TypeScriptAstRenderer`) walks the AST and emits a string per
   source file, packaged as a `GeneratedFile(RelativePath, Content)`.

The split lets a contributor add a `Ruby` plugin by writing only new
AST nodes + a renderer; the IR builder, the parser, and the
orchestrator stay untouched.

## Typical step sequence

A typical plugin defines steps along these lines (orders are arbitrary
ints; conventionally spaced by 10):

| Order | Name                       | Effect |
|-------|----------------------------|--------|
| 10    | `BuildModelsStep`          | For each `IrModel`, build AST + render → push `GeneratedFile`. |
| 20    | `BuildEnumsStep`           | Same for enums. |
| 30    | `BuildClientsStep`         | For each `IrClient`, build AST per transport. |
| 40    | `BuildValidationStep`      | (TS only) emit Zod/Valibot/TypeBox schemas. |
| 50    | `BuildQueryWrapperStep`    | (TS only) emit TanStack/SWR hooks. |
| 60    | `BuildMocksStep`           | (TS only) emit MSW handlers. |
| 90    | `BuildIndexStep`           | Emit a barrel `index.ts` / namespace re-export. |

Steps are flagged off via `ShouldExecute` — e.g. `BuildClientsStep`
returns `false` when `Configuration.GenerateClients` is `false`, or
when the user picked zero transports.

## Cancellation

Pipelines honor a `CancellationToken`. The orchestrator passes through
the token from `GenerateAsync`, and every step checks
`ct.ThrowIfCancellationRequested()` at meaningful boundaries (per
file, per model). Web/Desktop callers can cancel a long generation
mid-flight via a UI button.

## Where to look

- `src/ValIO.Core/Plugins/CodeGenerationPipeline.cs`
- `src/ValIO.Core/Plugins/GenerationContext.cs`
- `src/ValIO.Core/IR/IrDocument.cs` (and siblings)
- `src/ValIO.Core/Ast/Nodes/` for shared AST nodes
- `src/ValIO.Generators.TypeScript/` for the largest concrete pipeline
