using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Builders;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.TypeScript.Tests.IR;

/// <summary>
/// Unit tests for <see cref="IrTypeDependencyGraphBuilder"/>. Construct IR
/// models by hand — no OpenAPI document needed — and exercise the graph
/// builder in isolation.
/// </summary>
[TestFixture]
public class IrTypeDependencyGraphBuilderTests
{
    private readonly IrTypeDependencyGraphBuilder _sut = new();

    private static IrPrimitiveType Prim(IrPrimitiveKind k) => new() { PrimitiveKind = k };

    private static IrProperty Prop(string name, IrType type, bool required = true) => new()
    {
        Name = name, OriginalName = name, Type = type, IsRequired = required,
    };

    private static IrModel Model(string name, params IrProperty[] properties) => new()
    {
        Name = name,
        Properties = properties,
    };

    [Test]
    public void EmptyInputs_EmptyGraph()
    {
        var graph = _sut.Build([], []);
        graph.TopologicalOrder.Should().BeEmpty();
        graph.Dependencies.Should().BeEmpty();
    }

    [Test]
    public void SingleModelNoDependencies_AppearsInOrder()
    {
        var graph = _sut.Build([Model("Foo", Prop("id", Prim(IrPrimitiveKind.Integer)))], []);
        graph.TopologicalOrder.Should().ContainSingle().Which.Should().Be("Foo");
        graph.Dependencies["Foo"].Should().BeEmpty();
    }

    [Test]
    public void TwoModelsLinearDependency_DepSortedFirst()
    {
        // Bar { f: Foo } depends on Foo.
        var graph = _sut.Build(
            [
                Model("Foo", Prop("id", Prim(IrPrimitiveKind.Integer))),
                Model("Bar", Prop("f", new IrModelReference { ModelName = "Foo" })),
            ],
            []);

        var order = graph.TopologicalOrder.ToList();
        order.IndexOf("Foo").Should().BeLessThan(order.IndexOf("Bar"));
        graph.Dependencies["Bar"].Should().Contain("Foo");
        graph.Dependencies["Foo"].Should().BeEmpty();
    }

    [Test]
    public void DiamondShape_AllAppearInOrderWithoutDuplicates()
    {
        // A → B, A → C, B → D, C → D
        var graph = _sut.Build(
            [
                Model("D", Prop("v", Prim(IrPrimitiveKind.String))),
                Model("B", Prop("d", new IrModelReference { ModelName = "D" })),
                Model("C", Prop("d", new IrModelReference { ModelName = "D" })),
                Model("A",
                    Prop("b", new IrModelReference { ModelName = "B" }),
                    Prop("c", new IrModelReference { ModelName = "C" })),
            ],
            []);

        var order = graph.TopologicalOrder.ToList();
        order.Should().Contain(new[] { "A", "B", "C", "D" });
        order.Should().OnlyHaveUniqueItems();
        order.IndexOf("D").Should().BeLessThan(order.IndexOf("B"));
        order.IndexOf("D").Should().BeLessThan(order.IndexOf("C"));
        order.IndexOf("B").Should().BeLessThan(order.IndexOf("A"));
        order.IndexOf("C").Should().BeLessThan(order.IndexOf("A"));
    }

    [Test]
    public void Cycle_DoesNotDuplicateOrThrow()
    {
        // Self-recursive TreeNode { children: TreeNode[] }
        var graph = _sut.Build(
            [
                Model("TreeNode",
                    Prop("children", new IrArrayType { ElementType = new IrModelReference { ModelName = "TreeNode" } })),
            ],
            []);

        graph.TopologicalOrder.Should().ContainSingle().Which.Should().Be("TreeNode");
        graph.Dependencies["TreeNode"].Should().Contain("TreeNode");
    }

    [Test]
    public void EnumsArePartOfTheGraph_WithEmptyDeps()
    {
        var graph = _sut.Build(
            [Model("Foo", Prop("id", Prim(IrPrimitiveKind.String)))],
            [new IrEnum { Name = "Status", BaseType = IrEnumBaseType.String, Values = [] }]);

        graph.TopologicalOrder.Should().Contain("Status");
        graph.Dependencies["Status"].Should().BeEmpty();
    }

    [Test]
    public void ExtractDependenciesFromType_RecursesThroughComposites()
    {
        var nested = new IrUnionType
        {
            Types =
            [
                new IrArrayType { ElementType = new IrModelReference { ModelName = "X" } },
                new IrNullableType { InnerType = new IrModelReference { ModelName = "Y" } },
                new IrDictionaryType
                {
                    KeyType = Prim(IrPrimitiveKind.String),
                    ValueType = new IrModelReference { ModelName = "Z" }
                },
            ],
        };

        var deps = _sut.ExtractDependenciesFromType(nested);
        deps.Should().BeEquivalentTo(new[] { "X", "Y", "Z" });
    }

    [Test]
    public void ExtractDependenciesFromType_PrimitivesYieldEmpty()
    {
        _sut.ExtractDependenciesFromType(Prim(IrPrimitiveKind.String)).Should().BeEmpty();
        _sut.ExtractDependenciesFromType(Prim(IrPrimitiveKind.Integer)).Should().BeEmpty();
    }

    [Test]
    public void ExtractDependenciesFromModel_CollectsAllPropertyAndCompositionRefs()
    {
        var model = Model("Composite",
            Prop("a", new IrModelReference { ModelName = "A" }),
            Prop("b", new IrArrayType { ElementType = new IrModelReference { ModelName = "B" } }));

        var deps = _sut.ExtractDependenciesFromModel(model);
        deps.Should().BeEquivalentTo(new[] { "A", "B" });
    }
}
