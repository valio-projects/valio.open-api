using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.FSharp.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.FSharp.Tests.Integration;

/// <summary>Runs the shared composition contracts against generated FSharp models.</summary>
[TestFixture]
public sealed class AllOfModelsStepTests : AllOfModelsContractTests
{
    /// <inheritdoc />
    protected override async Task<GenerationContext> GenerateAsync(OpenApiDocument document)
    {
        using var harness = new FSharpPipelineTestHarness();
        return await harness.RunAsync(document);
    }

    /// <inheritdoc />
    protected override string StringField(string name) => $"{new NamingConverter().ToPascalCase(name)}: string";

    /// <inheritdoc />
    protected override string ModelFileName(string modelName) => $"{modelName}.fs";

    /// <inheritdoc />
    protected override IReadOnlyList<string> NestedUnionFragments => ["Payload: Map<string, obj> list"];
}
