using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Services;

/// <summary>
/// Базовый класс для извлечения enum из OpenAPI документа
/// </summary>
public abstract class BaseEnumExtractor : IEnumExtractor
{
    protected readonly ISchemaResolver SchemaResolver;
    protected readonly INamingConverter NamingConverter;
    protected readonly GenerationLanguage Language;

    protected BaseEnumExtractor(ISchemaResolver schemaResolver, INamingConverter namingConverter, GenerationLanguage language)
    {
        SchemaResolver = schemaResolver ?? throw new ArgumentNullException(nameof(schemaResolver));
        NamingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        Language = language;
    }

    /// <summary>
    /// Извлекает enum в виде TemplateData с уже подготовленными данными
    /// </summary>
    public virtual IReadOnlyList<EnumTemplateData> ExtractEnumTemplateData(OpenApiDocument document)
    {
        var enums = new List<EnumTemplateData>(32);

        if (document.Components?.Schemas == null)
            return enums;

        foreach (var schemaEntry in document.Components.Schemas)
        {
            var schemaName = schemaEntry.Key;
            var schema = schemaEntry.Value;

            var resolvedSchema = SchemaResolver.ResolveSchema(schema);
            
            // Проверяем, является ли схема enum'ом
            if (IsEnumSchema(resolvedSchema))
            {
                var enumTemplateData = ExtractEnumTemplateData(resolvedSchema, schemaName);
                if (enumTemplateData != null)
                {
                    enums.Add(enumTemplateData);
                }
            }
        }

        return enums;
    }

    /// <summary>
    /// Извлекает enum в виде TemplateData
    /// </summary>
    public virtual EnumTemplateData? ExtractEnumTemplateData(IOpenApiSchema schema, string enumName)
    {
        var resolvedSchema = SchemaResolver.ResolveSchema(schema);
        
        if (!IsEnumSchema(resolvedSchema))
            return null;

        var sanitizedName = NamingConverter.SanitizeIdentifier(enumName);
        var underlyingType = DetermineUnderlyingType(resolvedSchema);
        
        var enumValues = new List<EnumValueTemplateData>(8);
        
        if (resolvedSchema.Enum != null)
        {
            // Получаем имена enum из расширений
            var enumNames = GetEnumNames(resolvedSchema);
            var enumDescriptions = GetEnumDescriptions(resolvedSchema);

            for (int i = 0; i < resolvedSchema.Enum.Count; i++)
            {
                var enumValue = resolvedSchema.Enum[i];
                var valueName = enumNames != null && i < enumNames.Count 
                    ? enumNames[i] 
                    : GenerateEnumValueName(enumValue, underlyingType);
                
                var valueDescription = enumDescriptions != null && i < enumDescriptions.Count
                    ? enumDescriptions[i]
                    : null;

                // Форматируем значение для генерации кода
                var valueString = FormatEnumValueString(enumValue, underlyingType);

                enumValues.Add(new EnumValueTemplateData
                {
                    Name = NamingConverter.SanitizeIdentifier(valueName),
                    Value = enumValue,
                    Description = valueDescription,
                    ValueString = valueString
                });
            }
        }

        // Определяем формат enum (язык-специфичная реализация)
        var format = DetermineEnumFormat(underlyingType);

        return new EnumTemplateData
        {
            Name = sanitizedName,
            Description = resolvedSchema.Description,
            Values = enumValues,
            UnderlyingType = underlyingType,
            Language = Language,
            Format = format
        };
    }

    /// <summary>
    /// Определяет формат enum в зависимости от языка (язык-специфичная реализация)
    /// </summary>
    protected abstract EnumFormat DetermineEnumFormat(string underlyingType);

    /// <summary>
    /// Проверяет, является ли схема enum'ом
    /// </summary>
    public static bool IsEnumSchema(IOpenApiSchema schema)
    {
        return schema.Enum != null && schema.Enum.Count > 0;
    }

    /// <summary>
    /// Определяет базовый тип enum
    /// </summary>
    protected static string DetermineUnderlyingType(IOpenApiSchema schema)
    {
        if (schema.Type.HasValue)
        {
            var type = schema.Type.Value & ~JsonSchemaType.Null;
            return type switch
            {
                JsonSchemaType.Integer => "int",
                JsonSchemaType.Number => "double",
                JsonSchemaType.String => "string",
                JsonSchemaType.Boolean => "bool",
                _ => "int"
            };
        }

        // Если тип не указан, но есть enum значения, определяем по первому значению
        if (schema.Enum != null && schema.Enum.Count > 0)
        {
            var firstValue = schema.Enum[0];
            
            // Обрабатываем JsonNode
            if (firstValue is JsonNode jsonNode)
            {
                return jsonNode.GetValueKind() switch
                {
                    JsonValueKind.Number => "int",
                    JsonValueKind.String => "string",
                    JsonValueKind.True or JsonValueKind.False => "bool",
                    _ => "int"
                };
            }
            
            return "int";
        }

        return "int";
    }

    /// <summary>
    /// Генерирует имя для значения enum
    /// </summary>
    protected static string GenerateEnumValueName(object? value, string underlyingType)
    {
        if (value == null)
            return "None";

        if (value is string stringValue)
        {
            // Преобразуем строку в валидное имя enum (убираем спецсимволы, делаем PascalCase)
            var sanitized = stringValue
                .Replace("-", "_")
                .Replace(" ", "_")
                .Replace(".", "_");
            
            // Если начинается с цифры, добавляем префикс
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            return sanitized;
        }

        // Для числовых значений используем значение как имя
        return value.ToString() ?? "None";
    }

    /// <summary>
    /// Получает имена enum из расширений схемы
    /// </summary>
    protected static IReadOnlyList<string>? GetEnumNames(IOpenApiSchema schema)
    {
        if (schema.Extensions == null)
            return null;

        // Проверяем различные варианты расширений для имен enum
        var extensionsToCheck = new[] { "x-enumNames", "x-enumnames", "x-enum-varnames" };

        foreach (var extensionKey in extensionsToCheck)
        {
            if (schema.Extensions.TryGetValue(extensionKey, out var extension))
            {
                if (extension is JsonNodeExtension { Node: { } jsonNode })
                {
                    if (jsonNode is JsonArray jsonArray)
                    {
                        var names = new List<string>(8);
                        foreach (var item in jsonArray)
                        {
                            if (item != null && item.GetValueKind() == JsonValueKind.String)
                            {
                                names.Add(item.GetValue<string>() ?? string.Empty);
                            }
                        }
                        return names;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Получает описания enum из расширений схемы
    /// </summary>
    protected static IReadOnlyList<string>? GetEnumDescriptions(IOpenApiSchema schema)
    {
        if (schema.Extensions == null)
            return null;

        // Проверяем расширение для описаний enum
        if (schema.Extensions.TryGetValue("x-enum-descriptions", out var extension))
        {
            if (extension is JsonNodeExtension { Node: { } jsonNode })
            {
                if (jsonNode is JsonArray jsonArray)
                {
                    var descriptions = new List<string>(8);
                    foreach (var item in jsonArray)
                    {
                        if (item != null && item.GetValueKind() == JsonValueKind.String)
                        {
                            descriptions.Add(item.GetValue<string>() ?? string.Empty);
                        }
                    }
                    return descriptions;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Форматирует значение enum для генерации кода
    /// </summary>
    protected static string FormatEnumValueString(object? value, string underlyingType)
    {
        if (value == null)
            return "null";

        if (underlyingType == "string")
        {
            var stringValue = value.ToString() ?? "";
            // Экранируем кавычки и специальные символы
            var escaped = stringValue
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
            return $"\"{escaped}\"";
        }

        // Для числовых значений просто возвращаем строковое представление
        return value.ToString() ?? "0";
    }
}
