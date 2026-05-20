using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Tests.Renderers;

/// <summary>
/// Lower-level rendering invariants — pins specific renderer outputs so
/// changes anywhere in the renderer chain surface immediately.
/// </summary>
[TestFixture]
public class TypeScriptRendererInvariantsTests
{
    private TypeScriptAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new TypeScriptAstRenderer();

    [Test]
    public void LiteralString_DoubleQuoted()
    {
        var lit = new LiteralExpressionNode("hello", LiteralKind.String);
        _renderer.VisitLiteralExpression(lit).Should().Be("\"hello\"");
    }

    [Test]
    public void LiteralNumber_NoQuotes()
    {
        var lit = new LiteralExpressionNode(42, LiteralKind.Number);
        _renderer.VisitLiteralExpression(lit).Should().Be("42");
    }

    [Test]
    public void LiteralBoolean_LowercaseKeyword()
    {
        _renderer.VisitLiteralExpression(new LiteralExpressionNode(true, LiteralKind.Boolean)).Should().Be("true");
        _renderer.VisitLiteralExpression(new LiteralExpressionNode(false, LiteralKind.Boolean)).Should().Be("false");
    }

    [Test]
    public void LiteralNull_KeywordNull()
    {
        var lit = new LiteralExpressionNode(null, LiteralKind.Null);
        _renderer.VisitLiteralExpression(lit).Should().Be("null");
    }

    [Test]
    public void BinaryNullishCoalescing_RendersDoubleQuestion()
    {
        var n = new BinaryExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("a")),
            BinaryOperator.NullishCoalescing,
            new IdentifierExpressionNode(new IdentifierNode("b")));
        _renderer.VisitBinaryExpression(n).Should().Contain("??");
    }

    [Test]
    public void ConditionalExpression_RendersTernaryWithSpaces()
    {
        var n = new ConditionalExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("c")),
            new IdentifierExpressionNode(new IdentifierNode("a")),
            new IdentifierExpressionNode(new IdentifierNode("b")));
        _renderer.VisitConditionalExpression(n).Should().MatchRegex(@"c\s*\?\s*a\s*:\s*b");
    }

    [Test]
    public void Call_NoArgs_RendersParensOnly()
    {
        var n = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("noop")),
            ImmutableList<ExpressionNode>.Empty);
        _renderer.VisitCallExpression(n).Should().Be("noop()");
    }

    [Test]
    public void Call_WithGeneric_RendersGenericBeforeParen()
    {
        var n = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("useQuery")),
            ImmutableList<ExpressionNode>.Empty,
            genericTypeName: "User");
        _renderer.VisitCallExpression(n).Should().Be("useQuery<User>()");
    }

    [Test]
    public void ArrayLiteral_Empty_RendersBrackets()
    {
        var n = new ArrayLiteralExpressionNode(ImmutableList<ExpressionNode>.Empty);
        _renderer.VisitArrayLiteralExpression(n).Should().Be("[]");
    }

    [Test]
    public void ObjectLiteral_Empty_RendersBraces()
    {
        var n = new ObjectLiteralExpressionNode(ImmutableList<PropertyAssignmentNode>.Empty);
        _renderer.VisitObjectLiteralExpression(n).Should().Contain("{").And.Contain("}");
    }

    [Test]
    public void SpreadProperty_RendersTripleDots()
    {
        var n = new ObjectLiteralExpressionNode(ImmutableList.Create(
            new PropertyAssignmentNode(
                new IdentifierNode("_"),
                new IdentifierExpressionNode(new IdentifierNode("other")),
                isSpread: true)));
        _renderer.VisitObjectLiteralExpression(n).Should().Contain("...other");
    }

    [Test]
    public void TemplateLiteral_RendersBackticks()
    {
        var n = new TemplateLiteralExpressionNode(ImmutableList.Create(
            new TemplateLiteralPart("Hello, "),
            new TemplateLiteralPart(new IdentifierExpressionNode(new IdentifierNode("name"))),
            new TemplateLiteralPart("!")));
        _renderer.VisitTemplateLiteralExpression(n).Should().Be("`Hello, ${name}!`");
    }

    [Test]
    public void UnionType_PipeSeparated()
    {
        var n = new UnionTypeNode(ImmutableList.Create<TypeNode>(
            new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
            new SimpleTypeNode(new IdentifierNode("null"), isPrimitive: true)));
        var ts = new TypeScriptAstRenderer();
        // RenderType is internal but visible to renderer through VisitDefault.
        // We exercise it via a SourceFileNode wrap to keep the test public-API only.
        var sf = new SourceFileNode(
            imports: ImmutableList<ImportNode>.Empty,
            declarations: ImmutableList.Create<DeclarationNode>(
                new TypeAliasDeclarationNode(new IdentifierNode("Nullable"), n)));
        ts.Render(sf).Should().Contain("string | null");
    }
}
