using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

/// <summary>
/// Узел литерального массива (TypeScript)
/// Например: [1, 2, 3] или ["a", "b", "c"]
/// </summary>
public sealed class ArrayLiteralExpressionNode : ExpressionNode
{
    /// <summary>
    /// Элементы массива
    /// </summary>
    public IReadOnlyList<ExpressionNode> Elements { get; }

    /// <summary>
    /// Рендерить массив с переносами строк для каждого элемента (для query keys и т.д.)
    /// </summary>
    public bool PreferMultilineFormat { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ObjectLiteralExpression; // Временно используем ObjectLiteralExpression, можно добавить ArrayLiteralExpression позже
    
    public override IReadOnlyList<AstNode> Children => Elements.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ArrayLiteralExpressionNode(
        IReadOnlyList<ExpressionNode> elements,
        TypeNode? type = null,
        bool preferMultilineFormat = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        Elements = elements?.ToImmutableList() ?? ImmutableList<ExpressionNode>.Empty;
        PreferMultilineFormat = preferMultilineFormat;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitArrayLiteralExpression(this);
        }
        // Если visitor не поддерживает TypeScript, делегируем в VisitExpression
        return visitor.VisitExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new ArrayLiteralExpressionNode(
            Elements.Select(e => (ExpressionNode)e.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            PreferMultilineFormat,
            Metadata.ToImmutableDictionary());
    }
}
