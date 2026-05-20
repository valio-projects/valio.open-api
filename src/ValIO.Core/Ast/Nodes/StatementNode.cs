using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Базовый класс для всех statements
/// </summary>
public abstract class StatementNode : AstNode
{
    /// <summary>
    /// Документация (комментарий)
    /// </summary>
    public CommentNode? Documentation { get; }
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            if (Documentation != null)
                return new[] { Documentation };
            return Array.Empty<AstNode>();
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    protected StatementNode(CommentNode? documentation = null, IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Documentation = documentation;
    }
    
}
