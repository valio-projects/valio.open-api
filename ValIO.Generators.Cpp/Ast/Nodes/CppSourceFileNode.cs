using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Узел файла реализации (.cpp) C++.
/// Содержит #include директивы, анонимный namespace с parse-функциями, реализации методов.
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
    /// Содержимое анонимного namespace (parse-функции, внутренние хелперы)
    /// </summary>
    public IReadOnlyList<AstNode> AnonymousNamespaceMembers { get; }

    /// <summary>
    /// Реализации функций и методов (свободные функции, ClassName::Method)
    /// </summary>
    public IReadOnlyList<AstNode> Implementations { get; }

    public override AstNodeKind Kind => AstNodeKind.CppSourceFile;

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (HeaderComment != null) children.Add(HeaderComment);
            children.AddRange(Includes);
            children.AddRange(AnonymousNamespaceMembers);
            children.AddRange(Implementations);
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
        IReadOnlyList<AstNode>? anonymousNamespaceMembers = null,
        IReadOnlyList<AstNode>? implementations = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        HeaderComment = headerComment;
        Includes = includes?.ToImmutableList() ?? ImmutableList<CppIncludeDirectiveNode>.Empty;
        AnonymousNamespaceMembers = anonymousNamespaceMembers?.ToImmutableList() ?? ImmutableList<AstNode>.Empty;
        Implementations = implementations?.ToImmutableList() ?? ImmutableList<AstNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppSourceFile(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit CppSourceFileNode");
    }

    public override AstNode Clone()
    {
        return new CppSourceFileNode(
            FileName,
            HeaderComment != null ? (CommentNode)HeaderComment.Clone() : null,
            Includes.Select(i => (CppIncludeDirectiveNode)i.Clone()).ToImmutableList(),
            AnonymousNamespaceMembers.Select(m => m.Clone()).ToImmutableList(),
            Implementations.Select(i => i.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        return $"CppSourceFile({FileName}, {Implementations.Count} implementations)";
    }
}
