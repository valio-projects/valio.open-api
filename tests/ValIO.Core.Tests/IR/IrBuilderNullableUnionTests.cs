using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Core.Tests.IR;

/// <summary>Exercises disjoint nullable compositions through the production OpenAPI reader and shared IR.</summary>
[TestFixture]
public sealed class IrBuilderNullableUnionTests
{
    /// <summary>Verifies nullable model and enum identity in properties, collections, bodies and responses.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task ReferencesRetainIdentityAndPresence()
    {
        var ir = await IrTestHarness.BuildIrAsync(OpenApiFixture.Read("CoreIR/nullable-reference-unions-3.1.json"));
        var model = ir.Models.Single(model => model.Name == "NullableReferences");
        foreach (var name in new[] { "country", "state", "optionalCountry", "optionalState" })
        {
            var property = model.Properties.Single(property => property.Name == name);
            var nullable = property.Type.Should().BeOfType<IrNullableType>().Which;
            nullable.InnerType.Should().BeOfType<IrModelReference>().Which.ModelName
                .Should().Be(name.EndsWith("Country", StringComparison.OrdinalIgnoreCase) ? "NullableCountry" : "NullableState");
            property.IsRequired.Should().Be(!name.StartsWith("optional", StringComparison.Ordinal));
        }
        model.Properties.Single(property => property.Name == "countries").Type.Should().BeOfType<IrArrayType>()
            .Which.ElementType.Should().BeOfType<IrNullableType>().Which.InnerType.Should().BeOfType<IrModelReference>();
        model.Properties.Single(property => property.Name == "states").Type.Should().BeOfType<IrDictionaryType>()
            .Which.ValueType.Should().BeOfType<IrNullableType>().Which.InnerType.Should().BeOfType<IrModelReference>();
        var operation = ir.Clients.Single().Operations.Single();
        operation.RequestBody!.Type.Should().BeOfType<IrNullableType>().Which.InnerType.Should().BeOfType<IrModelReference>();
        operation.Responses.Single().Type.Should().BeOfType<IrNullableType>().Which.InnerType.Should().BeOfType<IrModelReference>();
    }

    /// <summary>Checks order independence and support for OpenAPI 3.0, 3.1 and 3.2 composition readers.</summary>
    /// <param name="version">The source document dialect.</param>
    /// <param name="name">The component for an exclusive or inclusive union.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [TestCase("3.0.4", "Exclusive")]
    [TestCase("3.1.2", "Exclusive")]
    [TestCase("3.2.0", "Exclusive")]
    [TestCase("3.1.2", "Inclusive")]
    public async Task DisjointReferencesBecomeNullable(string version, string name)
    {
        var model = await LoadModelAsync(version, name);
        model.Properties.Single().Type.Should().BeOfType<IrNullableType>().Which.InnerType
            .Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("Value");
    }

    /// <summary>Preserves exclusive null match counts when referenced schemas permit null or cannot prove its rejection.</summary>
    /// <param name="name">The component that uses the nullable or unconstrained reference.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [TestCase("NullableObjectChoice")]
    [TestCase("UntypedObjectChoice")]
    [TestCase("NullableStringChoice")]
    [TestCase("ReferencedValueChoice")]
    public async Task NullableOrUnknownReferencesKeepExclusiveBranches(string name)
    {
        var model = await LoadModelAsync("3.1.2", name);
        var union = model.Properties.Single().Type.Should().BeOfType<IrUnionType>().Which;
        union.Kind.Should().Be(IrUnionKind.OneOf);
        union.Types.Should().HaveCount(2);
    }

    /// <summary>Retains duplicate non-null branches and their predicates after extracting a disjoint null branch.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task NonNullMatchCountsAndConstraintsAreRetained()
    {
        var model = await LoadModelAsync("3.1.2", "RepeatedPredicate");
        var union = model.Properties.Single().Type.Should().BeOfType<IrNullableType>().Which.InnerType
            .Should().BeOfType<IrUnionType>().Which;
        union.Kind.Should().Be(IrUnionKind.OneOf);
        union.Types.Should().HaveCount(2);
        union.Types.Should().OnlyContain(type => type.Constraints!.Pattern == "^a");
    }

    /// <summary>Prevents a null-only union from widening to an unconstrained nullable value.</summary>
    /// <param name="name">The component with the null-only composition.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [TestCase("ExclusiveNullOnly")]
    [TestCase("InclusiveNullOnly")]
    public async Task NullOnlyCompositionsStayNullOnly(string name)
    {
        var model = await LoadModelAsync("3.1.2", name);
        model.Properties.Single().Type.Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.Null);
    }
    /// <summary>Reads the selected model from its OpenAPI dialect fixture.</summary>
    /// <param name="version">The OpenAPI version in the fixture.</param>
    /// <param name="name">The component selected for this case.</param>
    /// <returns>The lowered component model.</returns>
    private static async Task<IrModel> LoadModelAsync(string version, string name)
    {
        var ir = await IrTestHarness.BuildIrAsync(OpenApiFixture.Read($"CoreIR/nullable-unions-{version}.json"));
        return ir.Models.Single(model => model.Name == name);
    }
}
