using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Expressions;

/// <summary>
/// Узел выражения вызова функции/метода
/// </summary>
public sealed class CallExpressionNode : ExpressionNode
{
    /// <summary>
    /// Вызываемый объект/функция
    /// </summary>
    public ExpressionNode Target { get; }
    
    /// <summary>
    /// Аргументы вызова
    /// </summary>
    public IReadOnlyList<ExpressionNode> Arguments { get; }
    
    public override AstNodeKind Kind => AstNodeKind.CallExpression;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Target };
            children.AddRange(Arguments);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public CallExpressionNode(
        ExpressionNode target,
        IReadOnlyList<ExpressionNode>? arguments = null,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Arguments = arguments?.ToImmutableList() ?? ImmutableList<ExpressionNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitCallExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new CallExpressionNode(
            (ExpressionNode)Target.Clone(),
            Arguments.Select(a => (ExpressionNode)a.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
