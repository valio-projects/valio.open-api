using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Ast.Visitors;
using ValIO.Generators.TypeScript.Axios.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Axios.Ast.Nodes;

/// <summary>
/// Узел конфигурации Axios
/// </summary>
public sealed class AxiosConfigNode : ExpressionNode
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
    
    /// <summary>
    /// Дополнительные свойства (object literal properties)
    /// </summary>
    public IReadOnlyList<PropertyAssignmentNode> Properties { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ObjectLiteralExpression; // Используем существующий kind
    
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
            if (Type != null)
                children.Add(Type);
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
        : base(type, metadata)
    {
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Url = url ?? throw new ArgumentNullException(nameof(url));
        QueryParameters = queryParameters?.ToImmutableList() ?? ImmutableList<QueryParameterNode>.Empty;
        Headers = headers?.ToImmutableList() ?? ImmutableList<HeaderParameterNode>.Empty;
        Body = body;
        Signal = signal;
        Timeout = timeout;
        Properties = properties?.ToImmutableList() ?? ImmutableList<PropertyAssignmentNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IAxiosAstVisitor<TResult> axiosVisitor)
        {
            return axiosVisitor.VisitAxiosConfig(this);
        }
        // Fallback на object literal expression через ITypeScriptAstVisitor
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            var objectLiteral = new ObjectLiteralExpressionNode(Properties, Type, Metadata);
            return typescriptVisitor.VisitObjectLiteralExpression(objectLiteral);
        }
        return visitor.VisitExpression(this);
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
