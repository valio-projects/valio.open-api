using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Узел Zod effects схемы (refine, transform, superRefine)
/// </summary>
public sealed class ZodEffectsNode : ZodSchemaNode
{
    /// <summary>
    /// Внутренняя схема
    /// </summary>
    public ZodSchemaNode InnerSchema { get; }
    
    /// <summary>
    /// Тип эффекта
    /// </summary>
    public ZodEffectKind EffectKind { get; }
    
    /// <summary>
    /// Функция эффекта (опционально)
    /// </summary>
    public ExpressionNode? EffectFunction { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ZodEffects;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { InnerSchema };
            if (EffectFunction != null)
                children.Add(EffectFunction);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ZodEffectsNode(
        ZodSchemaNode innerSchema,
        ZodEffectKind effectKind,
        ExpressionNode? effectFunction = null,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(ZodSchemaKind.Effects, type, metadata)
    {
        InnerSchema = innerSchema ?? throw new ArgumentNullException(nameof(innerSchema));
        EffectKind = effectKind;
        EffectFunction = effectFunction;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodEffects(this);
        }
        return base.Accept(visitor);
    }
    
    public override AstNode Clone()
    {
        return new ZodEffectsNode(
            (ZodSchemaNode)InnerSchema.Clone(),
            EffectKind,
            EffectFunction != null ? (ExpressionNode)EffectFunction.Clone() : null,
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
