using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Generators.Java.Ast.Renderers;

namespace ValIO.Generators.Java.Tests.Renderers;

[TestFixture]
public class JavaAstRendererTests
{
    private JavaAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new JavaAstRenderer();

    [Test]
    public void Identifier_RendersBareName()
    {
        _renderer.VisitIdentifier(new IdentifierNode("UserId")).Should().Be("UserId");
    }

    [Test]
    public void Attribute_RendersWithAtSign()
    {
        var attr = new AttributeNode(new IdentifierNode("Override"));
        _renderer.VisitAttribute(attr).Should().Be("@Override");
    }

    [Test]
    public void Comment_RendersWithDoubleSlashPrefix()
    {
        var c = new CommentNode("hello", CommentKind.SingleLine);
        _renderer.VisitComment(c).Should().StartWith("// ");
        _renderer.VisitComment(c).Should().Contain("hello");
    }
}
