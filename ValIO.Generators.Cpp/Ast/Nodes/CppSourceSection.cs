using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Секция исходного файла .cpp.
/// Содержит опциональный заголовок (RenderSeparator), флаг обёртки в namespace и список узлов.
/// Структура задаётся AST builder'ом, рендерер только выводит без логики.
/// </summary>
public sealed class CppSourceSection
{
    /// <summary>
    /// Заголовок секции для комментария-разделителя (// ---- Title ----). null = без разделителя.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Обернуть содержимое в <c>namespace { ... }</c>.
    /// </summary>
    public bool WrapInAnonymousNamespace { get; }

    /// <summary>
    /// Узлы секции.
    /// </summary>
    public IReadOnlyList<AstNode> Members { get; }

    public CppSourceSection(
        string? title = null,
        bool wrapInAnonymousNamespace = false,
        IReadOnlyList<AstNode>? members = null)
    {
        Title = title;
        WrapInAnonymousNamespace = wrapInAnonymousNamespace;
        Members = members?.ToImmutableList() ?? ImmutableList<AstNode>.Empty;
    }
}
