using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Узел Zod array схемы
/// </summary>
public sealed class ZodArrayNode : ZodSchemaNode
{
    /// <summary>
    /// Схема элементов массива
    /// </summary>
    public ZodSchemaNode ElementSchema { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ZodArray;
    
    public override IReadOnlyList<AstNode> Children => new[] { ElementSchema };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ZodArrayNode(
        ZodSchemaNode elementSchema,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(ZodSchemaKind.Array, type, metadata)
    {
        ElementSchema = elementSchema ?? throw new ArgumentNullException(nameof(elementSchema));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodArray(this);
        }
        return base.Accept(visitor);
    }
    
    public override AstNode Clone()
    {
        return new ZodArrayNode(
            (ZodSchemaNode)ElementSchema.Clone(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
