using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Zod.Ast.Renderers;

/// <summary>
/// Chain-of-Responsibility link that lets <see cref="TypeScriptAstRenderer"/> emit Zod
/// schema nodes (<see cref="ZodSchemaNode"/>, <see cref="ZodPropertyNode"/>, etc.) when
/// it encounters them via <c>VisitDefault</c> (e.g. embedded in a generic source file).
///
/// Delegates the actual emission to a fresh <see cref="ZodAstRenderer"/>, which is a
/// self-contained Zod-aware visitor.
/// </summary>
public sealed class ZodNodeRenderer : ITypeScriptNodeRenderer
{
    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public bool CanRender(AstNode node) =>
        node is ZodSchemaNode or ZodPropertyNode or ZodEnumValueNode;

    /// <inheritdoc />
    public string Render(AstNode node, ITypeScriptAstRenderer host)
    {
        var renderer = new ZodAstRenderer();
        return node switch
        {
            ZodSchemaNode schema => renderer.RenderSchema(schema),
            ZodPropertyNode property => renderer.VisitZodProperty(property),
            ZodEnumValueNode enumValue => renderer.VisitZodEnumValue(enumValue),
            _ => throw new NotSupportedException(
                $"{nameof(ZodNodeRenderer)} cannot render {node.GetType().Name}"),
        };
    }
}
