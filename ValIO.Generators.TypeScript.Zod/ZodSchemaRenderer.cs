using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.TypeScript.Zod;

/// <summary>
/// Рендерер для генерации Zod схем валидации через StringBuilders
/// </summary>
public class ZodSchemaRenderer : IModelFormatRenderer
{
    private readonly INamingConverter _namingConverter;
    private readonly ZodTypeMapper _zodTypeMapper;

    public ZodSchemaRenderer(
        INamingConverter namingConverter,
        ZodTypeMapper zodTypeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _zodTypeMapper = zodTypeMapper ?? throw new ArgumentNullException(nameof(zodTypeMapper));
    }

    public ModelFormat Format => ModelFormat.ValidationSchema;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    /// <summary>
    /// Рендерит Zod схему для модели
    /// </summary>
    public string Render(ModelTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        var schemaName = $"{data.Name}Schema";
        var typeName = data.Name;
        
        // Генерируем импорты
        sb.AppendLine("import { z } from 'zod';");
        
        // Генерируем импорты зависимостей схем
        var schemaDependencies = GetOption<List<string>>(data.Options, "SchemaDependencies", new List<string>());
        var enumDependencies = GetOption<List<string>>(data.Options, "EnumDependencies", new List<string>());
        
        if (schemaDependencies.Count > 0 || enumDependencies.Count > 0)
        {
            var allDependencies = schemaDependencies.Concat(enumDependencies)
                .Where(dep => !dep.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            
            foreach (var dep in allDependencies)
            {
                var depFileName = SchemaNameToFileName(dep);
                sb.AppendLine($"import {{ {dep} }} from './{depFileName}.schema';");
            }
            sb.AppendLine();
        }
        
        // Проверяем наличие циклических ссылок
        var hasCircularReference = GetOption<bool>(data.Options, "HasCircularReference", false);
        var typeDependencies = GetOption<List<string>>(data.Options, "TypeDependencies", new List<string>());
        
        if (hasCircularReference)
        {
            // Для рекурсивных схем генерируем TypeScript интерфейс
            if (typeDependencies.Count > 0)
            {
                sb.AppendLine("// Импортируем TypeScript типы из .model.ts файлов для описания интерфейса");
                foreach (var typeDep in typeDependencies.Distinct().OrderBy(x => x))
                {
                    var typeFileName = TypeNameToFileName(typeDep);
                    sb.AppendLine($"import type {{ {typeDep} }} from './{typeFileName}.model';");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("// TypeScript интерфейс для строгой типизации рекурсивной схемы");
            sb.AppendLine($"type {typeName} = {{");
            foreach (var property in data.Properties)
            {
                GeneratePropertyType(sb, property, indent: "  ");
            }
            sb.AppendLine("};");
            sb.AppendLine();
        }
        
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {EscapeJSDocComment(data.Description)}");
            sb.AppendLine(" */");
        }
        
        // Генерируем Zod схему
        if (hasCircularReference)
        {
            sb.AppendLine($"export const {schemaName}: z.ZodType<{typeName}> = z.object({{");
        }
        else
        {
            sb.AppendLine($"export const {schemaName} = z.object({{");
        }
        
        foreach (var property in data.Properties)
        {
            GeneratePropertySchema(sb, property, indent: "  ");
        }
        
        sb.AppendLine("});");
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Zod схему для enum
    /// </summary>
    public string RenderEnum(EnumTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        var schemaName = $"{data.Name}Schema";
        var typeName = data.Name;
        var underlyingType = data.UnderlyingType ?? "string";
        var isStringEnum = underlyingType == "string";
        var constArrayName = $"{typeName}Values";
        
        sb.AppendLine("import { z } from 'zod';");
        sb.AppendLine();
        
        // Генерируем массив значений enum
        if (isStringEnum)
        {
            sb.AppendLine($"export const {constArrayName} = [");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                sb.AppendLine($"  {valueString}{comma}");
            }
            sb.AppendLine("] as const satisfies readonly string[];");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"export const {constArrayName} = [");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                sb.AppendLine($"  {valueString}{comma}");
            }
            sb.AppendLine("] as const satisfies readonly number[];");
            sb.AppendLine();
        }
        
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {EscapeJSDocComment(data.Description)}");
            sb.AppendLine(" */");
        }
        
        // Генерируем Zod схему enum
        if (isStringEnum)
        {
            sb.AppendLine($"export const {schemaName} = z.enum({constArrayName});");
        }
        else
        {
            // Числовые enum'ы в Zod требуют использования z.union с z.literal
            sb.AppendLine($"export const {schemaName} = z.union([");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                sb.AppendLine($"  z.literal({valueString}){comma}");
            }
            sb.AppendLine("]);");
        }
        
        return sb.ToString();
    }

    #region Private Helper Methods

    private void GeneratePropertyType(StringBuilder sb, PropertyTemplateData property, string indent)
    {
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(property.Description))
        {
            sb.AppendLine($"{indent}/**");
            sb.AppendLine($"{indent} * {EscapeJSDocComment(property.Description)}");
            sb.AppendLine($"{indent} */");
        }
        
        // Генерируем тип свойства
        var optional = property.IsOptional ? "?" : "";
        var typeString = GetTypeScriptTypeFromTypeInfo(property.TypeInfo);
        sb.AppendLine($"{indent}{property.Name}{optional}: {typeString};");
    }

    private void GeneratePropertySchema(StringBuilder sb, PropertyTemplateData property, string indent)
    {
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(property.Description))
        {
            sb.AppendLine($"{indent}/**");
            sb.AppendLine($"{indent} * {EscapeJSDocComment(property.Description)}");
            sb.AppendLine($"{indent} */");
        }
        
        // Генерируем Zod схему для свойства
        // Получаем Zod схему из Options (должна быть подготовлена заранее)
        var zodSchema = GetOption<string>(property.Options, "ZodSchema", "z.any()");
        var optional = property.IsRequired ? "" : ".optional()";
        
        sb.AppendLine($"{indent}{property.Name}: {zodSchema}{optional},");
    }

    private string GetTypeScriptTypeFromTypeInfo(TypeInfo typeInfo)
    {
        // Используем FullTypeName из TypeInfo
        var typeScriptType = typeInfo.FullTypeName;
        
        // Убираем "?" из типа, если он там есть
        if (typeScriptType.EndsWith("?"))
        {
            typeScriptType = typeScriptType.Substring(0, typeScriptType.Length - 1).Trim();
        }
        
        return typeScriptType;
    }

    private string FormatEnumValue(string valueString, string underlyingType)
    {
        if (underlyingType == "string")
        {
            // Если значение уже в кавычках, оставляем как есть, иначе добавляем кавычки
            if (valueString.StartsWith("\"") && valueString.EndsWith("\""))
            {
                return valueString;
            }
            return $"\"{valueString}\"";
        }
        return valueString;
    }

    private string SchemaNameToFileName(string schemaName)
    {
        // Удаляем суффикс "Schema" только с конца строки
        var typeName = schemaName.EndsWith("Schema", StringComparison.OrdinalIgnoreCase) 
            ? schemaName.Substring(0, schemaName.Length - 6) 
            : schemaName;
            
        if (string.IsNullOrEmpty(typeName))
            return schemaName.ToLowerInvariant();
        
        return _namingConverter.ToCamelCase(typeName);
    }

    private string TypeNameToFileName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return typeName.ToLowerInvariant();
        
        return _namingConverter.ToCamelCase(typeName);
    }

    private string EscapeJSDocComment(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        return text
            .Replace("*/", "* /")
            .Replace("/*", "/ *");
    }

    private T GetOption<T>(Dictionary<string, object> options, string key, T defaultValue)
    {
        if (options == null || !options.TryGetValue(key, out var value))
            return defaultValue;
        
        if (value is T directValue)
            return directValue;
        
        if (value is IConvertible convertible)
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        
        return defaultValue;
    }


    #endregion
}
