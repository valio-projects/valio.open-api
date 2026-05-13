using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Базовый класс для всех выражений
/// </summary>
public abstract class ExpressionNode : AstNode
{
    /// <summary>
    /// Тип выражения (опционально, для type inference)
    /// </summary>
    public TypeNode? Type { get; }
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            if (Type != null)
                return new[] { Type };
            return Array.Empty<AstNode>();
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    protected ExpressionNode(TypeNode? type = null, IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Type = type;
    }
    
}
