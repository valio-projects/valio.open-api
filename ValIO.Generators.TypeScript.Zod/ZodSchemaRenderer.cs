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

        // Пытаемся найти схему в документе
        // ВАЖНО: получаем оригинальную схему из документа, чтобы сохранить ссылки на компоненты
        var originalSchema = FindSchemaInDocument(data.Name, document);
        if (originalSchema == null)
        {
            // Если схема не найдена, создаем модель с базовыми Zod опциями (z.any() для всех свойств)
            return CreateFallbackZodModel(data);
        }

        // Создаем модель с Zod опциями, используя ZodTypeMapper для маппинга свойств
        // Передаем оригинальную схему, чтобы сохранить информацию о ссылках
        var zodModelData = CreateZodModelTemplateData(data, originalSchema, document, schemaResolver);
        return Render(zodModelData);
    }

    /// <summary>
    /// Находит схему в документе по имени модели (с учетом sanitized имен)
    /// </summary>
    private IOpenApiSchema? FindSchemaInDocument(string modelName, Microsoft.OpenApi.OpenApiDocument document)
    {
        if (document.Components?.Schemas == null)
            return null;

        // Сначала пытаемся найти по точному имени
        if (document.Components.Schemas.TryGetValue(modelName, out var exactSchema))
        {
            return exactSchema;
        }

        // Если не найдено, ищем по sanitized имени среди всех схем
        foreach (var schemaEntry in document.Components.Schemas)
        {
            var originalName = schemaEntry.Key;
            var sanitizedName = _namingConverter.SanitizeIdentifier(originalName);
            if (sanitizedName.Equals(modelName, StringComparison.OrdinalIgnoreCase))
            {
                return schemaEntry.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Создает fallback Zod модель с z.any() для всех свойств (когда схема не найдена)
    /// </summary>
    private string CreateFallbackZodModel(IModelTemplateData data)
    {
        var sb = new StringBuilder(4096);
        
        var schemaName = $"{data.Name}Schema";
        
        sb.AppendLine("import { z } from 'zod';");
        sb.AppendLine();
        
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
        
        // Генерируем Zod схему с z.any() для всех свойств
        CodeFormatter.AppendLine(sb, 0, $"export const {schemaName} = z.object({{");
        
        foreach (var property in data.Properties)
        {
            CommentGenerator.GenerateJSDocComment(sb, 1, property.Description);
            var optional = property.IsRequired ? "" : ".optional()";
            CodeFormatter.AppendLine(sb, 1, $"{property.Name}: z.any(){optional},");
        }
        
        CodeFormatter.AppendLine(sb, 0, "});");
        
        return sb.ToString();
    }

    /// <summary>
    /// Создает ModelTemplateData с Zod опциями из базовой модели
    /// </summary>
    private ModelTemplateData<ZodModelTemplateDataOptions, ZodPropertyTemplateDataOptions> CreateZodModelTemplateData(
        IModelTemplateData data, 
        IOpenApiSchema schema,
        Microsoft.OpenApi.OpenApiDocument document, 
        ISchemaResolver schemaResolver)
    {
        // ВАЖНО: schema - это уже оригинальная схема из документа (до разрешения)
        // Разрешаем её только для проверки наличия Properties
        var resolvedSchema = schemaResolver.ResolveSchema(schema);

        // Создаем свойства с Zod опциями и собираем зависимости
        var zodProperties = new List<PropertyTemplateData<ZodPropertyTemplateDataOptions>>();
        var allSchemaDependencies = new HashSet<string>();
        var allEnumDependencies = new HashSet<string>();
        var processedSchemas = new HashSet<string>();

        // Получаем оригинальные свойства из оригинальной схемы schema
        // Если schema - это ссылка, то schema.Properties будет null, и нужно использовать resolvedSchema
        // Но если schema - это объект, то schema.Properties содержит оригинальные схемы со ссылками
        IDictionary<string, IOpenApiSchema>? originalProperties = null;
        if (schema is OpenApiSchema openApiSchema && openApiSchema.Properties != null)
        {
            // Это оригинальная схема-объект, используем её Properties
            originalProperties = openApiSchema.Properties;
        }
        else if (resolvedSchema.Properties != null)
        {
            // Это ссылка или уже разрешенная схема, используем разрешенную
            // Но это означает, что свойства уже разрешены и ссылки потеряны
            originalProperties = resolvedSchema.Properties;
        }

        if (originalProperties != null)
        {
            foreach (var property in data.Properties)
            {
                // Получаем оригинальную схему свойства из оригинальной схемы
                // Это важно, чтобы сохранить информацию о ссылке на компонент
                if (!originalProperties.TryGetValue(property.Name, out var originalPropSchema))
                {
                    // Если свойство не найдено в схеме, используем z.any()
                    zodProperties.Add(CreateZodProperty(property, "z.any()"));
                    continue;
                }

                // Маппим свойство в Zod схему с зависимостями
                // Передаем оригинальную схему originalPropSchema, чтобы ZodTypeMapper мог определить ссылку
                var zodMappingResult = _zodTypeMapper.MapToZodSchemaWithDependencies(
                    originalPropSchema,  // Используем оригинальную схему свойства
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
