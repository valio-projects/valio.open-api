using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Valibot.Ast.Renderers;

/// <summary>
/// Звено цепочки ответственности для Valibot-узлов.
/// </summary>
/// <remarks>
/// На текущий момент Valibot полностью использует общий TypeScript AST
/// (без специфичных V*Node-узлов), поэтому это no-op placeholder: всегда
/// возвращает <c>false</c> из <see cref="CanRender"/>. Сохраняем класс для
/// симметрии с <see cref="ZodNodeRenderer"/> и регистрации в DI — когда
/// появятся Valibot-специфичные узлы, обновляем CanRender/Render без правок
/// в потребителях.
/// </remarks>
public sealed class ValibotNodeRenderer : ITypeScriptNodeRenderer
{
    /// <inheritdoc/>
    public int Priority => 100;

    /// <inheritdoc/>
    public bool CanRender(AstNode node) => false;

    /// <inheritdoc/>
    public string Render(AstNode node, ITypeScriptAstRenderer host)
        => throw new NotSupportedException(
            "ValibotNodeRenderer is a placeholder; CanRender returned false for "
            + node.GetType().Name);
}
