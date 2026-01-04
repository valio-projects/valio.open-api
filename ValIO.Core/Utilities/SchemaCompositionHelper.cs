using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

namespace ValIO.Core.Utilities;

/// <summary>
/// Утилита для работы с композицией схем (allOf, oneOf, anyOf)
/// </summary>
public static class SchemaCompositionHelper
{
    /// <summary>
    /// Получает имя схемы из ссылки или из resolver
    /// </summary>
    public static string? GetSchemaName(
        IOpenApiSchema schema, 
        ISchemaResolver schemaResolver,
        INamingConverter? namingConverter = null)
    {
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
        {
            var reference = refHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                return namingConverter != null 
                    ? namingConverter.ToPascalCase(reference.Id) 
                    : reference.Id;
            }
        }
        
        var schemaName = schemaResolver.GetSchemaName(schema, string.Empty);
        if (!string.IsNullOrEmpty(schemaName) && schemaName != "object")
        {
            return namingConverter != null 
                ? namingConverter.ToPascalCase(schemaName) 
                : schemaName;
        }
        
        return null;
    }
    
    /// <summary>
    /// Получает оригинальные Properties схемы (до разрешения)
    /// </summary>
    public static IDictionary<string, IOpenApiSchema>? GetOriginalProperties(
        IOpenApiSchema schema,
        IOpenApiSchema resolvedSchema)
    {
        if (schema is OpenApiSchema openApiSchema && openApiSchema.Properties != null)
        {
            return openApiSchema.Properties;
        }
        
        return resolvedSchema.Properties;
    }
    
    /// <summary>
    /// Получает оригинальную схему свойства из под-схемы
    /// </summary>
    public static IOpenApiSchema? GetOriginalPropertySchema(
        IOpenApiSchema subSchema,
        IOpenApiSchema resolvedSubSchema,
        string propertyName)
    {
        if (subSchema is OpenApiSchema openApiSubSchema && openApiSubSchema.Properties != null)
        {
            if (openApiSubSchema.Properties.TryGetValue(propertyName, out var originalPropSchema))
            {
                return originalPropSchema;
            }
        }
        
        if (resolvedSubSchema.Properties != null)
        {
            resolvedSubSchema.Properties.TryGetValue(propertyName, out var propSchema);
            return propSchema;
        }
        
        return null;
    }
    
    /// <summary>
    /// Создает информацию о композиции схем из OpenAPI схемы
    /// </summary>
    public static SchemaCompositionInfo? CreateCompositionInfo(
        IOpenApiSchema schema,
        IOpenApiSchema resolvedSchema,
        ISchemaResolver schemaResolver,
        INamingConverter? namingConverter = null)
    {
        SchemaCompositionType compositionType = SchemaCompositionType.None;
        IList<IOpenApiSchema>? subSchemas = null;
        
        if (resolvedSchema.AllOf != null && resolvedSchema.AllOf.Count > 0)
        {
            compositionType = SchemaCompositionType.AllOf;
            subSchemas = resolvedSchema.AllOf;
        }
        else if (resolvedSchema.OneOf != null && resolvedSchema.OneOf.Count > 0)
        {
            compositionType = SchemaCompositionType.OneOf;
            subSchemas = resolvedSchema.OneOf;
        }
        else if (resolvedSchema.AnyOf != null && resolvedSchema.AnyOf.Count > 0)
        {
            compositionType = SchemaCompositionType.AnyOf;
            subSchemas = resolvedSchema.AnyOf;
        }
        
        if (compositionType == SchemaCompositionType.None || subSchemas == null)
        {
            return null;
        }
        
        var subSchemaInfos = new List<SubSchemaInfo>();
        var referencedSchemaNames = new List<string>();
        
        foreach (var subSchema in subSchemas)
        {
            var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
            var referencedSchemaName = GetSchemaName(subSchema, schemaResolver, namingConverter);
            var isComponentReference = !string.IsNullOrEmpty(referencedSchemaName);
            var originalProperties = GetOriginalProperties(subSchema, resolvedSubSchema);
            
            // Проверяем, является ли это inline схемой (объект без $ref, но с Properties)
            var isInlineSchema = !isComponentReference && 
                                 resolvedSubSchema.Type.HasValue && 
                                 resolvedSubSchema.Type.Value.HasFlag(JsonSchemaType.Object) &&
                                 resolvedSubSchema.Properties != null &&
                                 resolvedSubSchema.Properties.Count > 0;
            
            if (!string.IsNullOrEmpty(referencedSchemaName))
            {
                referencedSchemaNames.Add(referencedSchemaName);
            }
            
            subSchemaInfos.Add(new SubSchemaInfo
            {
                OriginalSchema = subSchema,
                ResolvedSchema = resolvedSubSchema,
                ReferencedSchemaName = referencedSchemaName,
                IsComponentReference = isComponentReference,
                IsInlineSchema = isInlineSchema,
                OriginalProperties = originalProperties
            });
        }
        
        return new SchemaCompositionInfo
        {
            CompositionType = compositionType,
            OriginalSubSchemas = subSchemas.ToList(),
            ResolvedSubSchemas = subSchemaInfos.Select(s => s.ResolvedSchema).ToList(),
            ReferencedSchemaNames = referencedSchemaNames,
            SubSchemaInfos = subSchemaInfos
        };
    }
    
    /// <summary>
    /// Объединяет required свойства из всех под-схем композиции и из самой схемы
    /// </summary>
    public static HashSet<string> MergeRequiredProperties(
        SchemaCompositionInfo compositionInfo,
        IOpenApiSchema resolvedSchema,
        ISchemaResolver schemaResolver)
    {
        var allRequired = new HashSet<string>();
        
        foreach (var subSchemaInfo in compositionInfo.SubSchemaInfos)
        {
            if (subSchemaInfo.ResolvedSchema.Required != null)
            {
                foreach (var req in subSchemaInfo.ResolvedSchema.Required)
                {
                    allRequired.Add(req);
                }
            }
        }
        
        if (resolvedSchema.Required != null)
        {
            foreach (var req in resolvedSchema.Required)
            {
                allRequired.Add(req);
            }
        }
        
        return allRequired;
    }
    
    /// <summary>
    /// Получает все оригинальные свойства из композиции allOf и из самой схемы
    /// </summary>
    public static Dictionary<string, IOpenApiSchema> GetAllOfProperties(
        SchemaCompositionInfo compositionInfo,
        IOpenApiSchema schema,
        IOpenApiSchema resolvedSchema)
    {
        var allProperties = new Dictionary<string, IOpenApiSchema>();
        
        // Собираем свойства из всех под-схем allOf
        foreach (var subSchemaInfo in compositionInfo.SubSchemaInfos)
        {
            if (subSchemaInfo.OriginalProperties != null)
            {
                foreach (var prop in subSchemaInfo.OriginalProperties)
                {
                    allProperties[prop.Key] = prop.Value;
                }
            }
        }
        
        // Добавляем свойства из самой схемы (определенные после allOf)
        var originalSchemaProperties = GetOriginalProperties(schema, resolvedSchema);
        if (originalSchemaProperties != null)
        {
            foreach (var prop in originalSchemaProperties)
            {
                allProperties[prop.Key] = prop.Value;
            }
        }
        
        return allProperties;
    }
}
