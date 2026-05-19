using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.TypeBox.Ast.Renderers;

/// <summary>
/// Звено цепочки ответственности для TypeBox-узлов.
/// </summary>
/// <remarks>
/// На текущий момент TypeBox полностью использует общий TypeScript AST
/// (без специфичных Tb*Node-узлов), поэтому это no-op placeholder: всегда
/// возвращает <c>false</c> из <see cref="CanRender"/>. Сохраняем класс для
/// симметрии с <see cref="ZodNodeRenderer"/> и регистрации в DI — когда
/// появятся TypeBox-специфичные узлы, обновляем CanRender/Render без правок
/// в потребителях.
/// </remarks>
public sealed class TypeBoxNodeRenderer : ITypeScriptNodeRenderer
{
    /// <inheritdoc/>
    public int Priority => 100;

    /// <inheritdoc/>
    public bool CanRender(AstNode node) => false;

    /// <inheritdoc/>
    public string Render(AstNode node, ITypeScriptAstRenderer host)
        => throw new NotSupportedException(
            "TypeBoxNodeRenderer is a placeholder; CanRender returned false for "
            + node.GetType().Name);
}
