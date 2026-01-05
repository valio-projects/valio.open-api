using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
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
    
    public override AstNodeKind Kind => AstNodeKind.PathParameter;
    
    public override IReadOnlyList<AstNode> Children => new[] { Name, Type };
    
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
        return visitor.VisitPathParameter(this);
    }
    
    public override AstNode Clone()
    {
        return new PathParameterNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            Metadata.ToImmutableDictionary());
    }
}
