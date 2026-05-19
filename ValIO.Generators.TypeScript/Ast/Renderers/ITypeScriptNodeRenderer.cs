using ValIO.Core.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Ast.Renderers;

/// <summary>
/// Chain-of-Responsibility renderer for AST nodes that <see cref="TypeScriptAstRenderer"/>
/// itself cannot dispatch (e.g. plugin nodes from Zod / Valibot / TypeBox / Axios /
/// TanStack Query extensions). Registered as singletons via DI in
/// <see cref="TypeScriptGeneratorPlugin.ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>.
///
/// The chain is consulted in <see cref="TypeScriptAstRenderer"/>'s <c>VisitDefault</c>
/// path. Higher <see cref="Priority"/> wins. If no renderer matches, the host falls back
/// to the existing <see cref="NotSupportedException"/>.
/// </summary>
public interface ITypeScriptNodeRenderer
{
    /// <summary>
    /// Higher value wins. Used for ordering when multiple renderers <see cref="CanRender"/>.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Returns <c>true</c> iff this renderer claims responsibility for <paramref name="node"/>.
    /// </summary>
    bool CanRender(AstNode node);

    /// <summary>
    /// Renders <paramref name="node"/> to TypeScript source.
    /// </summary>
    /// <param name="node">Node to render. Implementations may safely downcast.</param>
    /// <param name="host">The owning <see cref="TypeScriptAstRenderer"/>, exposed via
    /// <see cref="ITypeScriptAstRenderer"/> so the implementation can recursively
    /// <see cref="Core.Ast.Visitors.IAstVisitor{TResult}.Visit"/> children through the
    /// host's full visitor.</param>
    string Render(AstNode node, ITypeScriptAstRenderer host);
}
