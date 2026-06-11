# ValIO.Generators.TypeScript.Tests

Test suite for the [`ValIO.Generators.TypeScript`](../../src/ValIO.Generators.TypeScript) plugin — the
largest suite. NUnit 4 + FluentAssertions.

## Structure

- `Builders/` — builder/renderer **unit** tests (construct a builder, feed small IR, assert AST/output).
- `Steps/`, `Integration/` — **integration** tests via `PipelineTestHarness` (run the plugin against a
  fixture, assert on `GeneratedFile`s).
- `Snapshots/` + `__snapshots__/` — golden-file tests. Regenerate with `UPDATE_SNAPSHOTS=1`.
- `TestData/*.json` — OpenAPI fixtures (embedded resources; add a new `*.json` to introduce one).

```bash
dotnet test tests/ValIO.Generators.TypeScript.Tests
UPDATE_SNAPSHOTS=1 dotnet test tests/ValIO.Generators.TypeScript.Tests --filter "FullyQualifiedName~StepSnapshotTests"
```

See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md) §9 for the full testing playbook.
