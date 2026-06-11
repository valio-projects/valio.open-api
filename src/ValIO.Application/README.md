# ValIO.Application

The orchestration engine that ties the pipeline together. All three frontends (CLI, Web, Desktop) call
into this project; none of them contain generation logic.

## Responsibilities

1. Parse the schema (`ValIO.Parsers`).
2. Build the IR (`ValIO.Core` `IrBuilder`).
3. Resolve the target language plugin via `IGeneratorRegistry.FindPlugin(languageId)`.
4. Run the plugin's `ICodeGenerationPipeline` (its ordered `ICodeGenerationStep`s).
5. Return a `GenerationResult` (`GeneratedFile[]` + warnings) for an `IGenerationOutputSink` to write.

Also hosts the DI wiring (`AddGeneratorPlugin<T>()` and the generator registry).
