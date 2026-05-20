using ValIO.Generators.Rust.Ast.Nodes;

namespace ValIO.Generators.Rust.Ast.Renderers;

/// <summary>
/// Contract for the Rust AST → source-text renderer.
/// </summary>
public interface IRustAstRenderer
{
    /// <summary>Renders a single <c>.rs</c> file.</summary>
    string Render(RustFileNode file);
}
