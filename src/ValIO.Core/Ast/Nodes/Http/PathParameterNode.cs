using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Http;

/// <summary>
/// Узел path параметра
/// </summary>
public sealed class PathParameterNode : AstNode
{
    /// <summary>
    /// Имя параметра
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Тип параметра
    /// </summary>
    public TypeNode Type { get; }
    
    
    public override IReadOnlyList<AstNode> Children => new AstNode[] { Name, Type };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public PathParameterNode(
        IdentifierNode name,
        TypeNode type,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IHttpAstVisitor<TResult> httpVisitor)
        {
            return httpVisitor.VisitPathParameter(this);
        }
        throw new NotSupportedException($"Visitor must implement IHttpAstVisitor<TResult> to visit PathParameterNode");
    }
    
    public override AstNode Clone()
    {
        return new PathParameterNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            Metadata.ToImmutableDictionary());
    }
}
