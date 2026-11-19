using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>
/// Узел объявления type alias (TypeScript)
/// </summary>
public sealed class TypeAliasDeclarationNode : DeclarationNode
{
    /// <summary>
    /// Тип алиаса
    /// </summary>
    public TypeNode Type { get; }
    
    
    /// <summary>Gets the type parameters that apply to the aliased type.</summary>
    /// <value>The ordered immutable type parameter declarations.</value>
    public IReadOnlyList<GenericParameterNode> GenericParameters { get; }

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Type };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            children.AddRange(GenericParameters);
            return children;
        }
    }
    
    /// <summary>Creates a named TypeScript alias with optional type parameters.</summary>
    /// <param name="name">The declared name without type arguments.</param>
    /// <param name="type">The aliased type expression.</param>
    /// <param name="documentation">The optional declaration documentation.</param>
    /// <param name="attributes">The optional declaration attributes.</param>
    /// <param name="metadata">The optional AST metadata.</param>
    /// <param name="genericParameters">The type parameters in declaration order.</param>
    /// <exception cref="ArgumentNullException">The aliased type is null.</exception>
    public TypeAliasDeclarationNode(
        IdentifierNode name,
        TypeNode type,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        IReadOnlyList<GenericParameterNode>? genericParameters = null)
        : base(name, documentation, attributes, metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        GenericParameters = genericParameters?.ToImmutableList() ?? ImmutableList<GenericParameterNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitTypeAliasDeclaration(this);
        }
        throw new NotSupportedException($"Visitor must implement ITypeScriptAstVisitor<TResult> to visit TypeAliasDeclarationNode");
    }
    
    /// <inheritdoc />
    public override AstNode Clone()
    {
        return new TypeAliasDeclarationNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary(),
            GenericParameters.Select(parameter => (GenericParameterNode)parameter.Clone()).ToImmutableList());
    }
}
