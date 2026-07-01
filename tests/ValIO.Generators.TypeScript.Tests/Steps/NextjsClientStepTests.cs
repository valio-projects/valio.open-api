using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Unit tests for <c>NextjsClientStep</c> — emits a server-safe Next.js (App Router) <c>fetch</c>
/// client (<c>{op}.nextjs.ts</c>) per operation, with cache tags + revalidation on cacheable GETs.
/// </summary>
[TestFixture]
public class NextjsClientStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task NoNextjsTransport_NoNextjsFiles()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        ctx.GeneratedFiles.Should().NotContain(f =>
            f.RelativePath.EndsWith(".nextjs.ts", StringComparison.Ordinal));
    }

    [Test]
    public async Task GetOperation_EmitsCacheTag_NextTags_AndRevalidateHelper()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["nextjs"] });

        var getPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getPetById.nextjs.ts", StringComparison.Ordinal));

        getPet.Content.Should().Contain("export const getPetByIdCacheTag = \"getPetById\"");
        getPet.Content.Should().Contain("tags: [getPetByIdCacheTag]");
        getPet.Content.Should().Contain("import { revalidateTag } from \"next/cache\"");
        getPet.Content.Should().Contain("export function revalidateGetPetById");
        // The revalidateTag(tag, "max") call renders with its args on separate lines.
        getPet.Content.Should().Contain("revalidateTag(");
        getPet.Content.Should().Contain("getPetByIdCacheTag,");
        getPet.Content.Should().Contain("\"max\"");
    }

    [Test]
    public async Task NextjsClient_IsServerSafe_NoUseClientDirective()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["nextjs"] });

        var getPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getPetById.nextjs.ts", StringComparison.Ordinal));
        getPet.Content.Should().NotContain("use client");
    }

    [Test]
    public async Task Mutation_IsNotCacheable_NoCacheTagOrNextConfig()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["nextjs"] });

        var addPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("addPet.nextjs.ts", StringComparison.Ordinal));

        addPet.Content.Should().Contain("method: \"POST\"");
        addPet.Content.Should().Contain("JSON.stringify");
        addPet.Content.Should().NotContain("CacheTag");
        addPet.Content.Should().NotContain("revalidateTag");
        addPet.Content.Should().NotContain("next:");
    }

    [Test]
    public async Task DefaultRevalidate_EmitsRevalidateInNextConfig()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["nextjs"],
            NextJs = new TypeScriptNextJsOptions { DefaultRevalidate = 60 },
        });

        var getPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getPetById.nextjs.ts", StringComparison.Ordinal));
        getPet.Content.Should().Contain("revalidate: 60");
    }

    [Test]
    public async Task GenerateRevalidateHelpersFalse_KeepsTagButOmitsHelper()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["nextjs"],
            NextJs = new TypeScriptNextJsOptions { GenerateRevalidateHelpers = false },
        });

        var getPet = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getPetById.nextjs.ts", StringComparison.Ordinal));

        getPet.Content.Should().Contain("export const getPetByIdCacheTag");
        // No helper function and no next/cache import (the cache tag const stays; its JSDoc may still
        // mention revalidateTag, so we assert against the function/import, not the bare word).
        getPet.Content.Should().NotContain("export function revalidate");
        getPet.Content.Should().NotContain("next/cache");
    }
}
