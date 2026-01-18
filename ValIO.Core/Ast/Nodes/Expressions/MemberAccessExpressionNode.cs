using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Expressions;

/// <summary>
/// Узел выражения доступа к члену (object.member)
/// </summary>
public sealed class MemberAccessExpressionNode : ExpressionNode
{
    /// <summary>
    /// Объект
    /// </summary>
    public ExpressionNode Object { get; }
    
    /// <summary>
    /// Член
    /// </summary>
    public IdentifierNode Member { get; }
    
    /// <summary>
    /// Использовать как оператор (например, "as" в C# для приведения типов)
    /// </summary>
    public bool UseAsOperator { get; }
    
    public override AstNodeKind Kind => AstNodeKind.MemberAccessExpression;
    
    public override IReadOnlyList<AstNode> Children => new AstNode[] { Object, Member };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public MemberAccessExpressionNode(
        ExpressionNode @object,
        IdentifierNode member,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        bool useAsOperator = false)
        : base(type, metadata)
    {
        Object = @object ?? throw new ArgumentNullException(nameof(@object));
        Member = member ?? throw new ArgumentNullException(nameof(member));
        UseAsOperator = useAsOperator;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitMemberAccessExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new MemberAccessExpressionNode(
            (ExpressionNode)Object.Clone(),
            (IdentifierNode)Member.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary(),
            UseAsOperator);
    }
}
