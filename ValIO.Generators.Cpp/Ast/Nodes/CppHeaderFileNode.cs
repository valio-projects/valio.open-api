using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Узел заголовочного (.h) файла C++.
/// Содержит #pragma once, #include директивы, forward declarations и объявления типов.
/// </summary>
public sealed class CppHeaderFileNode : AstNode
{
    /// <summary>
    /// Имя файла (для отладки и генерации .generated.h)
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Блок комментария в начале файла (copyright, generated notice)
    /// </summary>
    public CommentNode? HeaderComment { get; }

    /// <summary>
    /// Включать ли #pragma once
    /// </summary>
    public bool PragmaOnce { get; }

    /// <summary>
    /// Директивы #include
    /// </summary>
    public IReadOnlyList<CppIncludeDirectiveNode> Includes { get; }

    /// <summary>
    /// Forward declarations (class X; struct Y;)
    /// </summary>
    public IReadOnlyList<CppForwardDeclarationNode> ForwardDeclarations { get; }

    /// <summary>
    /// Using-алиасы (using X = Y;)
    /// </summary>
    public IReadOnlyList<CppUsingAliasNode> UsingAliases { get; }

    /// <summary>
    /// Основные объявления (structs, classes, functions, delegates и т.д.)
    /// </summary>
    public IReadOnlyList<AstNode> Declarations { get; }


    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (HeaderComment != null) children.Add(HeaderComment);
            children.AddRange(Includes);
            children.AddRange(ForwardDeclarations);
            children.AddRange(UsingAliases);
            children.AddRange(Declarations);
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public CppHeaderFileNode(
        string fileName,
        CommentNode? headerComment = null,
        bool pragmaOnce = true,
        IReadOnlyList<CppIncludeDirectiveNode>? includes = null,
        IReadOnlyList<CppForwardDeclarationNode>? forwardDeclarations = null,
        IReadOnlyList<CppUsingAliasNode>? usingAliases = null,
        IReadOnlyList<AstNode>? declarations = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        HeaderComment = headerComment;
        PragmaOnce = pragmaOnce;
        Includes = includes?.ToImmutableList() ?? ImmutableList<CppIncludeDirectiveNode>.Empty;
        ForwardDeclarations = forwardDeclarations?.ToImmutableList() ?? ImmutableList<CppForwardDeclarationNode>.Empty;
        UsingAliases = usingAliases?.ToImmutableList() ?? ImmutableList<CppUsingAliasNode>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<AstNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppHeaderFile(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit CppHeaderFileNode");
    }

    public override AstNode Clone()
    {
        return new CppHeaderFileNode(
            FileName,
            HeaderComment != null ? (CommentNode)HeaderComment.Clone() : null,
            PragmaOnce,
            Includes.Select(i => (CppIncludeDirectiveNode)i.Clone()).ToImmutableList(),
            ForwardDeclarations.Select(f => (CppForwardDeclarationNode)f.Clone()).ToImmutableList(),
            UsingAliases.Select(u => (CppUsingAliasNode)u.Clone()).ToImmutableList(),
            Declarations.Select(d => d.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        return $"CppHeaderFile({FileName}, {Declarations.Count} declarations)";
    }
}
