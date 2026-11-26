using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.Swift.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.Swift.Tests.Integration;

/// <summary>Runs the shared composition contracts against generated Swift models.</summary>
[TestFixture]
public sealed class AllOfModelsStepTests : AllOfModelsContractTests
{
    /// <inheritdoc />
    protected override async Task<GenerationContext> GenerateAsync(OpenApiDocument document)
    {
        using var harness = new SwiftPipelineTestHarness();
        return await harness.RunAsync(document);
    }

    /// <inheritdoc />
    protected override string StringField(string name) => $"public let {name}: String";

    /// <inheritdoc />
    protected override string ModelFileName(string modelName) => $"{modelName}.swift";

    /// <inheritdoc />
    protected override IReadOnlyList<string> NestedUnionFragments => ["public let payload: [[String: AnyCodable]]"];
}
