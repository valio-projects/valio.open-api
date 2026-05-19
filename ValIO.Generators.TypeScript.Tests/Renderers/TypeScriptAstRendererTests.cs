using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Tests.Renderers;

/// <summary>
/// Unit tests for <see cref="TypeScriptAstRenderer"/> — emission invariants
/// that don't require the full pipeline.
/// </summary>
[TestFixture]
public class TypeScriptAstRendererTests
{
    private TypeScriptAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new TypeScriptAstRenderer();

    private static SimpleTypeNode Simple(string name, bool isPrimitive = false) =>
        new(new IdentifierNode(name), isPrimitive);

    [Test]
    public void AsyncFunctionWithVoidReturn_WrapsInPromiseVoid()
    {
        var fn = new FunctionDeclarationNode(
            name: new IdentifierNode("noop"),
            returnType: Simple("void", isPrimitive: true),
            parameters: ImmutableList<ParameterDeclarationNode>.Empty,
            body: new BlockNode(ImmutableList<StatementNode>.Empty),
            isArrowFunction: false,
            isExported: true,
            isAsync: true);

        var sourceFile = new SourceFileNode(
            imports: ImmutableList<ImportNode>.Empty,
            declarations: ImmutableList.Create<DeclarationNode>(fn));
        var output = _renderer.Render(sourceFile);

        output.Should().Contain("async function noop");
        output.Should().Contain("Promise<void>");
        output.Should().NotMatchRegex(@"function noop\(\):\s*void");
    }

    [Test]
    public void AsyncFunctionReturningPromise_DoesNotDoubleWrap()
    {
        var promiseString = new GenericTypeNode(new IdentifierNode("Promise"), ImmutableList.Create<TypeNode>(Simple("string", isPrimitive: true)));
        var fn = new FunctionDeclarationNode(
            name: new IdentifierNode("getStr"),
            returnType: promiseString,
            parameters: ImmutableList<ParameterDeclarationNode>.Empty,
            body: new BlockNode(ImmutableList<StatementNode>.Empty),
            isAsync: true,
            isExported: true);

        var sourceFile = new SourceFileNode(
            imports: ImmutableList<ImportNode>.Empty,
            declarations: ImmutableList.Create<DeclarationNode>(fn));
        var output = _renderer.Render(sourceFile);

        output.Should().Contain("Promise<string>");
        output.Should().NotContain("Promise<Promise<string>>");
    }

    [Test]
    public void NonAsyncFunctionVoid_StaysVoid()
    {
        var fn = new FunctionDeclarationNode(
            name: new IdentifierNode("sync"),
            returnType: Simple("void", isPrimitive: true),
            parameters: ImmutableList<ParameterDeclarationNode>.Empty,
            body: new BlockNode(ImmutableList<StatementNode>.Empty),
            isAsync: false,
            isExported: true);

        var output = _renderer.Render(new SourceFileNode(
            imports: ImmutableList<ImportNode>.Empty,
            declarations: ImmutableList.Create<DeclarationNode>(fn)));

        output.Should().Contain(": void");
        output.Should().NotContain("Promise<");
    }

    [Test]
    public void MemberAccess_UseOptionalChaining_RendersWithQuestionDot()
    {
        var member = new MemberAccessExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("params")),
            new IdentifierNode("query"),
            useOptionalChaining: true);

        // Visit the expression directly through the renderer
        var rendered = _renderer.VisitMemberAccessExpression(member);
        rendered.Should().Be("params?.query");
    }

    [Test]
    public void MemberAccess_PlainDot_NoOptionalChaining()
    {
        var member = new MemberAccessExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("params")),
            new IdentifierNode("id"));

        var rendered = _renderer.VisitMemberAccessExpression(member);
        rendered.Should().Be("params.id");
    }

    [Test]
    public void CastExpression_AsOperator_RendersAsKeyword()
    {
        var cast = new CastExpressionNode(
            targetType: Simple("HeadersInit"),
            expression: new IdentifierExpressionNode(new IdentifierNode("h")),
            useAsOperator: true);

        var rendered = _renderer.VisitCastExpression(cast);
        rendered.Should().Contain("as HeadersInit");
    }
}
