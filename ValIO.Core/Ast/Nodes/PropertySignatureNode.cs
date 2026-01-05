using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел сигнатуры свойства интерфейса (TypeScript)
/// </summary>
public sealed class PropertySignatureNode : DeclarationNode
{
    /// <summary>
    /// Тип свойства
    /// </summary>
    public TypeNode Type { get; }
    
    /// <summary>
    /// Является ли свойство обязательным (не optional)
    /// </summary>
    public bool IsRequired { get; }
    
    /// <summary>
    /// Readonly модификатор
    /// </summary>
    public bool IsReadonly { get; }
    
    public override AstNodeKind Kind => AstNodeKind.PropertySignature;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Type };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public PropertySignatureNode(
        IdentifierNode name,
        TypeNode type,
        bool isRequired = true,
        bool isReadonly = false,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes, metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsRequired = isRequired;
        IsReadonly = isReadonly;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitPropertySignature(this);
        }
        throw new NotSupportedException($"Visitor must implement ITypeScriptAstVisitor<TResult> to visit PropertySignatureNode");
    }
    
    public override AstNode Clone()
    {
        return new PropertySignatureNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            IsRequired,
            IsReadonly,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
