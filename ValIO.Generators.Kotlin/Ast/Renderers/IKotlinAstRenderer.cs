using ValIO.Generators.Kotlin.Ast.Nodes;

namespace ValIO.Generators.Kotlin.Ast.Renderers;

/// <summary>
/// Contract for the Kotlin AST → source-text renderer.
/// </summary>
public interface IKotlinAstRenderer
{
    /// <summary>Renders a single <c>.kt</c> file.</summary>
    string Render(KotlinFileNode file);
}
