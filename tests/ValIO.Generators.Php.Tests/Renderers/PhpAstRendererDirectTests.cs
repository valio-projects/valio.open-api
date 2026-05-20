using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Php.Ast.Renderers;

namespace ValIO.Generators.Php.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="PhpAstRenderer"/>: type rendering,
/// literal rendering, and a couple of trivial pass-throughs.
/// </summary>
[TestFixture]
public class PhpAstRendererDirectTests
{
    private PhpAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new PhpAstRenderer();

    private static SimpleTypeNode Simple(string n) =>
        new(new IdentifierNode(n));

    [Test]
    public void SimpleType_RendersAsName()
    {
        _renderer.VisitSimpleType(Simple("string")).Should().Be("string");
    }

    [Test]
    public void GenericType_DropsTypeArgs_PhpHasNoRuntimeGenerics()
    {
        var t = new GenericTypeNode(new IdentifierNode("Collection"),
            ImmutableList.Create<TypeNode>(Simple("Pet")));
        // PHP runtime has no generics; renderer emits just the base name.
        _renderer.VisitGenericType(t).Should().Be("Collection");
    }

    [Test]
    public void NullableType_PrependsQuestionMark()
    {
        var t = new NullableTypeNode(Simple("string"));
        _renderer.VisitNullableType(t).Should().Be("?string");
    }

    [Test]
    public void NullableType_NestedNullable_DoesNotDoublePrefix()
    {
        // Already-nullable inner (rare but defensive): wrapping the outer
        // again should still produce a single ? prefix.
        var t = new NullableTypeNode(new NullableTypeNode(Simple("int")));
        _renderer.VisitNullableType(t).Should().Be("?int");
    }

    [Test]
    public void OptionalType_StripsWrapper()
    {
        var t = new OptionalTypeNode(Simple("int"));
        _renderer.VisitOptionalType(t).Should().Be("int");
    }

    [Test]
    public void ArrayType_ReturnsArray()
    {
        var t = new ArrayTypeNode(Simple("Pet"));
        _renderer.VisitArrayType(t).Should().Be("array");
    }

    [Test]
    public void DictionaryType_AlsoArray()
    {
        var t = new DictionaryTypeNode(Simple("string"), Simple("int"));
        _renderer.VisitDictionaryType(t).Should().Be("array");
    }

    [Test]
    public void UnionType_PipeJoined()
    {
        var t = new UnionTypeNode(ImmutableList.Create<TypeNode>(Simple("string"), Simple("int")));
        _renderer.VisitUnionType(t).Should().Be("string|int");
    }

    [Test]
    public void IntersectionType_AmpersandJoined()
    {
        var t = new IntersectionTypeNode(ImmutableList.Create<TypeNode>(Simple("Animal"), Simple("Furry")));
        _renderer.VisitIntersectionType(t).Should().Be("Animal&Furry");
    }

    [Test]
    public void Literal_String_SingleQuoted()
    {
        var lit = new LiteralExpressionNode("hi", LiteralKind.String);
        _renderer.VisitLiteralExpression(lit).Should().Be("'hi'");
    }

    [Test]
    public void Literal_Number_RendersValue()
    {
        var lit = new LiteralExpressionNode(42, LiteralKind.Number);
        _renderer.VisitLiteralExpression(lit).Should().Be("42");
    }

    [Test]
    public void Literal_BooleanTrue_LowercaseTrue()
    {
        var lit = new LiteralExpressionNode(true, LiteralKind.Boolean);
        _renderer.VisitLiteralExpression(lit).Should().Be("true");
    }

    [Test]
    public void Literal_Null_LowercaseNull()
    {
        var lit = new LiteralExpressionNode(null, LiteralKind.Null);
        _renderer.VisitLiteralExpression(lit).Should().Be("null");
    }
}
