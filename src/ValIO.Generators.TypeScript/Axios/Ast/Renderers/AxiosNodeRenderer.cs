using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Axios.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Axios.Ast.Renderers;

/// <summary>
/// Chain-of-Responsibility link that lets <see cref="TypeScriptAstRenderer"/> emit Axios
/// AST nodes (<see cref="AxiosCallExpressionNode"/>, <see cref="AxiosConfigNode"/>) when
/// they appear in a generic source file.
///
/// In practice, <c>AxiosCallExpressionNode.Accept</c> on a generic TypeScript visitor
/// also has a graceful fallback (it constructs an equivalent <c>CallExpressionNode</c>),
/// but routing through this renderer preserves the bespoke multi-line formatting
/// produced by <see cref="AxiosAstRenderer"/>.
/// </summary>
public sealed class AxiosNodeRenderer : ITypeScriptNodeRenderer
{
    /// <inheritdoc />
    public int Priority => 90;

    /// <inheritdoc />
    public bool CanRender(AstNode node) =>
        node is AxiosCallExpressionNode or AxiosConfigNode;

    /// <inheritdoc />
    public string Render(AstNode node, ITypeScriptAstRenderer host)
    {
        var renderer = new AxiosAstRenderer();
        return node switch
        {
            AxiosCallExpressionNode call => renderer.VisitAxiosCall(call),
            AxiosConfigNode config => renderer.VisitAxiosConfig(config),
            _ => throw new NotSupportedException(
                $"{nameof(AxiosNodeRenderer)} cannot render {node.GetType().Name}"),
        };
    }
}
