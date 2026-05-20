using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>
/// Узел generic параметра (TypeScript)
/// </summary>
public sealed class GenericParameterNode : AstNode
{
    /// <summary>
    /// Имя generic параметра
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Ограничение (например, extends Type)
    /// </summary>
    public TypeNode? Constraint { get; }
    
    /// <summary>
    /// Значение по умолчанию
    /// </summary>
    public TypeNode? Default { get; }
    
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Constraint != null)
                children.Add(Constraint);
            if (Default != null)
                children.Add(Default);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public GenericParameterNode(
        IdentifierNode name,
        TypeNode? constraint = null,
        TypeNode? @default = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Constraint = constraint;
        Default = @default;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitGenericParameter(this);
        }
        throw new NotSupportedException($"Visitor must implement ITypeScriptAstVisitor<TResult> to visit GenericParameterNode");
    }
    
    public override AstNode Clone()
    {
        return new GenericParameterNode(
            (IdentifierNode)Name.Clone(),
            Constraint != null ? (TypeNode)Constraint.Clone() : null,
            Default != null ? (TypeNode)Default.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
