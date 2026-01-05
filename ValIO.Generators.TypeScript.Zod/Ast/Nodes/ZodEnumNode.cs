using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Узел Zod enum схемы
/// </summary>
public sealed class ZodEnumNode : ZodSchemaNode
{
    /// <summary>
    /// Значения enum
    /// </summary>
    public IReadOnlyList<ZodEnumValueNode> Values { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ZodEnum;
    
    public override IReadOnlyList<AstNode> Children => Values.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ZodEnumNode(
        IReadOnlyList<ZodEnumValueNode> values,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(ZodSchemaKind.Enum, type, metadata)
    {
        if (values == null || values.Count == 0)
            throw new ArgumentException("Enum must have at least one value", nameof(values));
        
        Values = values.ToImmutableList();
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodEnum(this);
        }
        return base.Accept(visitor);
    }
    
    public override AstNode Clone()
    {
        return new ZodEnumNode(
            Values.Select(v => (ZodEnumValueNode)v.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
