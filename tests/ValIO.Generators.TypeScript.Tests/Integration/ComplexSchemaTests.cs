using FluentAssertions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>
/// Integration tests that exercise the pipeline against
/// <c>TestData/complex-schema.json</c> — designed to cover edge cases that
/// Petstore doesn't: oneOf polymorphism, allOf inheritance, recursive
/// references, dictionary-of-objects, and multipart with binary fields.
/// </summary>
[TestFixture]
public class ComplexSchemaTests
{
    private PipelineTestHarness _harness = null!;
    private OpenApiDocument _document = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _harness = new PipelineTestHarness();
        _document = await PipelineTestHarness.LoadDocumentAsync("complex-schema.json");
    }

    [OneTimeTearDown]
    public void OneTimeTeardown() => _harness.Dispose();

    [Test]
    public async Task RecursiveModel_ZodSchemaUsesLazy()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        var treeSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("schemas/treeNode/treeNode.schema.ts", StringComparison.Ordinal));
        treeSchema.Content.Should().Contain("z.lazy");
    }

    [Test]
    public async Task RecursiveModel_ValibotAnnotatesWithGenericSchema()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "valibot",
        });

        var treeSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("schemas/treeNode/treeNode.schema.ts", StringComparison.Ordinal));
        // Without this annotation TS infers `any` because v.lazy(...) breaks inference.
        treeSchema.Content.Should().Contain("v.GenericSchema<TreeNode>");
        treeSchema.Content.Should().Contain("import type { TreeNode }");
        treeSchema.Content.Should().Contain("v.lazy(() => TreeNodeSchema)");
    }

    [Test]
    public async Task RecursiveModel_TypeBoxWrapsInTypeRecursive()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "typebox",
        });

        var treeSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("schemas/treeNode/treeNode.schema.ts", StringComparison.Ordinal));
        // Type.Recursive((This) => Type.Object({..., children: Type.Array(This)}))
        treeSchema.Content.Should().Contain("Type.Recursive");
        treeSchema.Content.Should().MatchRegex(@"\(This\)\s*=>");
        treeSchema.Content.Should().Contain("Type.Array(This)");
    }

    [Test]
    public async Task BinaryProperty_ZodUsesInstanceOf()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        var uploadFormSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("schemas/uploadForm/uploadForm.schema.ts", StringComparison.Ordinal));
        // Required binary field needs z.instanceof(Blob) — z.any() infers as
        // `any | undefined` and breaks the required match against `Blob`.
        uploadFormSchema.Content.Should().Contain("z.instanceof(Blob)");
        uploadFormSchema.Content.Should().NotContain("z.any()");
    }

    [Test]
    public async Task MultipartFormBody_FetchCastsToBodyInit()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
        });

        var uploadFetch = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("uploadMultipart.fetch.ts", StringComparison.Ordinal));
        uploadFetch.Content.Should().Contain("as BodyInit");
    }

    [Test]
    public async Task MultipartFormBody_KyCastsToOptionsBody()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["ky"],
        });

        var uploadKy = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("uploadMultipart.ky.ts", StringComparison.Ordinal));
        uploadKy.Content.Should().Contain("Options[\"body\"]");
    }

    [Test]
    public async Task OneOfBody_ImportsAllVariants()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
        });

        // Animal is oneOf [Dog, Cat, Bird] → at minimum, the model file must
        // import each variant. The actual union-type emission may follow in
        // a future fix; this asserts the dependency surface so we notice
        // regressions in import resolution.
        var animal = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/animal/animal.ts", StringComparison.Ordinal));
        animal.Content.Should().Contain("Dog");
        animal.Content.Should().Contain("Cat");
        animal.Content.Should().Contain("Bird");
    }

    [Test]
    public async Task AllOfInheritance_DogInheritsFromAnimalBase()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
        });

        var dog = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/dog/dog.ts", StringComparison.Ordinal));
        // allOf merges parent props — Dog should reference AnimalBase or contain its props.
        dog.Content.Should().Match(c => c!.Contains("AnimalBase") || (c.Contains("id") && c.Contains("kind")));
    }

    [Test]
    public async Task DictionaryOfModels_GeneratesRecordType()
    {
        var ctx = await _harness.RunAsync(_document, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
        });

        // getDictionary returns Record<string, Animal>. Verify results.ts mentions
        // Record<...>.
        var resultsFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getDictionary.results.ts", StringComparison.Ordinal));
        resultsFile.Content.Should().Contain("Record<");
        resultsFile.Content.Should().Contain("Animal");
    }
}
