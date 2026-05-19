using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Узел файла реализации (.cpp) C++.
/// Содержит #include директивы и секции (с заголовками и разделителями).
/// Структура полностью задаётся builder'ом, рендерер только выводит.
/// </summary>
public sealed class CppSourceFileNode : AstNode
{
    /// <summary>
    /// Имя файла (для отладки)
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Блок комментария в начале файла (copyright, generated notice)
    /// </summary>
    public CommentNode? HeaderComment { get; }

    /// <summary>
    /// Директивы #include
    /// </summary>
    public IReadOnlyList<CppIncludeDirectiveNode> Includes { get; }

    /// <summary>
    /// Секции файла (заголовок, обёртка namespace, члены). Builder формирует структуру.
    /// </summary>
    public IReadOnlyList<CppSourceSection> Sections { get; }


    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (HeaderComment != null) children.Add(HeaderComment);
            children.AddRange(Includes);
            foreach (var section in Sections)
                children.AddRange(section.Members);
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public CppSourceFileNode(
        string fileName,
        CommentNode? headerComment = null,
        IReadOnlyList<CppIncludeDirectiveNode>? includes = null,
        IReadOnlyList<CppSourceSection>? sections = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        HeaderComment = headerComment;
        Includes = includes?.ToImmutableList() ?? ImmutableList<CppIncludeDirectiveNode>.Empty;
        Sections = sections?.ToImmutableList() ?? ImmutableList<CppSourceSection>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppSourceFile(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit CppSourceFileNode");
    }

    public override AstNode Clone()
    {
        var clonedSections = Sections.Select(s => new CppSourceSection(
            s.Title,
            s.WrapInAnonymousNamespace,
            s.Members.Select(m => m.Clone()).ToImmutableList())).ToImmutableList();
        return new CppSourceFileNode(
            FileName,
            HeaderComment != null ? (CommentNode)HeaderComment.Clone() : null,
            Includes.Select(i => (CppIncludeDirectiveNode)i.Clone()).ToImmutableList(),
            clonedSections,
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        var total = Sections.Sum(s => s.Members.Count);
        return $"CppSourceFile({FileName}, {Sections.Count} sections, {total} members)";
    }
}
