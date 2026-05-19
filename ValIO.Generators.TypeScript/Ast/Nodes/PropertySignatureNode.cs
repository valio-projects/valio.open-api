using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

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
    
    /// <summary>
    /// Пример значения для JSDoc @example
    /// </summary>
    public string? Example { get; }
    
    /// <summary>
    /// TODO-метка для JSDoc @todo
    /// </summary>
    public string? Todo { get; }
    
    /// <summary>
    /// Свойство устарело (deprecated)
    /// </summary>
    public bool IsDeprecated { get; }
    
    /// <summary>
    /// Значение является enum
    /// </summary>
    public bool IsEnum { get; }
    
    /// <summary>
    /// Значение является константой
    /// </summary>
    public bool IsConstant { get; }
    
    
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
        IReadOnlyDictionary<string, object>? metadata = null,
        string? example = null,
        string? todo = null,
        bool isDeprecated = false,
        bool isEnum = false,
        bool isConstant = false)
        : base(name, documentation, attributes, metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsRequired = isRequired;
        IsReadonly = isReadonly;
        Example = example;
        Todo = todo;
        IsDeprecated = isDeprecated;
        IsEnum = isEnum;
        IsConstant = isConstant;
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
            Metadata.ToImmutableDictionary(),
            Example,
            Todo,
            IsDeprecated,
            IsEnum,
            IsConstant);
    }
}
