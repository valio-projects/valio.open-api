using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Builders;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.IR;

/// <summary>
/// Unit tests for <see cref="IrBuilder"/> — OpenAPI → IR transformation.
/// Uses embedded TestData/*.json to exercise schemas without relying on
/// the full plugin pipeline.
/// </summary>
[TestFixture]
public class IrBuilderTests
{
    private static async Task<IrDocument> BuildIrAsync(string resourceName)
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync(resourceName);
        var converter = new NamingConverter();
        var resolver = new SchemaResolver(doc);
        return new IrBuilder(resolver, converter).BuildFromOpenApi(doc);
    }

    [Test]
    public async Task SimpleSchema_ExtractsModelsClientsAndEnums()
    {
        var ir = await BuildIrAsync("simple-schema.json");

        ir.Models.Should().Contain(m => m.Name == "User");
        ir.Enums.Should().Contain(e => e.Name == "UserRole");
        ir.Clients.SelectMany(c => c.Operations).Select(o => o.MethodName)
            .Should().Contain(new[] { "getUsers", "getUserById" });
    }

    [Test]
    public async Task UserModel_HasExpectedProperties()
    {
        var ir = await BuildIrAsync("simple-schema.json");
        var user = ir.Models.First(m => m.Name == "User");
        var names = user.Properties.Select(p => p.Name).ToList();
        names.Should().Contain(new[] { "id", "name", "email", "role" });
    }

    [Test]
    public async Task RequiredFlag_FollowsOpenApiRequiredList()
    {
        var ir = await BuildIrAsync("simple-schema.json");
        var user = ir.Models.First(m => m.Name == "User");
        var id = user.Properties.First(p => p.Name == "id");
        var role = user.Properties.First(p => p.Name == "role");

        id.IsRequired.Should().BeTrue("id is in `required: [id, name, email]`");
        role.IsRequired.Should().BeFalse("role is not listed as required");
    }

    [Test]
    public async Task EnumExtraction_PreservesValues()
    {
        var ir = await BuildIrAsync("simple-schema.json");
        var userRole = ir.Enums.First(e => e.Name == "UserRole");
        userRole.Values.Select(v => v.Value?.ToString())
            .Should().Contain(new[] { "admin", "user", "guest" });
    }

    [Test]
    public async Task PathOperation_PathParametersCollected()
    {
        var ir = await BuildIrAsync("simple-schema.json");
        var getById = ir.Clients.SelectMany(c => c.Operations)
            .First(o => o.MethodName == "getUserById");

        getById.PathParameters.Should().HaveCount(1);
        getById.PathParameters[0].Name.Should().Be("id");
        getById.PathParameters[0].IsRequired.Should().BeTrue();
    }

    [Test]
    public async Task TypeDependencyGraph_ContainsAllModels()
    {
        var ir = await BuildIrAsync("simple-schema.json");
        ir.TypeDependencies.Should().NotBeNull();
        ir.TypeDependencies.TopologicalOrder.Should().Contain("User");
    }

    [Test]
    public async Task AllOf_FlattensInheritedProperties()
    {
        var ir = await BuildIrAsync("complex-schema.json");
        // Dog allOf AnimalBase + own fields → Dog model must carry the
        // composition or have flattened props.
        var dog = ir.Models.FirstOrDefault(m => m.Name == "Dog");
        dog.Should().NotBeNull();
        var dogContent = dog!.Composition ?? null;
        // Either Composition has AllOf with AnimalBase, or properties include
        // inherited fields.
        var props = dog.Properties.Select(p => p.Name).ToList();
        var inheritedOrComposed = dogContent?.Kind == IrCompositionKind.AllOf
            || props.Contains("kind")
            || props.Contains("id");
        inheritedOrComposed.Should().BeTrue();
    }

    [Test]
    public async Task OneOf_ProducesCompositionOrUnion()
    {
        var ir = await BuildIrAsync("complex-schema.json");
        var animal = ir.Models.FirstOrDefault(m => m.Name == "Animal");
        animal.Should().NotBeNull();
        animal!.Composition?.Kind.Should().Be(IrCompositionKind.OneOf);
        animal.Composition?.Types.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task RecursiveSchema_TreeNodeReferencesItself()
    {
        var ir = await BuildIrAsync("complex-schema.json");
        var tree = ir.Models.First(m => m.Name == "TreeNode");
        var children = tree.Properties.First(p => p.Name == "children");

        // children: TreeNode[] — the type tree should contain a self-reference.
        bool ContainsTreeRef(IrType t) => t switch
        {
            IrModelReference m => m.ModelName.Equals("TreeNode", StringComparison.OrdinalIgnoreCase),
            IrArrayType a => ContainsTreeRef(a.ElementType),
            IrNullableType n => ContainsTreeRef(n.InnerType),
            IrOptionalType o => ContainsTreeRef(o.InnerType),
            _ => false,
        };
        ContainsTreeRef(children.Type).Should().BeTrue();
    }

    [Test]
    public async Task RequestBody_BinaryContentType_PreservedAsBinaryIr()
    {
        var ir = await BuildIrAsync("complex-schema.json");
        // uploadMultipart has multipart/form-data body referencing UploadForm.
        // The IR request body should mark the content type as form-data.
        var op = ir.Clients.SelectMany(c => c.Operations)
            .First(o => o.MethodName == "uploadMultipart");
        op.RequestBody.Should().NotBeNull();
        op.RequestBody!.IsForm.Should().BeTrue();
    }
}
