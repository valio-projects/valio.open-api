using ValIO.Core.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Ast.Renderers;

/// <summary>
/// Звено цепочки ответственности (Chain of Responsibility) для рендеринга
/// TypeScript-узлов. Используется монолитным <see cref="TypeScriptAstRenderer"/>
/// для делегирования рендеринга стек-специфичных узлов (Zod, Valibot, TypeBox,
/// Axios, TanStack Query) без необходимости добавлять методы в базовый visitor.
///
/// Контракт:
/// - <see cref="Priority"/>: выше — раньше попадает в опрос; первая «can-render»
///   запись побеждает.
/// - <see cref="CanRender(AstNode)"/>: предикат, проверяющий, обрабатывает ли это
///   звено указанный узел (как правило по типу/AstNodeKind).
/// - <see cref="Render(AstNode, ITypeScriptAstRenderer)"/>: рендерит узел; для
///   рекурсивных обходов получает «хост» — основной TypeScript-рендерер.
/// </summary>
public interface ITypeScriptNodeRenderer
{
    /// <summary>
    /// Приоритет звена. Чем выше число, тем раньше звено опрашивается.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Возвращает <c>true</c>, если это звено умеет рендерить данный узел.
    /// </summary>
    bool CanRender(AstNode node);

    /// <summary>
    /// Рендерит узел, используя <paramref name="host"/> для рекурсивных вызовов.
    /// </summary>
    /// <param name="node">Узел AST для рендеринга.</param>
    /// <param name="host">Основной TypeScript-рендерер; используйте его методы
    /// <see cref="ITypeScriptAstRenderer.Render(SourceFileNode)"/> и подобные,
    /// либо привязку как <see cref="ValIO.Core.Ast.Visitors.IAstVisitor{TResult}"/>,
    /// чтобы продолжить обход.</param>
    string Render(AstNode node, ITypeScriptAstRenderer host);
}
