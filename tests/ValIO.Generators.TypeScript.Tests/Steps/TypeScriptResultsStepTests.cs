using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Unit tests for <c>TypeScriptResultsStep</c> — generates
/// <c>{operation}.results.ts</c> with a discriminated union over status codes.
/// </summary>
[TestFixture]
public class TypeScriptResultsStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task SingleSuccessResponse_GeneratesResultUnionWithDefaultVariant()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var resultsFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.EndsWith("getUserById.results.ts", StringComparison.Ordinal));
        resultsFile.Should().NotBeNull();

        var content = resultsFile!.Content;
        content.Should().Contain("interface GetUserByIdResult200");
        content.Should().Contain("interface GetUserByIdResultOther");
        // Default union-alias suffix is "Results"; variant interfaces keep the fixed "Result" base.
        content.Should().Contain("type GetUserByIdResults");
        content.Should().Contain("isSuccessful:");
        content.Should().Contain("isFailed:");
    }

    [Test]
    public async Task ResultTypeSuffix_RenamesUnionAliasOnly_VariantsKeepResultBase()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ResultTypeSuffix = "Response",
        });

        var resultsFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUserById.results.ts", StringComparison.Ordinal));

        var content = resultsFile.Content;
        // The union alias adopts the configured suffix...
        content.Should().Contain("type GetUserByIdResponse =");
        content.Should().NotContain("type GetUserByIdResult ");
        content.Should().NotContain("type GetUserByIdResults");
        // ...while the per-status variant interfaces keep the fixed "Result" base.
        content.Should().Contain("interface GetUserByIdResult200");
        content.Should().Contain("interface GetUserByIdResultOther");
    }

    [Test]
    public async Task SuccessVariant_FlagsIsSuccessfulTrue()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var resultsFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUserById.results.ts", StringComparison.Ordinal));

        // 200 variant has literal type `isSuccessful: true`, never `false`.
        resultsFile.Content.Should().MatchRegex(@"GetUserByIdResult200[\s\S]+isSuccessful:\s*true");
    }

    [Test]
    public async Task ResponsesReferencingArrayOfModels_ImportModelType()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        // findPetsByStatus returns `Pet[]`. results.ts must import `Pet`
        // even though the IR type is IrArrayType(IrModelReference("Pet")) —
        // dependency extraction must recurse.
        var resultsFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("findPetsByStatus.results.ts", StringComparison.Ordinal));
        resultsFile.Content.Should().Contain("import type { Pet }");
    }

    [Test]
    public async Task EmptyResponse_GeneratesVoidAlias()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        // deletePet / deleteOrder respond without a body in any schema-defined status.
        // (Petstore actually has no body responses for delete — the Step skips it.)
        // Hence no `deletePet.results.ts` should be generated.
        var deleteResults = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.EndsWith("deletePet.results.ts", StringComparison.Ordinal));
        deleteResults.Should().BeNull("delete operations without typed responses should not emit results files");
    }
}
