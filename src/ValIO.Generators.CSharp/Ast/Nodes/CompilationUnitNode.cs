using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;

namespace ValIO.Generators.CSharp.Ast.Nodes;

/// <summary>
/// Узел единицы компиляции (C#)
/// </summary>
public sealed class CompilationUnitNode : AstNode
{
    /// <summary>
    /// Using директивы
    /// </summary>
    public IReadOnlyList<UsingDirectiveNode> Usings { get; }
    
    /// <summary>
    /// Namespaces
    /// </summary>
    public IReadOnlyList<NamespaceDeclarationNode> Namespaces { get; }
    
    /// <summary>
    /// Объявления верхнего уровня
    /// </summary>
    public IReadOnlyList<DeclarationNode> Declarations { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            children.AddRange(Usings);
            children.AddRange(Namespaces);
            children.AddRange(Declarations);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public CompilationUnitNode(
        IReadOnlyList<UsingDirectiveNode>? usings = null,
        IReadOnlyList<NamespaceDeclarationNode>? namespaces = null,
        IReadOnlyList<DeclarationNode>? declarations = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Usings = usings?.ToImmutableList() ?? ImmutableList<UsingDirectiveNode>.Empty;
        Namespaces = namespaces?.ToImmutableList() ?? ImmutableList<NamespaceDeclarationNode>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICSharpAstVisitor<TResult> csharpVisitor)
        {
            return csharpVisitor.VisitCompilationUnit(this);
        }
        throw new NotSupportedException($"Visitor must implement ICSharpAstVisitor<TResult> to visit CompilationUnitNode");
    }
    
    public override AstNode Clone()
    {
        return new CompilationUnitNode(
            Usings.Select(u => (UsingDirectiveNode)u.Clone()).ToImmutableList(),
            Namespaces.Select(n => (NamespaceDeclarationNode)n.Clone()).ToImmutableList(),
            Declarations.Select(d => (DeclarationNode)d.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
