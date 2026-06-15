using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Tests.TestHelpers;

namespace ValIO.Core.Tests.IR;

/// <summary>
/// Регрессия для нормализации allOf/oneOf/anyOf в IrBuilder
/// (паттерн Swashbuckle UseAllOfToExtendReferenceSchemas: single-element allOf + sibling nullable).
/// До фикса часть кейсов падала/теряла nullable; после — должны проходить.
/// </summary>
[TestFixture]
public class AllOfNormalizationTests
{
    private static readonly (string name, string body) Base =
        ("Base", """{"type":"object","properties":{"id":{"type":"string"}}}""");
    private static readonly (string name, string body) A =
        ("A", """{"type":"object","properties":{"a":{"type":"string"}}}""");
    private static readonly (string name, string body) B =
        ("B", """{"type":"object","properties":{"b":{"type":"string"}}}""");

    /// <summary>Строит модель Holder с единственным свойством <c>value</c> и возвращает его IrProperty.</summary>
    private static async Task<IrProperty> PropAsync(string propBody, params (string name, string body)[] refs)
    {
        var holder = "{\"type\":\"object\",\"properties\":{\"value\":" + propBody + "}}";
        var schemas = new List<(string, string)> { ("Holder", holder) };
        schemas.AddRange(refs);
        var ir = await IrTestHarness.BuildIrFromSchemasAsync("3.0.3", schemas.ToArray());
        var model = ir.Models.Single(m => m.Name == "Holder");
        return model.Properties.Single(p => p.OriginalName == "value");
    }

    private static async Task<IrType> PropTypeAsync(string propBody, params (string name, string body)[] refs)
        => (await PropAsync(propBody, refs)).Type;

    // 1 — single-element allOf of $ref collapses to a bare model reference (not a 1-arm intersection).
    [Test]
    public async Task SingleElementAllOf_OfRef_CollapsesToModelReference()
    {
        var type = await PropTypeAsync("""{"allOf":[{"$ref":"#/components/schemas/Base"}]}""", Base);
        type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("Base");
    }

    // 2 — THE BUG: single-element allOf + nullable → IrNullableType{IrModelReference} (nullable recovered).
    [Test]
    public async Task SingleElementAllOf_WithNullable_BecomesNullableModelReference()
    {
        var type = await PropTypeAsync("""{"allOf":[{"$ref":"#/components/schemas/Base"}],"nullable":true}""", Base);
        type.Should().BeOfType<IrNullableType>()
            .Which.InnerType.Should().BeOfType<IrModelReference>()
            .Which.ModelName.Should().Be("Base");
    }

    // 3 — single-element allOf + description collapses; description survives on the property.
    [Test]
    public async Task SingleElementAllOf_WithDescription_CollapsesAndKeepsDescription()
    {
        var prop = await PropAsync(
            """{"allOf":[{"$ref":"#/components/schemas/Base"}],"description":"Wrapped"}""", Base);
        prop.Type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("Base");
        prop.Description.Should().Be("Wrapped");
    }

    // 4 — empty allOf does not throw and is not turned into an intersection.
    [Test]
    public async Task EmptyAllOf_DoesNotThrow_AndIsNotIntersection()
    {
        var act = async () => await PropTypeAsync("""{"allOf":[]}""");
        await act.Should().NotThrowAsync();
        (await act()).Should().NotBeOfType<IrIntersectionType>();
    }

    // 5 — GUARD: genuine two-ref allOf stays a 2-member intersection.
    [Test]
    public async Task TwoRefAllOf_StaysIntersection_CountTwo()
    {
        var type = await PropTypeAsync(
            """{"allOf":[{"$ref":"#/components/schemas/A"},{"$ref":"#/components/schemas/B"}]}""", A, B);
        type.Should().BeOfType<IrIntersectionType>().Which.Types.Should().HaveCount(2);
    }

    // 7 — single-element oneOf collapses to the inner type.
    [Test]
    public async Task SingleElementOneOf_CollapsesToInner()
    {
        var type = await PropTypeAsync("""{"oneOf":[{"$ref":"#/components/schemas/A"}]}""", A);
        type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("A");
    }

    // 7b — single-element anyOf collapses to the inner type.
    [Test]
    public async Task SingleElementAnyOf_CollapsesToInner()
    {
        var type = await PropTypeAsync("""{"anyOf":[{"$ref":"#/components/schemas/A"}]}""", A);
        type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("A");
    }

    // 8 — single-element oneOf + nullable → nullable wrapper.
    [Test]
    public async Task SingleElementOneOf_WithNullable_BecomesNullable()
    {
        var type = await PropTypeAsync(
            """{"oneOf":[{"$ref":"#/components/schemas/A"}],"nullable":true}""", A);
        type.Should().BeOfType<IrNullableType>()
            .Which.InnerType.Should().BeOfType<IrModelReference>()
            .Which.ModelName.Should().Be("A");
    }

    // 9 — GUARD: genuine multi-arm oneOf stays a union.
    [Test]
    public async Task MultiArmOneOf_StaysUnion_CountTwo()
    {
        var type = await PropTypeAsync(
            """{"oneOf":[{"$ref":"#/components/schemas/A"},{"$ref":"#/components/schemas/B"}]}""", A, B);
        type.Should().BeOfType<IrUnionType>().Which.Types.Should().HaveCount(2);
    }

    // 11 — nested single-element allOf collapses fully (no nested 1-arm intersections).
    [Test]
    public async Task NestedSingleElementAllOf_CollapsesFully()
    {
        var type = await PropTypeAsync(
            """{"allOf":[{"allOf":[{"$ref":"#/components/schemas/Base"}]}]}""", Base);
        type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("Base");
    }

    // 12 — idempotency: nullable wrapper is applied exactly once (no IrNullableType{IrNullableType}).
    [Test]
    public async Task SingleElementAllOf_Nullable_IsNotDoubleWrapped()
    {
        var type = await PropTypeAsync(
            """{"allOf":[{"$ref":"#/components/schemas/Base"}],"nullable":true}""", Base);
        var nullable = type.Should().BeOfType<IrNullableType>().Subject;
        nullable.InnerType.Should().NotBeOfType<IrNullableType>();
        nullable.InnerType.Should().BeOfType<IrModelReference>();
    }
}
