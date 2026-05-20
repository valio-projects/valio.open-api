using ValIO.Generators.Dart.Ast.Nodes;

namespace ValIO.Generators.Dart.Ast.Renderers;

/// <summary>
/// Contract for the Dart AST → source text renderer.
/// </summary>
public interface IDartAstRenderer
{
    /// <summary>Render a complete .dart file.</summary>
    string Render(DartFileNode file);
}
