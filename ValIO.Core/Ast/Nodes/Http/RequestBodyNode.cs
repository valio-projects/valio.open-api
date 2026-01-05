using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Http;

/// <summary>
/// Узел request body
/// </summary>
public sealed class RequestBodyNode : AstNode
{
    /// <summary>
    /// Тип body
    /// </summary>
    public TypeNode Type { get; }
    
    /// <summary>
    /// Content type (например, "application/json")
    /// </summary>
    public string? ContentType { get; }
    
    /// <summary>
    /// Является ли body обязательным
    /// </summary>
    public bool IsRequired { get; }
    
    /// <summary>
    /// Тип request body
    /// </summary>
    public RequestBodyKind Kind { get; }
    
    public override AstNodeKind Kind => AstNodeKind.RequestBody;
    
    public override IReadOnlyList<AstNode> Children => new[] { Type };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public RequestBodyNode(
        TypeNode type,
        RequestBodyKind kind = RequestBodyKind.Json,
        string? contentType = null,
        bool isRequired = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Kind = kind;
        ContentType = contentType;
        IsRequired = isRequired;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitRequestBody(this);
    }
    
    public override AstNode Clone()
    {
        return new RequestBodyNode(
            (TypeNode)Type.Clone(),
            Kind,
            ContentType,
            IsRequired,
            Metadata.ToImmutableDictionary());
    }
}
