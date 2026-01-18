using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел идентификатора
/// </summary>
public sealed class IdentifierNode : AstNode
{
    /// <summary>
    /// Имя идентификатора
    /// </summary>
    public string Name { get; }
    
    public override AstNodeKind Kind => AstNodeKind.Identifier;
    
    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public IdentifierNode(string name, IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Identifier name cannot be null or whitespace", nameof(name));
        
        Name = name;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitIdentifier(this);
    }
    
    public override AstNode Clone()
    {
        return new IdentifierNode(Name, Metadata.ToImmutableDictionary());
    }
    
    public override string ToString()
    {
        return $"Identifier({Name})";
    }
}
