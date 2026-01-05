using ValIO.Generators.TypeScript.Zod.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Zod.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для Zod специфичных AST узлов
/// </summary>
public interface IZodAstVisitor<TResult>
{
    /// <summary>
    /// Посещает Zod схему (базовый метод)
    /// </summary>
    TResult VisitZodSchema(ZodSchemaNode node);
    
    /// <summary>
    /// Посещает Zod object схему
    /// </summary>
    TResult VisitZodObject(ZodObjectNode node);
    
    /// <summary>
    /// Посещает Zod array схему
    /// </summary>
    TResult VisitZodArray(ZodArrayNode node);
    
    /// <summary>
    /// Посещает Zod union схему
    /// </summary>
    TResult VisitZodUnion(ZodUnionNode node);
    
    /// <summary>
    /// Посещает Zod intersection схему
    /// </summary>
    TResult VisitZodIntersection(ZodIntersectionNode node);
    
    /// <summary>
    /// Посещает Zod enum схему
    /// </summary>
    TResult VisitZodEnum(ZodEnumNode node);
    
    /// <summary>
    /// Посещает Zod enum value
    /// </summary>
    TResult VisitZodEnumValue(ZodEnumValueNode node);
    
    /// <summary>
    /// Посещает Zod property
    /// </summary>
    TResult VisitZodProperty(ZodPropertyNode node);
    
    /// <summary>
    /// Посещает Zod effects схему
    /// </summary>
    TResult VisitZodEffects(ZodEffectsNode node);
}
