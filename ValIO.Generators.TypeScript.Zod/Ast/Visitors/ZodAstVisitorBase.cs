using ValIO.Generators.TypeScript.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Zod.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя Zod AST узлов
/// </summary>
public abstract class ZodAstVisitorBase<TResult> : TypeScriptAstVisitorBase<TResult>, IZodAstVisitor<TResult>
{
    // Абстрактные методы только для Zod узлов
    public abstract TResult VisitZodSchema(ZodSchemaNode node);
    public abstract TResult VisitZodObject(ZodObjectNode node);
    public abstract TResult VisitZodArray(ZodArrayNode node);
    public abstract TResult VisitZodUnion(ZodUnionNode node);
    public abstract TResult VisitZodIntersection(ZodIntersectionNode node);
    public abstract TResult VisitZodEnum(ZodEnumNode node);
    public abstract TResult VisitZodEnumValue(ZodEnumValueNode node);
    public abstract TResult VisitZodProperty(ZodPropertyNode node);
    public abstract TResult VisitZodEffects(ZodEffectsNode node);
}
