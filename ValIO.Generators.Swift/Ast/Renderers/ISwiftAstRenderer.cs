using ValIO.Generators.Swift.Ast.Nodes;

namespace ValIO.Generators.Swift.Ast.Renderers;

/// <summary>
/// Contract for the Swift AST → source-text renderer.
/// </summary>
public interface ISwiftAstRenderer
{
    /// <summary>Renders a single <c>.swift</c> file.</summary>
    string Render(SwiftFileNode file);
}
