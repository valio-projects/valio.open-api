using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Zod.Ast.Builders;

/// <summary>
/// Построитель AST для Zod специфичных узлов
/// Использует IR для построения AST узлов
/// </summary>
public class ZodAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly Func<IrType, TypeNode> _typeBuilder;
    
    /// <summary>
    /// Конструктор
    /// </summary>
    /// <param name="namingConverter">Конвертер имен</param>
    /// <param name="typeBuilder">Функция для построения TypeNode из IrType</param>
    public ZodAstBuilder(
        INamingConverter namingConverter,
        Func<IrType, TypeNode> typeBuilder)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeBuilder = typeBuilder ?? throw new ArgumentNullException(nameof(typeBuilder));
    }
    
    /// <summary>
    /// Строит ZodSchemaNode из IrModel с учётом oneOf, allOf, anyOf
    /// </summary>
    /// <param name="model">IR модель</param>
    /// <returns>AST узел Zod схемы (object, union или intersection)</returns>
    public ZodSchemaNode BuildZodSchemaFromIrModel(IrModel model)
    {
        if (model.Composition != null && model.Composition.Types.Count > 0)
        {
            switch (model.Composition.Kind)
            {
                case IrCompositionKind.OneOf:
                case IrCompositionKind.AnyOf:
                    var unionVariants = model.Composition.Types
                        .Select(t => BuildZodSchemaFromIrType(t, model.Name))
                        .ToImmutableList();
                    return new ZodUnionNode(unionVariants);
                case IrCompositionKind.AllOf:
                    var allOfSchemas = model.Composition.Types
                        .Select(t => BuildZodSchemaFromIrType(t, model.Name))
                        .ToList();
                    if (model.Properties.Count > 0)
                    {
                        var ownPropsSchema = BuildZodObject(model);
                        allOfSchemas.Add(ownPropsSchema);
                    }
                    if (allOfSchemas.Count == 1)
                        return allOfSchemas[0];
                    return new ZodIntersectionNode(allOfSchemas);
            }
        }

        return BuildZodObject(model);
    }

    /// <summary>
    /// Строит ZodObjectNode из IrModel (только properties, без composition)
    /// </summary>
    /// <param name="model">IR модель</param>
    /// <returns>AST узел Zod object схемы</returns>
    public ZodObjectNode BuildZodObject(IrModel model)
    {
        var properties = model.Properties.Select(p => BuildZodProperty(p, model.Name)).ToImmutableList();
        return new ZodObjectNode(properties);
    }

    /// <summary>
    /// Строит ZodObjectNode из списка IrParameter (для route/query params)
    /// </summary>
    public ZodObjectNode BuildZodObjectFromParameters(IReadOnlyList<IrParameter> parameters)
    {
        var properties = parameters
            .Select(p => new ZodPropertyNode(
                name: new IdentifierNode(p.Name),
                schema: BuildZodSchemaFromIrType(p.Type),
                isRequired: p.IsRequired,
                isNullable: p.Type is IrNullableType,
                isOptional: !p.IsRequired))
            .ToImmutableList();
        return new ZodObjectNode(properties);
    }
    
    /// <summary>
    /// Строит ZodPropertyNode из IrProperty
    /// </summary>
    /// <param name="property">IR свойство</param>
    /// <returns>AST узел Zod property</returns>
    public ZodPropertyNode BuildZodProperty(IrProperty property, string? currentModelName = null)
    {
        var name = new IdentifierNode(property.Name);
        var schema = BuildZodSchemaFromIrType(property.Type, currentModelName);
        
        if (property.Constraints != null)
        {
            schema = ApplyConstraints(schema, property.Constraints);
        }
        
        return new ZodPropertyNode(
            name: name,
            schema: schema,
            isRequired: property.IsRequired,
            isNullable: property.Type is IrNullableType,
            isOptional: !property.IsRequired);
    }
    
    /// <summary>
    /// Строит ZodSchemaNode из IrType
    /// </summary>
    /// <param name="irType">IR тип</param>
    /// <returns>AST узел Zod схемы</returns>
    public ZodSchemaNode BuildZodSchemaFromIrType(ValIO.Core.IR.Types.IrType irType, string? currentModelName = null)
    {
        return irType.Kind switch
        {
            IrTypeKind.Primitive => BuildZodPrimitive((IrPrimitiveType)irType),
            IrTypeKind.ModelReference => BuildZodModelReference((IrModelReference)irType, currentModelName),
            IrTypeKind.Array => BuildZodArray((IrArrayType)irType, currentModelName),
            IrTypeKind.Dictionary => BuildZodDictionary((IrDictionaryType)irType),
            IrTypeKind.Union => BuildZodUnion((IrUnionType)irType, currentModelName),
            IrTypeKind.Intersection => BuildZodIntersection((IrIntersectionType)irType, currentModelName),
            IrTypeKind.Nullable => BuildZodNullable((IrNullableType)irType, currentModelName),
            IrTypeKind.Optional => BuildZodOptional((IrOptionalType)irType, currentModelName),
            _ => BuildZodAny()
        };
    }
    
    /// <summary>
    /// Строит ZodEnumNode из IrEnum
    /// </summary>
    /// <param name="enumModel">IR enum</param>
    /// <returns>AST узел Zod enum схемы</returns>
    public ZodEnumNode BuildZodEnum(IrEnum enumModel)
    {
        var values = enumModel.Values.Select(v =>
        {
            var name = new IdentifierNode(v.Name);
            var value = v.Value switch
            {
                string str => new LiteralExpressionNode(str, LiteralKind.String),
                int i => new LiteralExpressionNode(i, LiteralKind.Number),
                long l => new LiteralExpressionNode(l, LiteralKind.Number),
                short s => new LiteralExpressionNode(s, LiteralKind.Number),
                byte b => new LiteralExpressionNode(b, LiteralKind.Number),
                _ => new LiteralExpressionNode(v.Value?.ToString() ?? "", LiteralKind.String)
            };
            return new ZodEnumValueNode(name, value);
        }).ToImmutableList();
        
        return new ZodEnumNode(values);
    }
    
    /// <summary>
    /// Строит ZodUnionNode для oneOf/anyOf
    /// </summary>
    public ZodUnionNode BuildZodUnion(IReadOnlyList<ZodSchemaNode> variants)
    {
        return new ZodUnionNode(variants);
    }
    
    /// <summary>
    /// Строит ZodIntersectionNode для allOf
    /// </summary>
    public ZodIntersectionNode BuildZodIntersection(IReadOnlyList<ZodSchemaNode> schemas)
    {
        return new ZodIntersectionNode(schemas);
    }
    
    // Приватные вспомогательные методы для IR типов
    
    /// <summary>
    /// Строит Zod схему из IrPrimitiveType
    /// </summary>
    private ZodSchemaNode BuildZodPrimitive(IrPrimitiveType primitiveType)
    {
        var schemaKind = primitiveType.PrimitiveKind switch
        {
            IrPrimitiveKind.String => ZodSchemaKind.String,
            IrPrimitiveKind.Number => ZodSchemaKind.Number,
            IrPrimitiveKind.Integer => ZodSchemaKind.Integer,
            IrPrimitiveKind.Long => ZodSchemaKind.BigInt,
            IrPrimitiveKind.Boolean => ZodSchemaKind.Boolean,
            IrPrimitiveKind.DateTime => ZodSchemaKind.String,
            IrPrimitiveKind.Date => ZodSchemaKind.String,
            IrPrimitiveKind.Time => ZodSchemaKind.String,
            // Binary, ByteArray, File - используем Any, так как в TypeScript они обычно передаются как any или string
            IrPrimitiveKind.Binary => ZodSchemaKind.Any,
            IrPrimitiveKind.ByteArray => ZodSchemaKind.Any,
            IrPrimitiveKind.Uuid => ZodSchemaKind.String,
            IrPrimitiveKind.File => ZodSchemaKind.Any,
            _ => ZodSchemaKind.Any
        };
        
        return new ZodPrimitiveSchemaNode(schemaKind);
    }
    
    /// <summary>
    /// Строит Zod схему из IrModelReference.
    /// При self-reference (modelReference.ModelName == currentModelName) использует z.lazy().
    /// </summary>
    private ZodSchemaNode BuildZodModelReference(IrModelReference modelReference, string? currentModelName = null)
    {
        var schemaName = $"{modelReference.ModelName}Schema";
        var isLazy = !string.IsNullOrEmpty(currentModelName)
            && string.Equals(modelReference.ModelName, currentModelName, StringComparison.OrdinalIgnoreCase);
        return new ZodPrimitiveSchemaNode(ZodSchemaKind.Any, schemaReference: schemaName, isLazy: isLazy);
    }
    
    /// <summary>
    /// Строит Zod схему из IrArrayType
    /// </summary>
    private ZodSchemaNode BuildZodArray(IrArrayType arrayType, string? currentModelName = null)
    {
        var elementSchema = BuildZodSchemaFromIrType(arrayType.ElementType, currentModelName);
        return new ZodArrayNode(elementSchema);
    }
    
    /// <summary>
    /// Строит Zod схему из IrDictionaryType
    /// </summary>
    private ZodSchemaNode BuildZodDictionary(IrDictionaryType dictionaryType)
    {
        var valueSchema = BuildZodSchemaFromIrType(dictionaryType.ValueType);
        return BuildZodAny();
    }
    
    /// <summary>
    /// Строит Zod схему из IrUnionType
    /// </summary>
    private ZodSchemaNode BuildZodUnion(IrUnionType unionType, string? currentModelName = null)
    {
        var variants = unionType.Types.Select(t => BuildZodSchemaFromIrType(t, currentModelName)).ToImmutableList();
        return new ZodUnionNode(variants);
    }
    
    /// <summary>
    /// Строит Zod схему из IrIntersectionType
    /// </summary>
    private ZodSchemaNode BuildZodIntersection(IrIntersectionType intersectionType, string? currentModelName = null)
    {
        var schemas = intersectionType.Types.Select(t => BuildZodSchemaFromIrType(t, currentModelName)).ToImmutableList();
        return new ZodIntersectionNode(schemas);
    }
    
    /// <summary>
    /// Строит Zod схему из IrNullableType
    /// </summary>
    private ZodSchemaNode BuildZodNullable(IrNullableType nullableType, string? currentModelName = null)
    {
        var innerSchema = BuildZodSchemaFromIrType(nullableType.InnerType, currentModelName);
        return new ZodEffectsNode(innerSchema, ZodEffectKind.Nullable);
    }
    
    /// <summary>
    /// Строит Zod схему из IrOptionalType
    /// </summary>
    private ZodSchemaNode BuildZodOptional(IrOptionalType optionalType, string? currentModelName = null)
    {
        var innerSchema = BuildZodSchemaFromIrType(optionalType.InnerType, currentModelName);
        return new ZodEffectsNode(innerSchema, ZodEffectKind.Optional);
    }
    
    /// <summary>
    /// Применяет constraints к Zod схеме
    /// </summary>
    private ZodSchemaNode ApplyConstraints(ZodSchemaNode schema, IrPropertyConstraints constraints)
    {
        return schema;
    }
    
    private ZodSchemaNode BuildZodAny()
    {
        return new ZodPrimitiveSchemaNode(ZodSchemaKind.Any);
    }
}
