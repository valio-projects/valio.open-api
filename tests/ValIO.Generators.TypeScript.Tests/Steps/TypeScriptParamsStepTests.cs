using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Unit tests for <c>TypeScriptParamsStep</c> — generates
/// <c>{operation}.params.ts</c> next to every operation client file.
/// </summary>
[TestFixture]
public class TypeScriptParamsStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task PathParam_FlatInterface_NoRouteWrapper()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var paramsFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.EndsWith("getUserById.params.ts", StringComparison.Ordinal));
        paramsFile.Should().NotBeNull("getUserById has a path parameter; params file must exist");

        // Single param group → flat layout. No `route:` wrapper, just `id`.
        paramsFile!.Content.Should().Contain("interface GetUserByIdParams");
        paramsFile.Content.Should().Contain("id:");
        paramsFile.Content.Should().NotContain("route:");
    }

    [Test]
    public async Task NoParameters_DoesNotGenerateParamsFile()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        // getUsers has no params → no params.ts emitted.
        var paramsFile = ctx.GeneratedFiles.FirstOrDefault(f =>
            f.RelativePath.EndsWith("getUsers.params.ts", StringComparison.Ordinal));
        paramsFile.Should().BeNull();
    }

    [Test]
    public async Task AggregateLayout_WithMultipleGroups_HasRouteAndQueryFields()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        // findPetsByStatus is single-param-group (query only) — flat.
        // updatePetWithForm has both route param and query params → aggregate.
        var updateForm = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("updatePetWithForm.params.ts", StringComparison.Ordinal));

        updateForm.Content.Should().Contain("interface UpdatePetWithFormRouteParams");
        updateForm.Content.Should().Contain("interface UpdatePetWithFormQueryParams");
        updateForm.Content.Should().Contain("interface UpdatePetWithFormParams");
        updateForm.Content.Should().Contain("route:");
        updateForm.Content.Should().Contain("query?:");
    }

    [Test]
    public async Task BinaryRequestBody_AppearsAsBodyField()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        // uploadFile takes application/octet-stream → body field must exist.
        var uploadParams = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("uploadFile.params.ts", StringComparison.Ordinal));
        uploadParams.Content.Should().Contain("body");
    }

    [Test]
    public async Task BodyDependencies_ImportedRecursively()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        // createUsersWithListInput receives `User[]` as body. The params file
        // must `import type { User }` even though the body's top-level
        // IR type is IrArrayType (User is nested inside).
        var listInput = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("createUsersWithListInput.params.ts", StringComparison.Ordinal));
        listInput.Content.Should().Contain("import type { User }");
    }
}
