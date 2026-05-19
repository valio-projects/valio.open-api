using System.Collections.Immutable;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел атрибута/декоратора
/// </summary>
public sealed class AttributeNode : AstNode
{
    /// <summary>
    /// Имя атрибута
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Аргументы атрибута
    /// </summary>
    public IReadOnlyList<ExpressionNode> Arguments { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            children.AddRange(Arguments);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public AttributeNode(IdentifierNode name, IReadOnlyList<ExpressionNode>? arguments = null, IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Arguments = arguments?.ToImmutableList() ?? ImmutableList<ExpressionNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitAttribute(this);
    }
    
    public override AstNode Clone()
    {
        return new AttributeNode(
            (IdentifierNode)Name.Clone(),
            Arguments.Select(a => (ExpressionNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
