using Microsoft.OpenApi.Models;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;

namespace ValIO.Core.Services;

/// <summary>
/// Реализация извлечения моделей из OpenAPI документа
/// </summary>
public class ModelExtractor : IModelExtractor
{
    private readonly ISchemaResolver _schemaResolver;
    private readonly INamingConverter _namingConverter;

    public ModelExtractor(ISchemaResolver schemaResolver, INamingConverter namingConverter)
    {
        _schemaResolver = schemaResolver;
        _namingConverter = namingConverter;
    }

    public IReadOnlyList<ApiModel> ExtractModels(OpenApiDocument document)
    {
        var models = new List<ApiModel>();

        if (document.Components?.Schemas == null)
            return models;

        foreach (var schemaEntry in document.Components.Schemas)
        {
            var schemaName = schemaEntry.Key;
            var schema = schemaEntry.Value;

            var apiModel = ExtractModel(schema, schemaName);
            models.Add(apiModel);
        }

        return models;
    }

    public ApiModel ExtractModel(OpenApiSchema schema, string modelName)
    {
        var resolvedSchema = _schemaResolver.ResolveSchema(schema);
        var sanitizedName = _namingConverter.SanitizeIdentifier(modelName);

        var apiModel = new ApiModel
        {
            Name = sanitizedName,
            Description = resolvedSchema.Description,
            OriginalSchema = resolvedSchema,
            Properties = new List<ApiProperty>(),
            RequiredProperties = resolvedSchema.Required?.ToList() ?? new List<string>()
        };

        if (resolvedSchema.Properties != null)
        {
            var properties = new List<ApiProperty>();
            foreach (var propertyEntry in resolvedSchema.Properties)
            {
                var propertyName = propertyEntry.Key;
                var propertySchema = propertyEntry.Value;

                var apiProperty = ExtractProperty(propertyName, propertySchema, resolvedSchema);
                properties.Add(apiProperty);
            }

            apiModel.Properties = properties;
        }

        return apiModel;
    }

    private ApiProperty ExtractProperty(string propertyName, OpenApiSchema propertySchema, OpenApiSchema parentSchema)
    {
        var resolvedPropertySchema = _schemaResolver.ResolveSchema(propertySchema);
        var isRequired = parentSchema.Required?.Contains(propertyName) ?? false;

        return new ApiProperty
        {
            Name = propertyName,
            Type = "object", // Будет определено TypeMapper'ом
            IsRequired = isRequired,
            IsNullable = resolvedPropertySchema.Nullable,
            Description = resolvedPropertySchema.Description,
            DefaultValue = resolvedPropertySchema.Default
        };
    }
}

