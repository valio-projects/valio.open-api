using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Builders;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.TypeScript.Tests.IR;

/// <summary>
/// Additional dependency-graph edge cases.
/// </summary>
[TestFixture]
public class IrTypeDependencyGraphMoreCasesTests
{
    private readonly IrTypeDependencyGraphBuilder _sut = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };
    private static IrProperty Prop(string name, IrType type) =>
        new() { Name = name, OriginalName = name, Type = type, IsRequired = true };
    private static IrModel Model(string name, params IrProperty[] props) =>
        new() { Name = name, Properties = props };

    [Test]
    public void DependencyOnNonExistentType_DoesntCrash()
    {
        // Foo depends on Bar which doesn't exist in the input.
        var graph = _sut.Build(
            [Model("Foo", Prop("b", new IrModelReference { ModelName = "Bar" }))],
            []);
        // Foo still appears in order; "Bar" reference may or may not be in deps.
        graph.TopologicalOrder.Should().Contain("Foo");
    }

    [Test]
    public void TwoCyclesShareNoNodes_BothProduceValidOrder()
    {
        // A↔B and C↔D.
        var graph = _sut.Build(
            [
                Model("A", Prop("b", new IrModelReference { ModelName = "B" })),
                Model("B", Prop("a", new IrModelReference { ModelName = "A" })),
                Model("C", Prop("d", new IrModelReference { ModelName = "D" })),
                Model("D", Prop("c", new IrModelReference { ModelName = "C" })),
            ],
            []);

        graph.TopologicalOrder.Should().BeEquivalentTo(new[] { "A", "B", "C", "D" });
        graph.TopologicalOrder.Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void Composition_AllOf_AddsParentToDependencies()
    {
        var model = Model("Dog");
        var withComposition = model with
        {
            Composition = new IrComposition
            {
                Kind = IrCompositionKind.AllOf,
                Types =
                [
                    new IrModelReference { ModelName = "AnimalBase" },
                ],
            },
        };
        var deps = _sut.ExtractDependenciesFromModel(withComposition);
        deps.Should().Contain("AnimalBase");
    }

    [Test]
    public void IntersectionType_CollectsAllMemberRefs()
    {
        var type = new IrIntersectionType
        {
            Types =
            [
                new IrModelReference { ModelName = "Mixin1" },
                new IrModelReference { ModelName = "Mixin2" },
                Prim(IrPrimitiveKind.String),
            ],
        };
        var deps = _sut.ExtractDependenciesFromType(type);
        deps.Should().BeEquivalentTo(new[] { "Mixin1", "Mixin2" });
    }

    [Test]
    public void OptionalAroundNullable_StillFindsInnerRef()
    {
        var type = new IrOptionalType
        {
            InnerType = new IrNullableType
            {
                InnerType = new IrModelReference { ModelName = "Pet" },
            },
        };
        var deps = _sut.ExtractDependenciesFromType(type);
        deps.Should().BeEquivalentTo(new[] { "Pet" });
    }
}
