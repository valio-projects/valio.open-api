using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.Java.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.Java.Tests.Integration;

/// <summary>Runs the shared composition contracts against generated Java models.</summary>
[TestFixture]
public sealed class AllOfModelsStepTests : AllOfModelsContractTests
{
    /// <inheritdoc />
    protected override async Task<GenerationContext> GenerateAsync(OpenApiDocument document)
    {
        using var harness = new JavaPipelineTestHarness();
        return await harness.RunAsync(document);
    }

    /// <inheritdoc />
    protected override string StringField(string name) => $"String {name}";

    /// <inheritdoc />
    protected override string ModelFileName(string modelName) => $"{modelName}.java";

    /// <inheritdoc />
    protected override IReadOnlyList<string> NestedUnionFragments => ["List<Map<String, Object>> payload", "import java.util.List;", "import java.util.Map;"];
}
