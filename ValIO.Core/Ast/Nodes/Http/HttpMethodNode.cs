using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Http;

/// <summary>
/// Узел HTTP метода
/// </summary>
public sealed class HttpMethodNode : AstNode
{
    /// <summary>
    /// HTTP метод
    /// </summary>
    public HttpMethodKind Method { get; }
    
    public override AstNodeKind Kind => AstNodeKind.HttpMethod;
    
    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public HttpMethodNode(
        HttpMethodKind method,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Method = method;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitHttpMethod(this);
    }
    
    public override AstNode Clone()
    {
        return new HttpMethodNode(Method, Metadata.ToImmutableDictionary());
    }
}
