using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

[TestFixture]
public class ZodSchemasStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task Zod3_UsesPlainZodImport()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        var schema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".schema.ts", StringComparison.Ordinal)
            && !f.RelativePath.Contains(".params.schema", StringComparison.Ordinal));
        schema.Content.Should().Contain("from \"zod\"");
        schema.Content.Should().NotContain("from \"zod/v4\"");
    }

    [Test]
    public async Task Zod4_UsesZodV4ImportPath()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod4",
        });

        var schema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith(".schema.ts", StringComparison.Ordinal)
            && !f.RelativePath.Contains(".params.schema", StringComparison.Ordinal));
        schema.Content.Should().Contain("from \"zod/v4\"");
    }

    [Test]
    public async Task ModelSchema_IsColocatedWithModel_AndImportsTypeFromSameDirectory()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        // The schema is co-located with the model under models/user/, next to user.ts, so it imports
        // the interface from the sibling file via "./user" — not an upward ../../models path.
        var userSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/user/user.schema.ts", StringComparison.Ordinal));
        ctx.GeneratedFiles.Should().Contain(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/user/user.ts", StringComparison.Ordinal));
        userSchema.Content.Should().Contain("from \"./user\"");
        userSchema.Content.Should().NotContain("../../models/");
    }

    [Test]
    public async Task DependencyImports_AreCamelCased()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        // Pet schema imports CategorySchema and TagSchema from sibling directories.
        var petSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/pet/pet.schema.ts", StringComparison.Ordinal));
        petSchema.Content.Should().Contain("\"../category/category.schema\"");
        petSchema.Content.Should().Contain("\"../tag/tag.schema\"");
    }

    [Test]
    public async Task EnumSchema_ReusesModelValues_ViaZEnum()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        var enumSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/userRole/userRole.schema.ts", StringComparison.Ordinal));

        // Reuses UserRoleValues from the enum model instead of re-declaring it.
        enumSchema.Content.Should().Contain("import { UserRoleValues } from \"./userRole\"");
        enumSchema.Content.Should().Contain("z.enum(UserRoleValues)");
        enumSchema.Content.Should().NotContain("UserRoleValues = [");
    }

    /// <summary>Verifies named schema variants use exact-one validation without assuming concrete Zod object types.</summary>
    [Test]
    public async Task OneOfWithDiscriminator_UsesSafeExclusiveUnion()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("discriminated-oneof.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod4",
        });

        var petSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/pet/pet.schema.ts", StringComparison.Ordinal));
        petSchema.Content.Should().Contain("z.custom<unknown>(").And.Contain(".safeParse(value).success")
            .And.Contain("z.union([CatSchema, DogSchema])");
        petSchema.Content.Should().NotContain("z.discriminatedUnion(");
    }

    /// <summary>Imports constraint constants only where the generated validator reads them.</summary>
    /// <returns>The asynchronous generated-import check.</returns>
    [Test]
    public async Task ZodSchemas_ImportUsedBoundsAndInlinePatterns()
    {
        var document = await PipelineTestHarness.LoadDocumentFromJsonAsync("""
            {
              "openapi":"3.1.0",
              "info":{"title":"Constraints","version":"1"},
              "paths":{"/items/{id}":{"get":{
                "operationId":"getItem",
                "parameters":[
                  {"in":"path","name":"id","required":true,"schema":{"type":"integer","minimum":1}},
                  {"in":"query","name":"code","schema":{"type":"string","pattern":"^[a-z]+$"}}
                ],
                "responses":{"200":{"description":"Success","content":{"application/json":{"schema":{"$ref":"#/components/schemas/PatternOnly"}}}}}
              }}},
              "components":{"schemas":{
                "PatternOnly":{"type":"object","properties":{"code":{"type":"string","pattern":"^[a-z]+$"}}},
                "Bounded":{"type":"object","properties":{"code":{"type":"string","minLength":2,"pattern":"^[a-z]+$"}}}
              }}
            }
            """);
        var context = await _harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = ["nextjs"],
            ValidationLibrary = "zod4",
        });

        var patternOnly = context.GeneratedFiles.Single(file => file.RelativePath.EndsWith("patternOnly.schema.ts", StringComparison.Ordinal));
        patternOnly.Content.Should().Contain("new RegExp(").And.NotContain("PatternOnlyConstraints");

        var bounded = context.GeneratedFiles.Single(file => file.RelativePath.EndsWith("bounded.schema.ts", StringComparison.Ordinal));
        bounded.Content.Should().Contain("import { BoundedConstraints }").And.Contain("BoundedConstraints.code.minLength");

        var parameters = context.GeneratedFiles.Single(file => file.RelativePath.EndsWith("getItem.params.schema.ts", StringComparison.Ordinal));
        parameters.Content.Should().Contain("GetItemRouteParamsConstraints.id.min").And.NotContain("GetItemQueryParamsConstraints");
    }
}
