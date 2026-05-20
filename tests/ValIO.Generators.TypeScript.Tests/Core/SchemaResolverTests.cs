using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Core;

/// <summary>
/// Unit tests for <see cref="SchemaResolver"/> and
/// <see cref="CachingSchemaResolver"/> — every IrBuilder pass depends on these.
/// </summary>
[TestFixture]
public class SchemaResolverTests
{
    [Test]
    public async Task SchemaResolver_ResolvesInlineSchema_Unchanged()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var resolver = new SchemaResolver(doc);
        // User.properties.id is inline (no $ref) — ResolveSchema should return it as-is.
        var idSchema = doc.Components!.Schemas["User"].Properties["id"];
        var resolved = resolver.ResolveSchema(idSchema);
        resolved.Should().BeSameAs(idSchema);
    }

    [Test]
    public async Task SchemaResolver_ResolvesUserModel_FromComponents()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var resolver = new SchemaResolver(doc);
        var userSchema = doc.Components!.Schemas["User"];
        var resolved = resolver.ResolveSchema(userSchema);
        resolved.Should().NotBeNull();
        resolved.Properties.Should().ContainKey("id");
    }

    [Test]
    public async Task CachingSchemaResolver_DelegatesAndCaches()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var inner = new SchemaResolver(doc);
        var caching = new CachingSchemaResolver(inner);

        var userSchema = doc.Components!.Schemas["User"];
        var first = caching.ResolveSchema(userSchema);
        var second = caching.ResolveSchema(userSchema);

        first.Should().BeSameAs(second, "cached lookup returns the identical instance");
    }

    [Test]
    public async Task CachingSchemaResolver_DifferentSchemas_AreIndependent()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var inner = new SchemaResolver(doc);
        var caching = new CachingSchemaResolver(inner);

        var userResolved = caching.ResolveSchema(doc.Components!.Schemas["User"]);
        var roleResolved = caching.ResolveSchema(doc.Components.Schemas["UserRole"]);

        userResolved.Should().NotBeSameAs(roleResolved);
    }
}
