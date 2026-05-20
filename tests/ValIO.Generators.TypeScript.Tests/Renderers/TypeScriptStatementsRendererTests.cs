using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Tests.Renderers;

/// <summary>
/// Focused statement renderer unit tests. Each test exercises a single
/// statement visitor in isolation, so a single broken rule produces one
/// failing test (not 20).
/// </summary>
[TestFixture]
public class TypeScriptStatementsRendererTests
{
    private TypeScriptAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new TypeScriptAstRenderer();

    private static IdentifierExpressionNode Id(string n) => new(new IdentifierNode(n));
    private static LiteralExpressionNode Str(string s) => new(s, LiteralKind.String);

    [Test]
    public void Return_NoExpression_BareReturn()
    {
        var ret = new ReturnStatementNode();
        _renderer.VisitReturnStatement(ret).Should().Be("return;");
    }

    [Test]
    public void Return_WithExpression_PrefixesReturn()
    {
        var ret = new ReturnStatementNode(Id("x"));
        _renderer.VisitReturnStatement(ret).Should().Be("return x;");
    }

    [Test]
    public void Throw_PrependsThrow()
    {
        var t = new ThrowStatementNode(
            new CallExpressionNode(
                Id("Error"),
                ImmutableList.Create<ExpressionNode>(Str("nope")),
                isNewExpression: true));
        _renderer.VisitThrowStatement(t).Should().StartWith("throw new Error");
    }

    [Test]
    public void If_RendersConditionAndBlock()
    {
        var stmt = new IfStatementNode(
            condition: Id("ready"),
            thenBlock: new BlockNode(ImmutableList.Create<StatementNode>(
                new ReturnStatementNode(Str("ok")))));
        var rendered = _renderer.VisitIfStatement(stmt);
        rendered.Should().Contain("if (ready) {");
        rendered.Should().Contain("return \"ok\";");
        rendered.Should().Contain("}");
    }

    [Test]
    public void ForEach_RendersForOfHeader()
    {
        var stmt = new ForEachStatementNode(
            variable: new IdentifierNode("u"),
            collection: Id("users"),
            body: new BlockNode(ImmutableList<StatementNode>.Empty));
        _renderer.VisitForEachStatement(stmt).Should().Contain("for (const u of users)");
    }

    [Test]
    public void VariableDeclaration_NoTypeNoInit_OnlyConstName()
    {
        var stmt = new VariableDeclarationStatementNode(
            name: new IdentifierNode("x"));
        _renderer.VisitVariableDeclarationStatement(stmt).Should().Be("const x;");
    }

    [Test]
    public void VariableDeclaration_TypedAndInitialized_RendersBoth()
    {
        var stmt = new VariableDeclarationStatementNode(
            name: new IdentifierNode("count"),
            type: new SimpleTypeNode(new IdentifierNode("number"), isPrimitive: true),
            initializer: new LiteralExpressionNode(0, LiteralKind.Number));
        _renderer.VisitVariableDeclarationStatement(stmt).Should().Be("const count: number = 0;");
    }

    [Test]
    public void ExpressionStatement_AppendsSemicolon()
    {
        var stmt = new ExpressionStatementNode(
            new CallExpressionNode(Id("ping"), ImmutableList<ExpressionNode>.Empty));
        _renderer.VisitExpressionStatement(stmt).Should().Be("ping();");
    }

    [Test]
    public void TryCatch_RendersTryAndCatchBlocks()
    {
        var tryStmt = new TryCatchStatementNode(
            tryBlock: new BlockNode(ImmutableList.Create<StatementNode>(
                new ExpressionStatementNode(
                    new CallExpressionNode(Id("doit"), ImmutableList<ExpressionNode>.Empty)))),
            catchClauses: ImmutableList.Create(new CatchClauseNode(
                exceptionVariable: new IdentifierNode("e"),
                body: new BlockNode(ImmutableList<StatementNode>.Empty))));
        var rendered = _renderer.VisitTryCatchStatement(tryStmt);
        rendered.Should().Contain("try {");
        rendered.Should().Contain("catch (e)");
    }

    [Test]
    public void BlankLine_IsEmptyString()
    {
        _renderer.VisitBlankLineStatement(new BlankLineStatementNode()).Should().Be("");
    }
}
