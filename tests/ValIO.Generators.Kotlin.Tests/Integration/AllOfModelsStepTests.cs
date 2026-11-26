using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.Kotlin.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.Kotlin.Tests.Integration;

/// <summary>Runs the shared composition contracts against generated Kotlin models.</summary>
[TestFixture]
public sealed class AllOfModelsStepTests : AllOfModelsContractTests
{
    /// <inheritdoc />
    protected override async Task<GenerationContext> GenerateAsync(OpenApiDocument document)
    {
        using var harness = new KotlinPipelineTestHarness();
        return await harness.RunAsync(document);
    }

    /// <inheritdoc />
    protected override string StringField(string name) => $"val {name}: String";

    /// <inheritdoc />
    protected override string ModelFileName(string modelName) => $"{modelName}.kt";

    /// <inheritdoc />
    protected override IReadOnlyList<string> NestedUnionFragments => ["val payload: List<Map<String, Any>>", "import com.example.generated.models.First", "import com.example.generated.models.Second"];
}
