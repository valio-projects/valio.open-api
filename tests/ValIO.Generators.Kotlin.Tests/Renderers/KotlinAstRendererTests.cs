using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Kotlin.Ast.Renderers;

namespace ValIO.Generators.Kotlin.Tests.Renderers;

[TestFixture]
public class KotlinAstRendererTests
{
    private KotlinAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new KotlinAstRenderer();

    [Test]
    public void SimpleType_RendersBareName()
    {
        var n = new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false);
        _renderer.VisitSimpleType(n).Should().Be("String");
    }

    [Test]
    public void GenericType_RendersAngleBrackets()
    {
        var n = new GenericTypeNode(new IdentifierNode("List"),
            ImmutableList.Create<TypeNode>(new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false)));
        _renderer.VisitGenericType(n).Should().Be("List<String>");
    }

    [Test]
    public void NullableType_RendersWithQuestionMark()
    {
        var n = new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false));
        _renderer.VisitNullableType(n).Should().EndWith("?");
    }
}
