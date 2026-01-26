using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Axios.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Axios.Ast.Nodes;

/// <summary>
/// Узел выражения вызова Axios
/// </summary>
public sealed class AxiosCallExpressionNode : ExpressionNode
{
    /// <summary>
    /// Axios instance (например, axios или customAxios)
    /// </summary>
    public ExpressionNode Instance { get; }
    
    /// <summary>
    /// Конфигурация Axios
    /// </summary>
    public AxiosConfigNode Config { get; }
    
    public override AstNodeKind Kind => AstNodeKind.AxiosCallExpression;
    
    public override IReadOnlyList<AstNode> Children => new AstNode[] { Instance, Config };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public AxiosCallExpressionNode(
        ExpressionNode instance,
        AxiosConfigNode config,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IAxiosAstVisitor<TResult> axiosVisitor)
        {
            return axiosVisitor.VisitAxiosCall(this);
        }
        // Fallback на обычный CallExpression, если visitor не поддерживает Axios
        var callExpression = new CallExpressionNode(
            new MemberAccessExpressionNode(Instance, new IdentifierNode("request")),
            new ExpressionNode[] { Config }.ToImmutableList());
        return visitor.VisitCallExpression(callExpression);
    }
    
    public override AstNode Clone()
    {
        return new AxiosCallExpressionNode(
            (ExpressionNode)Instance.Clone(),
            (AxiosConfigNode)Config.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
