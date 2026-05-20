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
/// Focused expression renderer unit tests. Each test pokes at a single AstNode
/// shape through the renderer's public visit* method to keep failures cheap
/// to diagnose.
/// </summary>
[TestFixture]
public class TypeScriptExpressionsRendererTests
{
    private TypeScriptAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new TypeScriptAstRenderer();

    private static IdentifierExpressionNode Id(string n) =>
        new(new IdentifierNode(n));
    private static LiteralExpressionNode Str(string s) =>
        new(s, LiteralKind.String);
    private static LiteralExpressionNode Num(double n) =>
        new(n, LiteralKind.Number);
    private static LiteralExpressionNode Bool(bool b) =>
        new(b, LiteralKind.Boolean);

    [Test]
    public void Literal_String_Quoted()
    {
        _renderer.VisitLiteralExpression(Str("hi")).Should().Be("\"hi\"");
    }

    [Test]
    public void Literal_String_EscapesQuotes()
    {
        _renderer.VisitLiteralExpression(Str("say \"hi\"")).Should().Contain("\\\"");
    }

    [Test]
    public void Literal_Number_RendersValueOnly()
    {
        _renderer.VisitLiteralExpression(Num(42)).Should().Be("42");
    }

    [Test]
    public void Literal_Boolean_LowercaseKeyword()
    {
        _renderer.VisitLiteralExpression(Bool(true)).Should().Be("true");
        _renderer.VisitLiteralExpression(Bool(false)).Should().Be("false");
    }

    [Test]
    public void Literal_Null_LiteralKeyword()
    {
        var nullLit = new LiteralExpressionNode(null, LiteralKind.Null);
        _renderer.VisitLiteralExpression(nullLit).Should().Be("null");
    }

    [Test]
    public void TemplateLiteral_PureText_NoInterpolation()
    {
        var template = new TemplateLiteralExpressionNode(
            new[] { new TemplateLiteralPart("hello world") });
        _renderer.VisitTemplateLiteralExpression(template).Should().Be("`hello world`");
    }

    [Test]
    public void TemplateLiteral_OneExpression_RendersDollarBraces()
    {
        var template = new TemplateLiteralExpressionNode(new TemplateLiteralPart[]
        {
            new("user-"),
            new(Id("name")),
            new("-end"),
        });
        _renderer.VisitTemplateLiteralExpression(template).Should().Be("`user-${name}-end`");
    }

    [Test]
    public void Conditional_Ternary_RendersWithParens()
    {
        var cond = new ConditionalExpressionNode(Id("ok"), Str("y"), Str("n"));
        _renderer.VisitConditionalExpression(cond).Should().Be("(ok ? \"y\" : \"n\")");
    }

    [Test]
    public void Binary_Equality_TripleEquals()
    {
        var eq = new BinaryExpressionNode(Id("a"), BinaryOperator.Equal, Id("b"));
        _renderer.VisitBinaryExpression(eq).Should().Be("(a === b)");
    }

    [Test]
    public void Binary_NullishCoalescing_DoubleQuestionMark()
    {
        var nc = new BinaryExpressionNode(Id("a"), BinaryOperator.NullishCoalescing, Str("fallback"));
        _renderer.VisitBinaryExpression(nc).Should().Contain("??");
    }

    [Test]
    public void Unary_LogicalNot_PrefixBang()
    {
        var not = new UnaryExpressionNode(UnaryOperator.LogicalNot, Id("done"));
        _renderer.VisitUnaryExpression(not).Should().Be("!done");
    }

    [Test]
    public void Call_NoArgs_RendersParens()
    {
        var call = new CallExpressionNode(Id("ready"), ImmutableList<ExpressionNode>.Empty);
        _renderer.VisitCallExpression(call).Should().Be("ready()");
    }

    [Test]
    public void Call_OneArg_InlineFormat()
    {
        var call = new CallExpressionNode(Id("greet"), ImmutableList.Create<ExpressionNode>(Str("world")));
        _renderer.VisitCallExpression(call).Should().Be("greet(\"world\")");
    }

    [Test]
    public void Call_NewExpression_PrefixesNew()
    {
        var call = new CallExpressionNode(
            Id("Error"),
            ImmutableList.Create<ExpressionNode>(Str("boom")),
            isNewExpression: true);
        _renderer.VisitCallExpression(call).Should().StartWith("new ");
    }

    [Test]
    public void Call_GenericByTypeArg_RendersAngleBrackets()
    {
        var call = new CallExpressionNode(
            Id("parse"),
            ImmutableList.Create<ExpressionNode>(Id("raw")),
            genericTypeArgument: new SimpleTypeNode(new IdentifierNode("User")));
        _renderer.VisitCallExpression(call).Should().Contain("parse<User>");
    }

    [Test]
    public void MemberAccess_Identifier_PlainDot()
    {
        var m = new MemberAccessExpressionNode(Id("obj"), new IdentifierNode("prop"));
        _renderer.VisitMemberAccessExpression(m).Should().Be("obj.prop");
    }

    [Test]
    public void Spread_ExpressionRendersTripleDot()
    {
        var spread = new SpreadExpressionNode(Id("others"));
        _renderer.VisitSpreadExpression(spread).Should().Be("...others");
    }

    [Test]
    public void ObjectLiteral_Empty_BracesOnly()
    {
        var obj = new ObjectLiteralExpressionNode();
        _renderer.VisitObjectLiteralExpression(obj).Should().Be("{}");
    }

    [Test]
    public void ObjectLiteral_AsConst_AppendsAsConst()
    {
        var obj = new ObjectLiteralExpressionNode(
            ImmutableList<PropertyAssignmentNode>.Empty,
            asConstAssertion: true);
        _renderer.VisitObjectLiteralExpression(obj).Should().Be("{} as const");
    }

    [Test]
    public void ObjectLiteral_ShorthandProperty_NameOnly()
    {
        var props = ImmutableList.Create(
            new PropertyAssignmentNode(new IdentifierNode("id"), Id("id"), isShorthand: true));
        var obj = new ObjectLiteralExpressionNode(props);
        _renderer.VisitObjectLiteralExpression(obj).Should().Contain("id").And.NotContain("id: id");
    }

    [Test]
    public void ObjectLiteral_Spread_RendersTripleDot()
    {
        var props = ImmutableList.Create(
            new PropertyAssignmentNode(new IdentifierNode("_"), Id("rest"), isSpread: true));
        var obj = new ObjectLiteralExpressionNode(props);
        _renderer.VisitObjectLiteralExpression(obj).Should().Contain("...rest");
    }

    [Test]
    public void ArrayLiteral_Empty_BracketsOnly()
    {
        var arr = new ArrayLiteralExpressionNode(ImmutableList<ExpressionNode>.Empty);
        _renderer.VisitArrayLiteralExpression(arr).Should().Be("[]");
    }

    [Test]
    public void ArrayLiteral_Numbers_CommaSeparated()
    {
        var arr = new ArrayLiteralExpressionNode(
            ImmutableList.Create<ExpressionNode>(Num(1), Num(2), Num(3)));
        _renderer.VisitArrayLiteralExpression(arr).Should().Be("[1, 2, 3]");
    }

    [Test]
    public void Await_PrefixesAwait()
    {
        var aw = new AwaitExpressionNode(Id("p"));
        _renderer.VisitAwaitExpression(aw).Should().Be("await p");
    }

    [Test]
    public void Cast_AsOperator_KeepsExpr()
    {
        var cast = new CastExpressionNode(
            new SimpleTypeNode(new IdentifierNode("number")),
            Id("x"),
            useAsOperator: true);
        _renderer.VisitCastExpression(cast).Should().Contain("x").And.Contain("as number");
    }
}
