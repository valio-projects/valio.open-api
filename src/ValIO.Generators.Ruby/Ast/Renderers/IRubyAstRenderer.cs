using ValIO.Generators.Ruby.Ast.Nodes;

namespace ValIO.Generators.Ruby.Ast.Renderers;

/// <summary>
/// Contract for the Ruby AST → source-text renderer.
/// </summary>
public interface IRubyAstRenderer
{
    /// <summary>Renders a single <c>.rb</c> file.</summary>
    string Render(RubyFileNode file);
}
