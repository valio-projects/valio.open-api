using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Http;

/// <summary>
/// Узел типа ответа
/// </summary>
public sealed class ResponseTypeNode : AstNode
{
    /// <summary>
    /// Тип ответа
    /// </summary>
    public TypeNode Type { get; }
    
    /// <summary>
    /// HTTP статус код
    /// </summary>
    public int StatusCode { get; }
    
    /// <summary>
    /// Является ли ответ успешным
    /// </summary>
    public bool IsSuccess { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ResponseType;
    
    public override IReadOnlyList<AstNode> Children => new[] { Type };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ResponseTypeNode(
        TypeNode type,
        int statusCode,
        bool isSuccess = true,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        StatusCode = statusCode;
        IsSuccess = isSuccess;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IHttpAstVisitor<TResult> httpVisitor)
        {
            return httpVisitor.VisitResponseType(this);
        }
        throw new NotSupportedException($"Visitor must implement IHttpAstVisitor<TResult> to visit ResponseTypeNode");
    }
    
    public override AstNode Clone()
    {
        return new ResponseTypeNode(
            (TypeNode)Type.Clone(),
            StatusCode,
            IsSuccess,
            Metadata.ToImmutableDictionary());
    }
}
