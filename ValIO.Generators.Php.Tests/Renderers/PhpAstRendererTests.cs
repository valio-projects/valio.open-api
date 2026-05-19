using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Php.Ast.Renderers;

namespace ValIO.Generators.Php.Tests.Renderers;

[TestFixture]
public class PhpAstRendererTests
{
    private PhpAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new PhpAstRenderer();

    [Test]
    public void Identifier_RendersBareName()
    {
        _renderer.VisitIdentifier(new IdentifierNode("getUserById")).Should().Be("getUserById");
    }

    [Test]
    public void Attribute_RendersWithSharpBrackets()
    {
        // PHP 8 attributes use #[AttrName] syntax.
        var attr = new AttributeNode(new IdentifierNode("Override"));
        _renderer.VisitAttribute(attr).Should().Be("#[Override]");
    }

    [Test]
    public void Comment_SingleLine_RendersWithSlashSlashPrefix()
    {
        var c = new CommentNode("hello", CommentKind.SingleLine);
        _renderer.VisitComment(c).Should().StartWith("// ");
    }

    [Test]
    public void SimpleType_RendersBareName()
    {
        var t = new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true);
        _renderer.VisitSimpleType(t).Should().Be("string");
    }

    [Test]
    public void NullableType_PrependsQuestionMark()
    {
        var t = new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true));
        _renderer.VisitNullableType(t).Should().StartWith("?");
    }
}
