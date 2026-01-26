using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Узел Zod union схемы
/// </summary>
public sealed class ZodUnionNode : ZodSchemaNode
{
    /// <summary>
    /// Варианты union
    /// </summary>
    public IReadOnlyList<ZodSchemaNode> Variants { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ZodUnion;
    
    public override IReadOnlyList<AstNode> Children => Variants.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ZodUnionNode(
        IReadOnlyList<ZodSchemaNode> variants,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(ZodSchemaKind.Union, type, metadata)
    {
        if (variants == null || variants.Count == 0)
            throw new ArgumentException("Union must have at least one variant", nameof(variants));
        
        Variants = variants.ToImmutableList();
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodUnion(this);
        }
        return base.Accept(visitor);
    }
    
    public override AstNode Clone()
    {
        return new ZodUnionNode(
            Variants.Select(v => (ZodSchemaNode)v.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
