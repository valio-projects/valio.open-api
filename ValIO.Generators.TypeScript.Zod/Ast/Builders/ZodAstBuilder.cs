using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.Zod.Ast.Builders;

/// <summary>
/// Построитель AST для Zod специфичных узлов
/// </summary>
public class ZodAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly ITypeMapper _typeMapper;
    
    public ZodAstBuilder(INamingConverter namingConverter, ITypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
    }
    
    /// <summary>
    /// Строит ZodObjectNode из ModelTemplateData
    /// </summary>
    public ZodObjectNode BuildZodObject(ModelTemplateData<TypeScriptModelTemplateDataOptions, TypeScriptPropertyTemplateDataOptions> modelData)
    {
        var properties = modelData.Properties.Select(BuildZodProperty).ToImmutableList();
        return new ZodObjectNode(properties);
    }
    
    /// <summary>
    /// Строит ZodPropertyNode из PropertyTemplateData
    /// </summary>
    public ZodPropertyNode BuildZodProperty(PropertyTemplateData<TypeScriptPropertyTemplateDataOptions> propertyData)
    {
        var name = new IdentifierNode(propertyData.Name);
        var schema = BuildZodSchemaFromTypeInfo(propertyData.TypeInfo);
        
        // Применяем optional/nullable модификаторы
        if (!propertyData.IsRequired && propertyData.IsOptional)
        {
            schema = new ZodEffectsNode(schema, ZodEffectKind.Refine);
        }
        
        if (propertyData.IsNullable)
        {
            schema = new ZodEffectsNode(schema, ZodEffectKind.Refine);
        }
        
        return new ZodPropertyNode(
            name: name,
            schema: schema,
            isRequired: propertyData.IsRequired,
            isNullable: propertyData.IsNullable,
            isOptional: propertyData.IsOptional);
    }
    
    /// <summary>
    /// Строит ZodSchemaNode из TypeInfo
    /// </summary>
    public ZodSchemaNode BuildZodSchemaFromTypeInfo(TypeInfo typeInfo)
    {
        if (typeInfo.IsCollection)
        {
            var elementSchema = typeInfo.GenericArguments.FirstOrDefault() != null
                ? BuildZodSchemaFromTypeInfo(typeInfo.GenericArguments[0])
                : BuildZodAny();
            return new ZodArrayNode(elementSchema);
        }
        
        if (typeInfo.IsDictionary)
        {
            // Dictionary в Zod представляется как z.record()
            var valueSchema = typeInfo.GenericArguments.ElementAtOrDefault(1) != null
                ? BuildZodSchemaFromTypeInfo(typeInfo.GenericArguments[1])
                : BuildZodAny();
            // TODO: Реализовать ZodRecordNode если нужно
            return BuildZodAny();
        }
        
        // Примитивные типы
        if (typeInfo.IsPrimitive)
        {
            return BuildZodPrimitive(typeInfo.BaseType);
        }
        
        // Ссылочные типы (объекты)
        return BuildZodReference(typeInfo.BaseType);
    }
    
    /// <summary>
    /// Строит ZodEnumNode из EnumTemplateData
    /// </summary>
    public ZodEnumNode BuildZodEnum(EnumTemplateData<TypeScriptEnumTemplateDataOptions> enumData)
    {
        var values = enumData.Values.Select(v =>
        {
            var name = new IdentifierNode(v.Name ?? v.Value?.ToString() ?? "Unknown");
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
    
    // Приватные вспомогательные методы
    
    private ZodSchemaNode BuildZodPrimitive(string baseType)
    {
        // Создаем простые Zod схемы для примитивных типов
        // Используем ZodSchemaNode с соответствующим ZodSchemaKind
        return baseType.ToLowerInvariant() switch
        {
            "string" => new ZodPrimitiveSchemaNode(ZodSchemaKind.String),
            "number" or "int" or "integer" => new ZodPrimitiveSchemaNode(ZodSchemaKind.Number),
            "boolean" or "bool" => new ZodPrimitiveSchemaNode(ZodSchemaKind.Boolean),
            "null" => new ZodPrimitiveSchemaNode(ZodSchemaKind.Null),
            _ => new ZodPrimitiveSchemaNode(ZodSchemaKind.Any)
        };
    }
    
    private ZodSchemaNode BuildZodAny()
    {
        return new ZodPrimitiveSchemaNode(ZodSchemaKind.Any);
    }
    
    private ZodSchemaNode BuildZodReference(string typeName)
    {
        // Ссылка на другую Zod схему
        // TODO: Реализовать ZodReferenceNode если нужно
        // Пока возвращаем any, так как ссылки будут разрешаться на уровне рендеринга
        return new ZodPrimitiveSchemaNode(ZodSchemaKind.Any);
    }
}
