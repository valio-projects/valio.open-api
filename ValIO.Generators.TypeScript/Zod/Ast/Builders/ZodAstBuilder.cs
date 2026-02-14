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
    /// Строит ZodObjectNode из IrModel
    /// </summary>
    /// <param name="model">IR модель</param>
    /// <returns>AST узел Zod object схемы</returns>
    public ZodObjectNode BuildZodObject(IrModel model)
    {
        var properties = model.Properties.Select(BuildZodProperty).ToImmutableList();
        return new ZodObjectNode(properties);
    }
    
    /// <summary>
    /// Строит ZodPropertyNode из IrProperty
    /// </summary>
    /// <param name="property">IR свойство</param>
    /// <returns>AST узел Zod property</returns>
    public ZodPropertyNode BuildZodProperty(IrProperty property)
    {
        var name = new IdentifierNode(property.Name);
        var schema = BuildZodSchemaFromIrType(property.Type);
        
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
    public ZodSchemaNode BuildZodSchemaFromIrType(ValIO.Core.IR.Types.IrType irType)
    {
        return irType.Kind switch
        {
            IrTypeKind.Primitive => BuildZodPrimitive((IrPrimitiveType)irType),
            IrTypeKind.ModelReference => BuildZodModelReference((IrModelReference)irType),
            IrTypeKind.Array => BuildZodArray((IrArrayType)irType),
            IrTypeKind.Dictionary => BuildZodDictionary((IrDictionaryType)irType),
            IrTypeKind.Union => BuildZodUnion((IrUnionType)irType),
            IrTypeKind.Intersection => BuildZodIntersection((IrIntersectionType)irType),
            IrTypeKind.Nullable => BuildZodNullable((IrNullableType)irType),
            IrTypeKind.Optional => BuildZodOptional((IrOptionalType)irType),
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
            IrPrimitiveKind.Integer => ZodSchemaKind.Number,
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
    /// Строит Zod схему из IrModelReference
    /// </summary>
    private ZodSchemaNode BuildZodModelReference(IrModelReference modelReference)
    {
        // Ссылка на другую Zod схему - сохраняем имя схемы в metadata
        var schemaName = $"{modelReference.ModelName}Schema";
        var metadata = new Dictionary<string, object>
        {
            { "SchemaReference", schemaName }
        }.ToImmutableDictionary();
        return new ZodPrimitiveSchemaNode(ZodSchemaKind.Any, metadata: metadata);
    }
    
    /// <summary>
    /// Строит Zod схему из IrArrayType
    /// </summary>
    private ZodSchemaNode BuildZodArray(IrArrayType arrayType)
    {
        var elementSchema = BuildZodSchemaFromIrType(arrayType.ElementType);
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
    private ZodSchemaNode BuildZodUnion(IrUnionType unionType)
    {
        var variants = unionType.Types.Select(BuildZodSchemaFromIrType).ToImmutableList();
        return new ZodUnionNode(variants);
    }
    
    /// <summary>
    /// Строит Zod схему из IrIntersectionType
    /// </summary>
    private ZodSchemaNode BuildZodIntersection(IrIntersectionType intersectionType)
    {
        var schemas = intersectionType.Types.Select(BuildZodSchemaFromIrType).ToImmutableList();
        return new ZodIntersectionNode(schemas);
    }
    
    /// <summary>
    /// Строит Zod схему из IrNullableType
    /// </summary>
    private ZodSchemaNode BuildZodNullable(IrNullableType nullableType)
    {
        var innerSchema = BuildZodSchemaFromIrType(nullableType.InnerType);
        return new ZodEffectsNode(innerSchema, ZodEffectKind.Nullable);
    }
    
    /// <summary>
    /// Строит Zod схему из IrOptionalType
    /// </summary>
    private ZodSchemaNode BuildZodOptional(IrOptionalType optionalType)
    {
        var innerSchema = BuildZodSchemaFromIrType(optionalType.InnerType);
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
