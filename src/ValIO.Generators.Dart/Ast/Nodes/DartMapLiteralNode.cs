using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>Represents a typed Dart map literal.</summary>
/// <param name="keyType">The key type.</param>
/// <param name="valueType">The value type.</param>
/// <param name="entries">The entries in source order.</param>
public sealed class DartMapLiteralNode(TypeNode keyType, TypeNode valueType, IReadOnlyList<(ExpressionNode Key, ExpressionNode Value)> entries) : ExpressionNode
{
    /// <summary>Gets the key type.</summary>
    /// <value>The key type.</value>
    public TypeNode KeyType { get; } = keyType;

    /// <summary>Gets the value type.</summary>
    /// <value>The value type.</value>
    public TypeNode ValueType { get; } = valueType;

    /// <summary>Gets the entries in source order.</summary>
    /// <value>The entries in source order.</value>
    public IReadOnlyList<(ExpressionNode Key, ExpressionNode Value)> Entries { get; } = entries.ToArray();

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [KeyType, ValueType, .. Entries.SelectMany(entry => new AstNode[] { entry.Key, entry.Value })];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is IDartAstVisitor<TResult> dart
            ? dart.VisitDartMapLiteral(this)
            : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new DartMapLiteralNode((TypeNode)KeyType.Clone(), (TypeNode)ValueType.Clone(),
        Entries.Select(entry => ((ExpressionNode)entry.Key.Clone(), (ExpressionNode)entry.Value.Clone())).ToArray());
}
