using ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для TanStack Query специфичных AST узлов
/// </summary>
public interface ITanStackQueryAstVisitor<TResult>
{
    /// <summary>
    /// Посещает query key
    /// </summary>
    TResult VisitQueryKey(QueryKeyNode node);
    
    /// <summary>
    /// Посещает useQuery хук
    /// </summary>
    TResult VisitUseQuery(UseQueryNode node);
    
    /// <summary>
    /// Посещает useMutation хук
    /// </summary>
    TResult VisitUseMutation(UseMutationNode node);
    
    /// <summary>
    /// Посещает query options
    /// </summary>
    TResult VisitQueryOptions(QueryOptionsNode node);
    
    /// <summary>
    /// Посещает mutation options
    /// </summary>
    TResult VisitMutationOptions(MutationOptionsNode node);
}
