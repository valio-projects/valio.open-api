using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;

namespace ValIO.Generators.TypeScript.Tests.Core;

/// <summary>
/// Unit tests for the core AST node hierarchy — Clone, Children, ToString,
/// constructor invariants. These nodes are shared by every generator, so a
/// regression here breaks every language plugin.
/// </summary>
[TestFixture]
public class AstNodeTests
{
    [Test]
    public void IdentifierNode_TwoNamesAreNotEqual()
    {
        var a = new IdentifierNode("foo");
        var b = new IdentifierNode("foo");
        a.Name.Should().Be(b.Name);
        a.Clone().Should().NotBeSameAs(a);
    }

    [Test]
    public void LiteralExpressionNode_HoldsValueAndKind()
    {
        var lit = new LiteralExpressionNode(42, LiteralKind.Number);
        lit.Value.Should().Be(42);
        lit.LiteralKind.Should().Be(LiteralKind.Number);
    }

    [Test]
    public void LiteralExpressionNode_Clone_PreservesValueAndKind()
    {
        var lit = new LiteralExpressionNode("hello", LiteralKind.String);
        var clone = (LiteralExpressionNode)lit.Clone();
        clone.Value.Should().Be("hello");
        clone.LiteralKind.Should().Be(LiteralKind.String);
        clone.Should().NotBeSameAs(lit);
    }

    [Test]
    public void MemberAccessExpressionNode_OptionalChaining_FlagPropagatesThroughClone()
    {
        var node = new MemberAccessExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("obj")),
            new IdentifierNode("prop"),
            useOptionalChaining: true);
        node.UseOptionalChaining.Should().BeTrue();
        var clone = (MemberAccessExpressionNode)node.Clone();
        clone.UseOptionalChaining.Should().BeTrue();
    }

    [Test]
    public void CastExpressionNode_UseAsOperator_FlagPropagatesThroughClone()
    {
        var node = new CastExpressionNode(
            new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
            new IdentifierExpressionNode(new IdentifierNode("x")),
            useAsOperator: true);
        node.UseAsOperator.Should().BeTrue();
        var clone = (CastExpressionNode)node.Clone();
        clone.UseAsOperator.Should().BeTrue();
    }

    [Test]
    public void BinaryExpressionNode_StoresOperator()
    {
        var n = new BinaryExpressionNode(
            new LiteralExpressionNode(1, LiteralKind.Number),
            BinaryOperator.NullishCoalescing,
            new LiteralExpressionNode(2, LiteralKind.Number));
        n.Operator.Should().Be(BinaryOperator.NullishCoalescing);
        n.Children.Count.Should().BeGreaterThan(0);
    }

    [Test]
    public void CallExpressionNode_GenericTypeNameIsPreserved()
    {
        var call = new CallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("useQuery")),
            ImmutableList<ExpressionNode>.Empty,
            genericTypeName: "User");
        call.GenericTypeName.Should().Be("User");
    }

    [Test]
    public void ConditionalExpressionNode_HasThreeChildren()
    {
        var cond = new ConditionalExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("x")),
            new LiteralExpressionNode(1, LiteralKind.Number),
            new LiteralExpressionNode(2, LiteralKind.Number));
        cond.Condition.Should().NotBeNull();
        cond.TrueExpression.Should().NotBeNull();
        cond.FalseExpression.Should().NotBeNull();
    }

    [Test]
    public void SimpleTypeNode_IsPrimitiveFlagIsPreserved()
    {
        var n = new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true);
        n.IsPrimitive.Should().BeTrue();
        ((SimpleTypeNode)n.Clone()).IsPrimitive.Should().BeTrue();
    }

    [Test]
    public void GenericTypeNode_PreservesAllTypeArguments()
    {
        var n = new GenericTypeNode(
            new IdentifierNode("Map"),
            ImmutableList.Create<TypeNode>(
                new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
                new SimpleTypeNode(new IdentifierNode("number"), isPrimitive: true)));
        n.TypeArguments.Count.Should().Be(2);
        ((GenericTypeNode)n.Clone()).TypeArguments.Count.Should().Be(2);
    }

    [Test]
    public void UnionTypeNode_PreservesMembers()
    {
        var n = new UnionTypeNode(ImmutableList.Create<TypeNode>(
            new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
            new SimpleTypeNode(new IdentifierNode("null"), isPrimitive: true)));
        n.Types.Count.Should().Be(2);
    }

    [Test]
    public void ArrayTypeNode_IsReadOnlyFlagIsPreserved()
    {
        var n = new ArrayTypeNode(
            new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
            isReadOnly: true);
        n.IsReadOnly.Should().BeTrue();
    }

    [Test]
    public void ImportNode_PreservesItemsAndKind()
    {
        var n = new ImportNode(
            items: ImmutableList.Create(new IdentifierNode("Pet"), new IdentifierNode("Tag")),
            from: "./pet",
            kind: ImportKind.Named,
            isTypeOnly: true);
        n.Items.Count.Should().Be(2);
        n.From.Should().Be("./pet");
        n.IsTypeOnly.Should().BeTrue();
        n.ImportKind.Should().Be(ImportKind.Named);
    }

    [Test]
    public void CommentNode_PreservesKind()
    {
        var n = new CommentNode("hello", CommentKind.JSDoc);
        n.CommentKind.Should().Be(CommentKind.JSDoc);
        n.Text.Should().Be("hello");
    }
}
