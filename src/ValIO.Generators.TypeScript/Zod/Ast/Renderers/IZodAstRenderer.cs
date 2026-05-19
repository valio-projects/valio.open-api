using ValIO.Generators.TypeScript.Zod.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Zod.Ast.Renderers;

/// <summary>
/// Интерфейс рендерера AST для Zod
/// </summary>
public interface IZodAstRenderer
{
    /// <summary>
    /// Рендерит ZodObjectNode в TypeScript код
    /// </summary>
    string Render(ZodObjectNode node);

    /// <summary>
    /// Рендерит ZodSchemaNode (object, union, intersection) в TypeScript код
    /// </summary>
    string RenderSchema(ZodSchemaNode node);
}
