using ValIO.Generators.FSharp.Ast.Nodes;

namespace ValIO.Generators.FSharp.Ast.Renderers;

/// <summary>
/// Contract for the F# AST → source-text renderer.
/// </summary>
public interface IFSharpAstRenderer
{
    /// <summary>Renders a single <c>.fs</c> file.</summary>
    string Render(FSharpFileNode file);
}
