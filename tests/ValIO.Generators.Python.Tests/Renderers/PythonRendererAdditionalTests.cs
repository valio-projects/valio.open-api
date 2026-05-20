using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Python.Ast.Renderers;

namespace ValIO.Generators.Python.Tests.Renderers;

[TestFixture]
public class PythonRendererAdditionalTests
{
    private PythonAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new PythonAstRenderer();

    [Test]
    public void NestedListOfDict_RendersPep585Pattern()
    {
        var innerDict = new GenericTypeNode(new IdentifierNode("dict"),
            ImmutableList.Create<TypeNode>(
                new SimpleTypeNode(new IdentifierNode("str"), isPrimitive: true),
                new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true)));
        var outer = new GenericTypeNode(new IdentifierNode("list"),
            ImmutableList.Create<TypeNode>(innerDict));
        _renderer.VisitGenericType(outer).Should().Be("list[dict[str, int]]");
    }

    [Test]
    public void NullableType_RendersUnionWithNone()
    {
        var n = new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("str"), isPrimitive: true));
        _renderer.VisitNullableType(n).Should().Contain("None").And.Contain("str");
    }

    [Test]
    public void OptionalType_UnwrapsToInner()
    {
        // Python OptionalType emission may be `T | None` or just `T` — verify it
        // includes the inner type.
        var n = new OptionalTypeNode(new SimpleTypeNode(new IdentifierNode("str"), isPrimitive: true));
        _renderer.VisitOptionalType(n).Should().Contain("str");
    }

    [Test]
    public void IntersectionType_FallsBack()
    {
        // Python lacks first-class intersection types; renderer should still
        // produce something readable (no crash).
        var n = new IntersectionTypeNode(ImmutableList.Create<TypeNode>(
            new SimpleTypeNode(new IdentifierNode("A"), isPrimitive: false),
            new SimpleTypeNode(new IdentifierNode("B"), isPrimitive: false)));
        Func<string> act = () => _renderer.VisitIntersectionType(n);
        act.Should().NotThrow();
    }
}
