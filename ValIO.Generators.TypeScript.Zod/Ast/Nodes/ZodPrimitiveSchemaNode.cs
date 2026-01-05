using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Узел Zod примитивной схемы (string, number, boolean, null, any)
/// </summary>
public sealed class ZodPrimitiveSchemaNode : ZodSchemaNode
{
    public override AstNodeKind Kind => AstNodeKind.ZodSchema;
    
    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ZodPrimitiveSchemaNode(
        ZodSchemaKind schemaKind,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(schemaKind, type, metadata)
    {
        // Валидация: только примитивные типы
        if (schemaKind != ZodSchemaKind.String &&
            schemaKind != ZodSchemaKind.Number &&
            schemaKind != ZodSchemaKind.Integer &&
            schemaKind != ZodSchemaKind.Boolean &&
            schemaKind != ZodSchemaKind.Null &&
            schemaKind != ZodSchemaKind.Any)
        {
            throw new ArgumentException($"Invalid primitive schema kind: {schemaKind}", nameof(schemaKind));
        }
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodSchema(this);
        }
        return base.Accept(visitor);
    }
    
    public override AstNode Clone()
    {
        return new ZodPrimitiveSchemaNode(
            SchemaKind,
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
