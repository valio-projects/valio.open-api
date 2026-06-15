using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Tests.TestHelpers;

namespace ValIO.Core.Tests.IR;

/// <summary>
/// Покрывает модель-уровневую композицию (наследование allOf, oneOf+discriminator)
/// и нормализацию single-element allOf в позициях request/response.
/// </summary>
[TestFixture]
public class CompositionVariantsTests
{
    // 6 — Phase C: inheritance composition holds ONLY the $ref base; own props are not duplicated.
    [Test]
    public async Task Inheritance_CompositionHoldsOnlyBaseRef_NoDuplicateProps()
    {
        var model = await IrTestHarness.BuildSingleModelAsync(
            "Dog",
            """{"allOf":[{"$ref":"#/components/schemas/AnimalBase"},{"type":"object","properties":{"bark":{"type":"string"}}}]}""",
            "3.0.3",
            ("AnimalBase", """{"type":"object","properties":{"name":{"type":"string"}}}"""));

        model.Composition.Should().NotBeNull();
        model.Composition!.Kind.Should().Be(IrCompositionKind.AllOf);
        model.Composition.Types.Should().ContainSingle()
            .Which.Should().BeOfType<IrModelReference>()
            .Which.ModelName.Should().Be("AnimalBase");

        // Own inline prop present; base prop NOT flattened/duplicated into Dog.
        model.Properties.Select(p => p.OriginalName).Should().Contain("bark");
        model.Properties.Select(p => p.OriginalName).Should().NotContain("name");
    }

    // 10 — oneOf + discriminator populates the discriminator metadata on the model composition.
    [Test]
    public async Task OneOf_WithDiscriminator_PopulatesMapping()
    {
        var model = await IrTestHarness.BuildSingleModelAsync(
            "Pet",
            """
            {"oneOf":[{"$ref":"#/components/schemas/Dog"},{"$ref":"#/components/schemas/Cat"}],
             "discriminator":{"propertyName":"kind","mapping":{"dog":"#/components/schemas/Dog","cat":"#/components/schemas/Cat"}}}
            """,
            "3.0.3",
            ("Dog", """{"type":"object","properties":{"bark":{"type":"string"}}}"""),
            ("Cat", """{"type":"object","properties":{"meow":{"type":"string"}}}"""));

        model.Composition.Should().NotBeNull();
        model.Composition!.Kind.Should().Be(IrCompositionKind.OneOf);
        model.Composition.Types.Should().HaveCount(2);
        model.Composition.DiscriminatorPropertyName.Should().Be("kind");
        model.Composition.DiscriminatorMapping.Should().ContainKey("dog");
        model.Composition.DiscriminatorMapping!["dog"].Should().Be("Dog");
    }

    // 13 — degenerate self-wrapping allOf component does not stack-overflow.
    [Test]
    public async Task SelfWrappingAllOf_DoesNotStackOverflow()
    {
        var act = async () => await IrTestHarness.BuildIrFromSchemasAsync(
            "3.0.3",
            ("Node", """{"allOf":[{"$ref":"#/components/schemas/Node"}],"nullable":true}"""));

        var ir = await act.Should().NotThrowAsync();
        ir.Subject.Models.Select(m => m.Name).Should().Contain("Node");
    }

    // 14a — single-element allOf in a response body collapses to a model reference.
    [Test]
    public async Task SingleElementAllOf_InResponseBody_NormalizesToReference()
    {
        const string json = """
        {
          "openapi":"3.0.3",
          "info":{"title":"T","version":"1.0.0"},
          "paths":{
            "/x":{"get":{"operationId":"getX","responses":{"200":{"description":"ok",
              "content":{"application/json":{"schema":{
                "allOf":[{"$ref":"#/components/schemas/Result"}],"description":"wrapped"}}}}}}}
          },
          "components":{"schemas":{"Result":{"type":"object","properties":{"v":{"type":"string"}}}}}
        }
        """;
        var ir = await IrTestHarness.BuildIrAsync(json);
        var op = ir.Clients.SelectMany(c => c.Operations).Single(o => o.MethodName.Contains("getX", StringComparison.OrdinalIgnoreCase) || o.Path == "/x");
        var ok = op.Responses.First(r => r.StatusCode == "200");
        ok.Type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("Result");
    }

    // 14b — single-element allOf in a request body collapses to a model reference.
    [Test]
    public async Task SingleElementAllOf_InRequestBody_NormalizesToReference()
    {
        const string json = """
        {
          "openapi":"3.0.3",
          "info":{"title":"T","version":"1.0.0"},
          "paths":{
            "/x":{"post":{"operationId":"postX",
              "requestBody":{"content":{"application/json":{"schema":{
                "allOf":[{"$ref":"#/components/schemas/CreateForm"}],"description":"wrapped"}}}},
              "responses":{"200":{"description":"ok"}}}}
          },
          "components":{"schemas":{"CreateForm":{"type":"object","properties":{"v":{"type":"string"}}}}}
        }
        """;
        var ir = await IrTestHarness.BuildIrAsync(json);
        var op = ir.Clients.SelectMany(c => c.Operations).Single(o => o.RequestBody != null);
        op.RequestBody!.Type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("CreateForm");
    }
}
