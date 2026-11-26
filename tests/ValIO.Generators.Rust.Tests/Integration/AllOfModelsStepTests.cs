using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.Rust.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.Rust.Tests.Integration;

/// <summary>Runs the shared composition contracts against generated Rust models.</summary>
[TestFixture]
public sealed class AllOfModelsStepTests : AllOfModelsContractTests
{
    /// <inheritdoc />
    protected override async Task<GenerationContext> GenerateAsync(OpenApiDocument document)
    {
        using var harness = new RustPipelineTestHarness();
        return await harness.RunAsync(document);
    }

    /// <inheritdoc />
    protected override string StringField(string name) => $"pub {name}: String";

    /// <inheritdoc />
    protected override string ModelFileName(string modelName) => $"{new NamingConverter().ToSnakeCase(modelName)}.rs";

    /// <inheritdoc />
    protected override IReadOnlyList<string> NestedUnionFragments => ["pub payload: Vec<std::collections::HashMap<String, serde_json::Value>>"];
}
