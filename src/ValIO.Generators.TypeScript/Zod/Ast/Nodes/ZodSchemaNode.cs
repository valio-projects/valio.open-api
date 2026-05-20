using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Базовый узел Zod схемы
/// </summary>
public abstract class ZodSchemaNode : ExpressionNode
{
    /// <summary>
    /// Тип Zod схемы
    /// </summary>
    public ZodSchemaKind SchemaKind { get; }
    
    
    /// <summary>
    /// Конструктор
    /// </summary>
    protected ZodSchemaNode(
        ZodSchemaKind schemaKind,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(type, metadata)
    {
        SchemaKind = schemaKind;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodSchema(this);
        }
        return visitor.DefaultVisit(this);
    }
    
    public abstract override AstNode Clone();
}
