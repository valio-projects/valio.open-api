using ValIO.Core.Ast.Nodes;

namespace ValIO.Core.Ast.Utilities;

/// <summary>
/// Утилита для клонирования AST узлов (Prototype Pattern helper)
/// </summary>
public static class AstNodeCloner
{
    /// <summary>
    /// Клонирует узел
    /// </summary>
    public static T CloneNode<T>(T node) where T : AstNode
    {
        return (T)node.Clone();
    }
    
    /// <summary>
    /// Клонирует список узлов
    /// </summary>
    public static IReadOnlyList<T> CloneNodes<T>(IReadOnlyList<T> nodes) where T : AstNode
    {
        return nodes.Select(n => (T)n.Clone()).ToList();
    }
}
