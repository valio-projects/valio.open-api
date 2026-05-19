using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.TypeBox.Ast.Renderers;

/// <summary>
/// Placeholder chain-of-responsibility link for TypeBox. TypeBox schemas are emitted
/// directly through the generic TypeScript AST (see
/// <see cref="TypeBox.Ast.Builders.TypeBoxSchemaSourceFileBuilder"/>), so there are no
/// dedicated TypeBox AST node types this renderer needs to handle today.
///
/// Registered uniformly with the rest of the chain so that future TypeBox-specific
/// nodes can be added without changing DI wiring.
/// </summary>
public sealed class TypeBoxNodeRenderer : ITypeScriptNodeRenderer
{
    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public bool CanRender(AstNode node) => false;

    /// <inheritdoc />
    public string Render(AstNode node, ITypeScriptAstRenderer host) =>
        throw new NotSupportedException(
            $"{nameof(TypeBoxNodeRenderer)} is a placeholder — {nameof(CanRender)} always returns false.");
}
