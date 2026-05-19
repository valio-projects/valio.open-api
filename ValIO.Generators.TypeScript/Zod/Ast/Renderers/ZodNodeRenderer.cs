using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Zod.Ast.Renderers;

/// <summary>
/// Звено цепочки ответственности для Zod-узлов (Z*Node иерархия).
/// Делегирует рендеринг в <see cref="ZodAstRenderer"/>.
/// </summary>
/// <remarks>
/// Приоритет 100 — выше Axios/TanStack звеньев, потому что Zod-узлы являются
/// валидно типизированными выражениями и не должны быть перехвачены звеньями
/// клиентских библиотек.
/// </remarks>
public sealed class ZodNodeRenderer : ITypeScriptNodeRenderer
{
    private readonly ZodAstRenderer _zodRenderer;

    /// <summary>
    /// Используется DI-контейнером для создания экземпляра.
    /// </summary>
    public ZodNodeRenderer()
        : this(new ZodAstRenderer())
    {
    }

    /// <summary>
    /// Конструктор с внешним <see cref="ZodAstRenderer"/> (полезно для тестов и
    /// сценариев, где состояние renderer нужно переиспользовать).
    /// </summary>
    public ZodNodeRenderer(ZodAstRenderer zodRenderer)
    {
        _zodRenderer = zodRenderer ?? throw new ArgumentNullException(nameof(zodRenderer));
    }

    /// <inheritdoc/>
    public int Priority => 100;

    /// <inheritdoc/>
    public bool CanRender(AstNode node) => node is ZodSchemaNode or ZodPropertyNode or ZodEnumValueNode;

    /// <inheritdoc/>
    public string Render(AstNode node, ITypeScriptAstRenderer host)
    {
        return node switch
        {
            ZodSchemaNode schema => _zodRenderer.RenderSchema(schema),
            ZodPropertyNode property => _zodRenderer.VisitZodProperty(property),
            ZodEnumValueNode enumValue => _zodRenderer.VisitZodEnumValue(enumValue),
            _ => throw new NotSupportedException($"ZodNodeRenderer can't render {node.GetType().Name}")
        };
    }
}
