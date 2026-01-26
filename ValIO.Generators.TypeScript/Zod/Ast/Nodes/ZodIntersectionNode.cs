using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Узел Zod intersection схемы
/// </summary>
public sealed class ZodIntersectionNode : ZodSchemaNode
{
    /// <summary>
    /// Схемы для intersection
    /// </summary>
    public IReadOnlyList<ZodSchemaNode> Schemas { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ZodIntersection;
    
    public override IReadOnlyList<AstNode> Children => Schemas.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ZodIntersectionNode(
        IReadOnlyList<ZodSchemaNode> schemas,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(ZodSchemaKind.Intersection, type, metadata)
    {
        if (schemas == null || schemas.Count < 2)
            throw new ArgumentException("Intersection must have at least two schemas", nameof(schemas));
        
        Schemas = schemas.ToImmutableList();
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodIntersection(this);
        }
        return base.Accept(visitor);
    }
    
    public override AstNode Clone()
    {
        return new ZodIntersectionNode(
            Schemas.Select(s => (ZodSchemaNode)s.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
