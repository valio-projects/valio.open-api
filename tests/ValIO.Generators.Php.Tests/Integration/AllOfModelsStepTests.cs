using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.Php.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.Php.Tests.Integration;

/// <summary>Runs the shared composition contracts against generated Php models.</summary>
[TestFixture]
public sealed class AllOfModelsStepTests : AllOfModelsContractTests
{
    /// <inheritdoc />
    protected override async Task<GenerationContext> GenerateAsync(OpenApiDocument document)
    {
        using var harness = new PhpPipelineTestHarness();
        return await harness.RunAsync(document);
    }

    /// <inheritdoc />
    protected override string StringField(string name) => $"string ${name}";

    /// <inheritdoc />
    protected override string ModelFileName(string modelName) => $"{modelName}.php";

    /// <inheritdoc />
    protected override IReadOnlyList<string> NestedUnionFragments => ["array<array<string, First|Second>>", "array $payload"];
}
