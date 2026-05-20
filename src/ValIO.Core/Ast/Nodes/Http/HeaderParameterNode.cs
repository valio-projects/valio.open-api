using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Http;

/// <summary>
/// Узел header параметра
/// </summary>
public sealed class HeaderParameterNode : AstNode
{
    /// <summary>
    /// Имя параметра
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Тип параметра
    /// </summary>
    public TypeNode Type { get; }
    
    /// <summary>
    /// Является ли параметр обязательным
    /// </summary>
    public bool IsRequired { get; }
    
    
    public override IReadOnlyList<AstNode> Children => new AstNode[] { Name, Type };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public HeaderParameterNode(
        IdentifierNode name,
        TypeNode type,
        bool isRequired = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsRequired = isRequired;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IHttpAstVisitor<TResult> httpVisitor)
        {
            return httpVisitor.VisitHeaderParameter(this);
        }
        throw new NotSupportedException($"Visitor must implement IHttpAstVisitor<TResult> to visit HeaderParameterNode");
    }
    
    public override AstNode Clone()
    {
        return new HeaderParameterNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            IsRequired,
            Metadata.ToImmutableDictionary());
    }
}
