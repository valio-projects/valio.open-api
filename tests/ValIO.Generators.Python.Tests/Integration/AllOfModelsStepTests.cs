using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.Python.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.Python.Tests.Integration;

/// <summary>Runs the shared composition contracts against generated Python models.</summary>
[TestFixture]
public sealed class AllOfModelsStepTests : AllOfModelsContractTests
{
    /// <inheritdoc />
    protected override async Task<GenerationContext> GenerateAsync(OpenApiDocument document)
    {
        using var harness = new PythonPipelineTestHarness();
        return await harness.RunAsync(document);
    }

    /// <inheritdoc />
    protected override string StringField(string name) => $"{name}: str";

    /// <inheritdoc />
    protected override string ModelFileName(string modelName) => $"{new NamingConverter().ToSnakeCase(modelName)}.py";

    /// <inheritdoc />
    protected override IReadOnlyList<string> NestedUnionFragments => ["payload: \"list[dict[str, First | Second]]\"", "from .first import First", "from .second import Second"];
}
