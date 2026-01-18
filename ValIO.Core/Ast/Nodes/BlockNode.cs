using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел блока statements
/// </summary>
public sealed class BlockNode : AstNode
{
    /// <summary>
    /// Statements в блоке
    /// </summary>
    public IReadOnlyList<StatementNode> Statements { get; }
    
    public override AstNodeKind Kind => AstNodeKind.Block;
    
    public override IReadOnlyList<AstNode> Children => Statements.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public BlockNode(IReadOnlyList<StatementNode>? statements = null, IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Statements = statements?.ToImmutableList() ?? ImmutableList<StatementNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitBlock(this);
    }
    
    public override AstNode Clone()
    {
        return new BlockNode(
            Statements.Select(s => (StatementNode)s.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
    
    public override string ToString()
    {
        return $"Block({Statements.Count} statements)";
    }
}
