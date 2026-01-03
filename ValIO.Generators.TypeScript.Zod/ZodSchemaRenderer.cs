using System.Text;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Zod.Models;

namespace ValIO.Generators.TypeScript.Zod;

/// <summary>
/// Рендерер для генерации Zod схем валидации через StringBuilders
/// </summary>
public class ZodSchemaRenderer : IModelFormatRenderer<ZodModelTemplateDataOptions, ZodPropertyTemplateDataOptions>
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
    /// Рендерит Zod схему для модели (базовый метод для интерфейса)
    /// </summary>
    public string Render(IModelTemplateData data)
    {
        throw new InvalidOperationException("Use Render(IModelTemplateData, OpenApiDocument, ISchemaResolver) method for Zod schema generation");
    }

    /// <summary>
    /// Рендерит Zod схему для модели с дополнительными параметрами для создания правильной модели
    /// </summary>
    public string Render(IModelTemplateData data, Microsoft.OpenApi.OpenApiDocument document, ISchemaResolver schemaResolver)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        if (schemaResolver == null)
            throw new ArgumentNullException(nameof(schemaResolver));

        // Создаем модель с Zod опциями, используя ZodTypeMapper для маппинга свойств
        var zodModelData = CreateZodModelTemplateData(data, document, schemaResolver);
        return Render(zodModelData);
    }

    /// <summary>
    /// Создает ModelTemplateData с Zod опциями из базовой модели
    /// </summary>
    private ModelTemplateData<ZodModelTemplateDataOptions, ZodPropertyTemplateDataOptions> CreateZodModelTemplateData(
        IModelTemplateData data, 
        Microsoft.OpenApi.OpenApiDocument document, 
        ISchemaResolver schemaResolver)
    {
        // Получаем OpenAPI схему для модели из document
        if (document.Components?.Schemas == null || !document.Components.Schemas.TryGetValue(data.Name, out var schema))
        {
            throw new InvalidOperationException($"Schema '{data.Name}' not found in OpenAPI document");
        }

        var modelSchema = schemaResolver.ResolveSchema(schema);

        // Создаем свойства с Zod опциями и собираем зависимости
        var zodProperties = new List<PropertyTemplateData<ZodPropertyTemplateDataOptions>>();
        var allSchemaDependencies = new HashSet<string>();
        var allEnumDependencies = new HashSet<string>();
        var processedSchemas = new HashSet<string>();

        if (modelSchema.Properties != null)
        {
            foreach (var property in data.Properties)
            {
                // Получаем схему свойства из OpenAPI схемы
                if (!modelSchema.Properties.TryGetValue(property.Name, out var propSchema))
                {
                    // Если свойство не найдено в схеме, используем z.any()
                    zodProperties.Add(CreateZodProperty(property, "z.any()"));
                    continue;
                }

                var propertySchema = schemaResolver.ResolveSchema(propSchema);

                // Маппим свойство в Zod схему с зависимостями
                var zodMappingResult = _zodTypeMapper.MapToZodSchemaWithDependencies(
                    propertySchema,
                    property.Name,
                    schemaResolver,
                    processedSchemas,
                    data.Name);

                // Собираем зависимости
                foreach (var dep in zodMappingResult.SchemaDependencies)
                {
                    allSchemaDependencies.Add(dep);
                }
                foreach (var dep in zodMappingResult.EnumDependencies)
                {
                    allEnumDependencies.Add(dep);
                }

                zodProperties.Add(CreateZodProperty(property, zodMappingResult.ZodSchema));
            }
        }
        else
        {
            // Если схема не найдена, создаем свойства без Zod схем (будут z.any())
            foreach (var property in data.Properties)
            {
                zodProperties.Add(CreateZodProperty(property, "z.any()"));
            }
        }

        // Создаем Zod опции для модели с зависимостями
        var zodModelOptions = new ZodModelTemplateDataOptions
        {
            SchemaDependencies = allSchemaDependencies.Where(s => s != data.Name).ToList(),
            EnumDependencies = allEnumDependencies.ToList(),
            HasCircularReference = processedSchemas.Contains(data.Name)
        };

        // Создаем базовые свойства с Zod опциями
        var zodBaseClassProperties = data.BaseClassProperties.Select(p => CreateZodProperty(p, "z.any()")).ToList();

        // Создаем собственные свойства с Zod опциями
        var zodOwnProperties = data.OwnProperties.Select(p => CreateZodProperty(p, "z.any()")).ToList();

        return new ModelTemplateData<ZodModelTemplateDataOptions, ZodPropertyTemplateDataOptions>
        {
            Name = data.Name,
            Description = data.Description,
            Language = data.Language,
            Format = data.Format,
            Dependencies = data.Dependencies,
            BaseClassName = data.BaseClassName,
            Namespace = data.Namespace,
            ImportPath = data.ImportPath,
            Options = zodModelOptions,
            Properties = zodProperties,
            BaseClassProperties = zodBaseClassProperties,
            OwnProperties = zodOwnProperties
        };
    }

    /// <summary>
    /// Создает PropertyTemplateData с Zod опциями из базового свойства
    /// </summary>
    private PropertyTemplateData<ZodPropertyTemplateDataOptions> CreateZodProperty(IPropertyTemplateData property, string zodSchema)
    {
        return new PropertyTemplateData<ZodPropertyTemplateDataOptions>
        {
            Name = property.Name,
            TypeInfo = property.TypeInfo,
            Description = property.Description,
            IsRequired = property.IsRequired,
            IsNullable = property.IsNullable,
            IsOptional = property.IsOptional,
            DefaultValue = property.DefaultValue,
            Min = property.Min,
            Max = property.Max,
            MinLength = property.MinLength,
            MaxLength = property.MaxLength,
            ExclusiveMin = property.ExclusiveMin,
            ExclusiveMax = property.ExclusiveMax,
            Pattern = property.Pattern,
            Options = new ZodPropertyTemplateDataOptions
            {
                ZodSchema = zodSchema
            }
        };
    }

    /// <summary>
    /// Рендерит Zod схему для модели со строгой типизацией
    /// </summary>
    public string Render(ModelTemplateData<ZodModelTemplateDataOptions, ZodPropertyTemplateDataOptions> data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder(4096);
        
        var schemaName = $"{data.Name}Schema";
        var typeName = data.Name;
        
        // Генерируем импорты
        sb.AppendLine("import { z } from 'zod';");
        
        // Получаем типизированные Options (теперь без приведения)
        var zodOptions = data.Options;
        
        // Генерируем импорты зависимостей схем
        var schemaDependencies = zodOptions.SchemaDependencies ?? Array.Empty<string>();
        var enumDependencies = zodOptions.EnumDependencies ?? Array.Empty<string>();
        
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
        var hasCircularReference = zodOptions.HasCircularReference;
        var typeDependencies = zodOptions.TypeDependencies ?? Array.Empty<string>();
        
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
            CodeFormatter.AppendLine(sb, 0, $"type {typeName} = {{");
            foreach (var property in data.Properties.Cast<PropertyTemplateData<ZodPropertyTemplateDataOptions>>())
            {
                GeneratePropertyType(sb, property, indentLevel: 1);
            }
            CodeFormatter.AppendLine(sb, 0, "};");
            CodeFormatter.AppendEmptyLine(sb);
        }
        
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
        
        // Генерируем Zod схему
        if (hasCircularReference)
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {schemaName}: z.ZodType<{typeName}> = z.object({{");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {schemaName} = z.object({{");
        }
        
        foreach (var property in data.Properties.Cast<PropertyTemplateData<ZodPropertyTemplateDataOptions>>())
        {
            GeneratePropertySchema(sb, property, indentLevel: 1);
        }
        
        CodeFormatter.AppendLine(sb, 0, "});");
        
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
            CodeFormatter.AppendLine(sb, 0, $"export const {constArrayName} = [");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                CodeFormatter.AppendLine(sb, 1, $"{valueString}{comma}");
            }
            CodeFormatter.AppendLine(sb, 0, "] as const satisfies readonly string[];");
            CodeFormatter.AppendEmptyLine(sb);
        }
        else
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {constArrayName} = [");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                CodeFormatter.AppendLine(sb, 1, $"{valueString}{comma}");
            }
            CodeFormatter.AppendLine(sb, 0, "] as const satisfies readonly number[];");
            CodeFormatter.AppendEmptyLine(sb);
        }
        
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
        
        // Генерируем Zod схему enum
        if (isStringEnum)
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {schemaName} = z.enum({constArrayName});");
        }
        else
        {
            // Числовые enum'ы в Zod требуют использования z.union с z.literal
            CodeFormatter.AppendLine(sb, 0, $"export const {schemaName} = z.union([");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                var comma = i < data.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.ValueString, underlyingType);
                CodeFormatter.AppendLine(sb, 1, $"z.literal({valueString}){comma}");
            }
            CodeFormatter.AppendLine(sb, 0, "]);");
        }
        
        return sb.ToString();
    }

    #region Private Helper Methods

    private void GeneratePropertyType(StringBuilder sb, PropertyTemplateData<ZodPropertyTemplateDataOptions> property, int indentLevel)
    {
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, indentLevel, property.Description);
        
        // Генерируем тип свойства
        var optional = property.IsOptional ? "?" : "";
        var typeString = property.TypeInfo.FullTypeName;
        CodeFormatter.AppendLine(sb, indentLevel, $"{property.Name}{optional}: {typeString};");
    }

    private void GeneratePropertySchema(StringBuilder sb, PropertyTemplateData<ZodPropertyTemplateDataOptions> property, int indentLevel)
    {
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, indentLevel, property.Description);
        
        // Генерируем Zod схему для свойства (теперь без приведения)
        var zodSchema = property.Options.ZodSchema ?? "z.any()";
        var optional = property.IsRequired ? "" : ".optional()";
        
        CodeFormatter.AppendLine(sb, indentLevel, $"{property.Name}: {zodSchema}{optional},");
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




    #endregion
}
