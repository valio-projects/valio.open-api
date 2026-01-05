using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Базовый класс для всех типов
/// </summary>
public abstract class TypeNode : AstNode
{
    /// <summary>
    /// Является ли тип nullable
    /// </summary>
    public bool IsNullable { get; }
    
    /// <summary>
    /// Является ли тип optional (для TypeScript)
    /// </summary>
    public bool IsOptional { get; }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    protected TypeNode(bool isNullable = false, bool isOptional = false, IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        IsNullable = isNullable;
        IsOptional = isOptional;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitType(this);
    }
}
