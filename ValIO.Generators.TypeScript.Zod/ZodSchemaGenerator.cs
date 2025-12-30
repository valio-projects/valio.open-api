using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.CSharp.Models;
using ValIO.Generators.TypeScript;
using ValIO.Generators.TypeScript.Models;
using ValIO.Generators.TypeScript.Zod.Models;

namespace ValIO.Generators.TypeScript.Zod;

/// <summary>
/// Генератор Zod схем для валидации TypeScript моделей
/// </summary>
public class ZodSchemaGenerator
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptTypeMapper _typeMapper;
    private readonly ZodTypeMapper _zodTypeMapper;
    private readonly ITemplateRegistry _templateRegistry;
    private readonly ILogger<ZodSchemaGenerator>? _logger;

    public ZodSchemaGenerator(
        INamingConverter namingConverter,
        TypeScriptTypeMapper typeMapper,
        ZodTypeMapper zodTypeMapper,
        ITemplateRegistry templateRegistry,
        ILogger<ZodSchemaGenerator>? logger = null)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _zodTypeMapper = zodTypeMapper ?? throw new ArgumentNullException(nameof(zodTypeMapper));
        _templateRegistry = templateRegistry ?? throw new ArgumentNullException(nameof(templateRegistry));
        _logger = logger;
    }

    /// <summary>
    /// Генерирует Zod схему для модели
    /// </summary>
    public string GenerateSchema(ApiModel model, ISchemaResolver schemaResolver)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var schemaName = $"{_namingConverter.ToPascalCase(model.Name)}Schema";
        var typeName = _namingConverter.ToPascalCase(model.Name);
        var properties = new List<ZodPropertySchemaTemplateData>();

        foreach (var property in model.Properties)
        {
            if (model.OriginalSchema?.Properties != null &&
                model.OriginalSchema.Properties.TryGetValue(property.Name, out var propertySchema))
            {
                var resolvedSchema = schemaResolver.ResolveSchema(propertySchema);
                var zodSchema = _zodTypeMapper.MapToZodSchema(resolvedSchema, property, schemaResolver);
                
                properties.Add(new ZodPropertySchemaTemplateData
                {
                    Name = _namingConverter.ToCamelCase(property.Name),
                    ZodSchema = zodSchema,
                    IsRequired = property.IsRequired,
                    Description = DecodeHtmlEntities(property.Description)
                });
            }
        }

        var templateData = new ZodSchemaTemplateData
        {
            SchemaName = schemaName,
            TypeName = typeName,
            Description = DecodeHtmlEntities(model.Description),
            Properties = properties
        };

        return _templateRegistry.Render("zod-schema.cshtml", templateData);
    }

    /// <summary>
    /// Генерирует Zod схему для enum
    /// </summary>
    public string GenerateEnumSchema(ApiEnum apiEnum)
    {
        if (apiEnum == null)
            throw new ArgumentNullException(nameof(apiEnum));

        var schemaName = $"{_namingConverter.ToPascalCase(apiEnum.Name)}Schema";
        var typeName = _namingConverter.ToPascalCase(apiEnum.Name);

        if (apiEnum.Values.Count == 0)
        {
            return $"import {{ z }} from 'zod';\n\nexport const {schemaName} = z.string();\n";
        }

        var enumValues = apiEnum.Values.Select(v =>
        {
            if (apiEnum.UnderlyingType == "string")
            {
                return $"\"{v.Value}\"";
            }
            return v.Value?.ToString() ?? "0";
        }).ToList();

        var templateData = new ZodEnumSchemaTemplateData
        {
            SchemaName = schemaName,
            TypeName = typeName,
            Description = DecodeHtmlEntities(apiEnum.Description),
            EnumValues = enumValues
        };

        return _templateRegistry.Render("zod-enum.cshtml", templateData);
    }

    private static string? DecodeHtmlEntities(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        return System.Net.WebUtility.HtmlDecode(text);
    }
}
