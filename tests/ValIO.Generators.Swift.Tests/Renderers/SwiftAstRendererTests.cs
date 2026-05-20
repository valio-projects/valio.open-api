using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Swift.Ast.Renderers;

namespace ValIO.Generators.Swift.Tests.Renderers;

[TestFixture]
public class SwiftAstRendererTests
{
    private SwiftAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new SwiftAstRenderer();

    [Test]
    public void SimpleType_RendersBareName()
    {
        var n = new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false);
        _renderer.VisitSimpleType(n).Should().Be("String");
    }

    [Test]
    public void GenericType_RendersAngleBrackets()
    {
        var n = new GenericTypeNode(new IdentifierNode("Array"),
            ImmutableList.Create<TypeNode>(new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false)));
        var rendered = _renderer.VisitGenericType(n);
        rendered.Should().Contain("String");
    }

    [Test]
    public void NullableType_RendersWithQuestionMark()
    {
        var n = new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false));
        _renderer.VisitNullableType(n).Should().EndWith("?");
    }
}
