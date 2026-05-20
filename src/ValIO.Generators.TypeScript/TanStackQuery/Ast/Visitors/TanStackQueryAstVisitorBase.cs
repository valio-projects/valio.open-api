using ValIO.Generators.TypeScript.Ast.Visitors;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Nodes;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя TanStack Query AST узлов
/// </summary>
public abstract class TanStackQueryAstVisitorBase<TResult> : TypeScriptAstVisitorBase<TResult>, ITanStackQueryAstVisitor<TResult>
{
    // Абстрактные методы только для TanStack Query узлов
    public abstract TResult VisitQueryKey(QueryKeyNode node);
    public abstract TResult VisitUseQuery(UseQueryNode node);
    public abstract TResult VisitUseMutation(UseMutationNode node);
    public abstract TResult VisitQueryOptions(QueryOptionsNode node);
    public abstract TResult VisitMutationOptions(MutationOptionsNode node);
}
