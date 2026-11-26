using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.Dart.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.Dart.Tests.Integration;

/// <summary>Runs the shared composition contracts against generated Dart models.</summary>
[TestFixture]
public sealed class AllOfModelsStepTests : AllOfModelsContractTests
{
    /// <inheritdoc />
    protected override async Task<GenerationContext> GenerateAsync(OpenApiDocument document)
    {
        using var harness = new DartPipelineTestHarness();
        return await harness.RunAsync(document);
    }

    /// <inheritdoc />
    protected override string StringField(string name) => $"final String {name};";

    /// <inheritdoc />
    protected override string ModelFileName(string modelName) => $"{new NamingConverter().ToSnakeCase(modelName)}.dart";

    /// <inheritdoc />
    protected override IReadOnlyList<string> NestedUnionFragments => ["final List<Map<String, dynamic>> payload;"];
}
