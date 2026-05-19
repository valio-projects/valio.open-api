using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;

namespace ValIO.Generators.CSharp.Ast.Nodes;

/// <summary>
/// Узел using директивы (C#)
/// </summary>
public sealed class UsingDirectiveNode : AstNode
{
    /// <summary>
    /// Namespace
    /// </summary>
    public IdentifierNode Namespace { get; }
    
    /// <summary>
    /// Алиас (опционально)
    /// </summary>
    public IdentifierNode? Alias { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Namespace };
            if (Alias != null)
                children.Add(Alias);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public UsingDirectiveNode(
        IdentifierNode @namespace,
        IdentifierNode? alias = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Namespace = @namespace ?? throw new ArgumentNullException(nameof(@namespace));
        Alias = alias;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICSharpAstVisitor<TResult> csharpVisitor)
        {
            return csharpVisitor.VisitUsingDirective(this);
        }
        throw new NotSupportedException($"Visitor must implement ICSharpAstVisitor<TResult> to visit UsingDirectiveNode");
    }
    
    public override AstNode Clone()
    {
        return new UsingDirectiveNode(
            (IdentifierNode)Namespace.Clone(),
            Alias != null ? (IdentifierNode)Alias.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
