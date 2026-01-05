using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Http;

/// <summary>
/// Узел HTTP route
/// </summary>
public sealed class RouteNode : AstNode
{
    /// <summary>
    /// Путь (например, "/api/users/{id}")
    /// </summary>
    public string Path { get; }
    
    /// <summary>
    /// Path параметры
    /// </summary>
    public IReadOnlyList<PathParameterNode> PathParameters { get; }
    
    public override AstNodeKind Kind => AstNodeKind.Route;
    
    public override IReadOnlyList<AstNode> Children => PathParameters.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public RouteNode(
        string path,
        IReadOnlyList<PathParameterNode>? pathParameters = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        PathParameters = pathParameters?.ToImmutableList() ?? ImmutableList<PathParameterNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IHttpAstVisitor<TResult> httpVisitor)
        {
            return httpVisitor.VisitRoute(this);
        }
        throw new NotSupportedException($"Visitor must implement IHttpAstVisitor<TResult> to visit RouteNode");
    }
    
    public override AstNode Clone()
    {
        return new RouteNode(
            Path,
            PathParameters.Select(p => (PathParameterNode)p.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
