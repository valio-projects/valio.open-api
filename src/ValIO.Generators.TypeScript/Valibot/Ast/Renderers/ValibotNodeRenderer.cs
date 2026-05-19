using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Valibot.Ast.Renderers;

/// <summary>
/// Placeholder chain-of-responsibility link for Valibot. Valibot schemas are emitted
/// directly through the generic TypeScript AST (see
/// <see cref="Valibot.Ast.Builders.ValibotSchemaSourceFileBuilder"/>), so there are no
/// dedicated Valibot AST node types this renderer needs to handle today.
///
/// Registered uniformly with the rest of the chain so that future Valibot-specific
/// nodes can be added without changing DI wiring.
/// </summary>
public sealed class ValibotNodeRenderer : ITypeScriptNodeRenderer
{
    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public bool CanRender(AstNode node) => false;

    /// <inheritdoc />
    public string Render(AstNode node, ITypeScriptAstRenderer host) =>
        throw new NotSupportedException(
            $"{nameof(ValibotNodeRenderer)} is a placeholder — {nameof(CanRender)} always returns false.");
}
