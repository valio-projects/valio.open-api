using ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Renderers;

/// <summary>
/// Интерфейс рендерера AST для TanStack Query
/// </summary>
public interface ITanStackQueryAstRenderer
{
    /// <summary>
    /// Рендерит UseQueryNode в TypeScript код
    /// </summary>
    string Render(UseQueryNode node);
    
    /// <summary>
    /// Рендерит UseMutationNode в TypeScript код
    /// </summary>
    string Render(UseMutationNode node);
}
