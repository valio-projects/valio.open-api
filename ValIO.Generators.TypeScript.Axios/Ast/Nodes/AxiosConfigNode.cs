using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Axios.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Axios.Ast.Nodes;

/// <summary>
/// Узел конфигурации Axios
/// </summary>
public sealed class AxiosConfigNode : ObjectLiteralExpressionNode
{
    /// <summary>
    /// HTTP метод
    /// </summary>
    public HttpMethodNode Method { get; }
    
    /// <summary>
    /// URL (может быть template literal)
    /// </summary>
    public ExpressionNode Url { get; }
    
    /// <summary>
    /// Query параметры
    /// </summary>
    public IReadOnlyList<QueryParameterNode> QueryParameters { get; }
    
    /// <summary>
    /// Headers
    /// </summary>
    public IReadOnlyList<HeaderParameterNode> Headers { get; }
    
    /// <summary>
    /// Request body
    /// </summary>
    public RequestBodyNode? Body { get; }
    
    /// <summary>
    /// AbortSignal
    /// </summary>
    public ExpressionNode? Signal { get; }
    
    /// <summary>
    /// Timeout
    /// </summary>
    public ExpressionNode? Timeout { get; }
    
    public override AstNodeKind Kind => AstNodeKind.AxiosConfig;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Method, Url };
            children.AddRange(QueryParameters);
            children.AddRange(Headers);
            if (Body != null)
                children.Add(Body);
            if (Signal != null)
                children.Add(Signal);
            if (Timeout != null)
                children.Add(Timeout);
            children.AddRange(Properties);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public AxiosConfigNode(
        HttpMethodNode method,
        ExpressionNode url,
        IReadOnlyList<QueryParameterNode>? queryParameters = null,
        IReadOnlyList<HeaderParameterNode>? headers = null,
        RequestBodyNode? body = null,
        ExpressionNode? signal = null,
        ExpressionNode? timeout = null,
        IReadOnlyList<PropertyAssignmentNode>? properties = null,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(properties ?? ImmutableList<PropertyAssignmentNode>.Empty, type, metadata)
    {
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Url = url ?? throw new ArgumentNullException(nameof(url));
        QueryParameters = queryParameters?.ToImmutableList() ?? ImmutableList<QueryParameterNode>.Empty;
        Headers = headers?.ToImmutableList() ?? ImmutableList<HeaderParameterNode>.Empty;
        Body = body;
        Signal = signal;
        Timeout = timeout;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IAxiosAstVisitor<TResult> axiosVisitor)
        {
            return axiosVisitor.VisitAxiosConfig(this);
        }
        return visitor.VisitObjectLiteralExpression(this);
    }
    
    public override AstNode Clone()
    {
        return new AxiosConfigNode(
            (HttpMethodNode)Method.Clone(),
            (ExpressionNode)Url.Clone(),
            QueryParameters.Select(q => (QueryParameterNode)q.Clone()).ToImmutableList(),
            Headers.Select(h => (HeaderParameterNode)h.Clone()).ToImmutableList(),
            Body != null ? (RequestBodyNode)Body.Clone() : null,
            Signal != null ? (ExpressionNode)Signal.Clone() : null,
            Timeout != null ? (ExpressionNode)Timeout.Clone() : null,
            Properties.Select(p => (PropertyAssignmentNode)p.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
