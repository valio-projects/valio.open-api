using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Core.Services;
using ValIO.Generators.Ruby.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.Ruby.Tests.Integration;

/// <summary>Runs the shared composition contracts against generated Ruby models.</summary>
[TestFixture]
public sealed class AllOfModelsStepTests : AllOfModelsContractTests
{
    /// <inheritdoc />
    protected override async Task<GenerationContext> GenerateAsync(OpenApiDocument document)
    {
        using var harness = new RubyPipelineTestHarness();
        return await harness.RunAsync(document);
    }

    /// <inheritdoc />
    protected override string StringField(string name) => $"attr_accessor :{name}";

    /// <inheritdoc />
    protected override string ModelFileName(string modelName) => $"{new NamingConverter().ToSnakeCase(modelName)}.rb";

    /// <inheritdoc />
    protected override IReadOnlyList<string> NestedUnionFragments => ["attr_accessor :payload", "# @return [Array<Hash<String, Object>>]"];
}
