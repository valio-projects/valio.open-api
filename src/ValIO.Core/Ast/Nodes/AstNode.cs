using System.Collections.Immutable;
using System.Diagnostics;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Базовый класс для всех AST узлов (immutable)
/// </summary>
public abstract class AstNode
{
    /// <summary>
    /// Дочерние узлы
    /// </summary>
    public abstract IReadOnlyList<AstNode> Children { get; }
    
    /// <summary>
    /// Метаданные узла (для дополнительной информации)
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    protected AstNode(IReadOnlyDictionary<string, object>? metadata = null)
    {
        Metadata = metadata ?? ImmutableDictionary<string, object>.Empty;
    }
    
    /// <summary>
    /// Принимает посетителя (Visitor Pattern)
    /// </summary>
    public abstract TResult Accept<TResult>(IAstVisitor<TResult> visitor);
    
    /// <summary>
    /// Клонирует узел (Prototype Pattern)
    /// </summary>
    public abstract AstNode Clone();
    
    /// <summary>
    /// Возвращает строковое представление узла для отладки
    /// </summary>
    public override string ToString() => GetType().Name;
}
