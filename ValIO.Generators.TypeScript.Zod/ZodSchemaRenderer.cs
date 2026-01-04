using System.Text;
using System.Text.RegularExpressions;
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
        // Используем сохраненную оригинальную схему из ModelTemplateData, если доступна
        var originalSchema = data is ModelTemplateData modelData && modelData.OriginalSchema != null
            ? modelData.OriginalSchema
            : schema;
        
        var resolvedSchema = schemaResolver.ResolveSchema(originalSchema);

        // Создаем свойства с Zod опциями и собираем зависимости
        var zodProperties = new List<PropertyTemplateData<ZodPropertyTemplateDataOptions>>();
        var allSchemaDependencies = new HashSet<string>();
        var allEnumDependencies = new HashSet<string>();
        var processedSchemas = new HashSet<string>();

        // Используем сохраненную информацию о композиции, если доступна
        var compositionInfo = data is ModelTemplateData modelData2 && modelData2.CompositionInfo != null
            ? modelData2.CompositionInfo
            : SchemaCompositionHelper.CreateCompositionInfo(originalSchema, resolvedSchema, schemaResolver, _namingConverter);

        // Обрабатываем allOf - объединяем свойства из всех схем
        if (compositionInfo != null && compositionInfo.CompositionType == SchemaCompositionType.AllOf)
        {
            // Используем сохраненную информацию о композиции для получения всех свойств
            var allProperties = SchemaCompositionHelper.GetAllOfProperties(compositionInfo, originalSchema, resolvedSchema);
            var allRequired = SchemaCompositionHelper.MergeRequiredProperties(compositionInfo, resolvedSchema, schemaResolver);

            // Обрабатываем все свойства (из allOf и из самой схемы)
            foreach (var property in data.Properties)
            {
                // Используем сохраненную оригинальную схему свойства, если доступна
                IOpenApiSchema? originalPropSchema = null;
                if (property is PropertyTemplateData propData && propData.OriginalSchema != null)
                {
                    originalPropSchema = propData.OriginalSchema;
                }
                else if (allProperties.TryGetValue(property.Name, out var propSchema))
                {
                    originalPropSchema = propSchema;
                }

                if (originalPropSchema == null)
                {
                    zodProperties.Add(CreateZodProperty(property, "z.any()"));
                    continue;
                }

                var zodMappingResult = _zodTypeMapper.MapToZodSchemaWithDependencies(
                    originalPropSchema,
                    property.Name,
                    schemaResolver,
                    processedSchemas,
                    data.Name);

                foreach (var dep in zodMappingResult.SchemaDependencies)
                {
                    allSchemaDependencies.Add(dep);
                }
                foreach (var dep in zodMappingResult.EnumDependencies)
                {
                    allEnumDependencies.Add(dep);
                }

                var zodSchemaString = RenderExpression(zodMappingResult.Expression);
                zodProperties.Add(CreateZodProperty(property, zodSchemaString));
            }
        }
        else
        {
            // Обычная схема без allOf
            // Получаем оригинальные свойства из оригинальной схемы или используем сохраненные OriginalSchema
            var originalProperties = SchemaCompositionHelper.GetOriginalProperties(originalSchema, resolvedSchema);

            if (originalProperties != null)
            {
                foreach (var property in data.Properties)
                {
                    // Используем сохраненную оригинальную схему свойства, если доступна
                    IOpenApiSchema? originalPropSchema = null;
                    if (property is PropertyTemplateData propData && propData.OriginalSchema != null)
                    {
                        originalPropSchema = propData.OriginalSchema;
                    }
                    else if (originalProperties.TryGetValue(property.Name, out var propSchema))
                    {
                        originalPropSchema = propSchema;
                    }

                    if (originalPropSchema == null)
                    {
                        zodProperties.Add(CreateZodProperty(property, "z.any()"));
                        continue;
                    }

                    // Маппим свойство в Zod схему с зависимостями
                    var zodMappingResult = _zodTypeMapper.MapToZodSchemaWithDependencies(
                        originalPropSchema,
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

                    var zodSchemaString = RenderExpression(zodMappingResult.Expression);
                    zodProperties.Add(CreateZodProperty(property, zodSchemaString));
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
        }

        // Проверяем наличие рекурсивных ссылок (циклических зависимостей)
        // Рекурсивная ссылка - это когда свойство ссылается на текущую схему
        var hasCircularReference = CheckForCircularReference(data, schemaResolver, data.Name);
        
        // Собираем TypeScript типы для рекурсивных схем
        var typeDependencies = new HashSet<string>();
        if (hasCircularReference)
        {
            // Для рекурсивных схем собираем типы из зависимостей свойств
            foreach (var property in data.Properties)
            {
                CollectTypeDependencies(property.TypeInfo, typeDependencies, data.Name);
            }
        }

        // Создаем Zod опции для модели с зависимостями
        var zodModelOptions = new ZodModelTemplateDataOptions
        {
            SchemaDependencies = allSchemaDependencies.Where(s => !s.EndsWith($"{data.Name}Schema", StringComparison.OrdinalIgnoreCase)).ToList(),
            EnumDependencies = allEnumDependencies.ToList(),
            HasCircularReference = hasCircularReference,
            TypeDependencies = typeDependencies.ToList()
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
        
        // Генерируем константы валидации ПЕРЕД схемой
        var constantsName = GenerateValidationConstants(sb, data);
        
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
            GeneratePropertySchema(sb, property, indentLevel: 1, constantsName);
        }
        
        CodeFormatter.AppendLine(sb, 0, "});");
        
        return sb.ToString();
    }

    /// <summary>
    /// Генерирует константы валидации для свойств схемы
    /// Возвращает имя констант или null если константы не были сгенерированы
    /// </summary>
    private string? GenerateValidationConstants(StringBuilder sb, ModelTemplateData<ZodModelTemplateDataOptions, ZodPropertyTemplateDataOptions> data)
    {
        var constantsName = $"{data.Name}SchemaConstants";
        var hasAnyConstants = false;
        
        // Собираем константы для каждого свойства
        var propertiesConstants = new Dictionary<string, Dictionary<string, string>>();
        
        foreach (var property in data.Properties.Cast<PropertyTemplateData<ZodPropertyTemplateDataOptions>>())
        {
            var propertyConstants = new Dictionary<string, string>();
            
            // Добавляем только те константы, которые имеют значения
            if (property.Min.HasValue)
            {
                propertyConstants["min"] = FormatNumberValue(property.Min.Value);
            }
            
            if (property.Max.HasValue)
            {
                propertyConstants["max"] = FormatNumberValue(property.Max.Value);
            }
            
            if (property.MinLength.HasValue)
            {
                propertyConstants["minLength"] = property.MinLength.Value.ToString();
            }
            
            if (property.MaxLength.HasValue)
            {
                propertyConstants["maxLength"] = property.MaxLength.Value.ToString();
            }
            
            if (!string.IsNullOrEmpty(property.Pattern))
            {
                propertyConstants["pattern"] = $"\"{property.Pattern.Replace("\"", "\\\"")}\"";
            }
            
            if (property.DefaultValue != null)
            {
                propertyConstants["default"] = FormatDefaultValue(property.DefaultValue);
            }
            
            if (property.ExclusiveMin.HasValue)
            {
                propertyConstants["exclusiveMin"] = FormatNumberValue(property.ExclusiveMin.Value);
            }
            
            if (property.ExclusiveMax.HasValue)
            {
                propertyConstants["exclusiveMax"] = FormatNumberValue(property.ExclusiveMax.Value);
            }
            
            if (propertyConstants.Count > 0)
            {
                propertiesConstants[property.Name] = propertyConstants;
                hasAnyConstants = true;
            }
        }
        
        // Генерируем константы только если есть хотя бы одно свойство с константами
        if (!hasAnyConstants)
            return null;
        
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 0, $"export const {constantsName} = {{");
        
        var propertiesList = propertiesConstants.ToList();
        for (int i = 0; i < propertiesList.Count; i++)
        {
            var prop = propertiesList[i];
            var camelCaseName = _namingConverter.ToCamelCase(prop.Key);
            var isLast = i == propertiesList.Count - 1;
            
            CodeFormatter.AppendLine(sb, 1, $"{camelCaseName}: {{");
            
            var constantsList = prop.Value.ToList();
            for (int j = 0; j < constantsList.Count; j++)
            {
                var constant = constantsList[j];
                var isLastConstant = j == constantsList.Count - 1;
                var comma = isLastConstant ? "" : ",";
                CodeFormatter.AppendLine(sb, 2, $"{constant.Key}: {constant.Value}{comma}");
            }
            
            var closingComma = isLast ? "" : ",";
            CodeFormatter.AppendLine(sb, 1, $"}}{closingComma}");
        }
        
        CodeFormatter.AppendLine(sb, 0, "} as const;");
        
        return constantsName;
    }
    
    /// <summary>
    /// Форматирует числовое значение для константы
    /// </summary>
    private string FormatNumberValue(decimal value)
    {
        // Если значение целое, выводим без десятичной точки
        if (value == Math.Truncate(value))
        {
            return ((long)value).ToString();
        }
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    
    /// <summary>
    /// Форматирует значение по умолчанию для константы
    /// </summary>
    private string FormatDefaultValue(object value)
    {
        return value switch
        {
            string str => $"\"{str.Replace("\"", "\\\"")}\"",
            bool b => b ? "true" : "false",
            null => "null",
            _ => value.ToString() ?? "null"
        };
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

    private void GeneratePropertySchema(StringBuilder sb, PropertyTemplateData<ZodPropertyTemplateDataOptions> property, int indentLevel, string? constantsName = null)
    {
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, indentLevel, property.Description);
        
        // Генерируем Zod схему для свойства
        var zodSchema = property.Options.ZodSchema ?? "z.any()";
        
        // Если есть константы, пытаемся заменить жестко закодированные значения на ссылки на константы
        if (!string.IsNullOrEmpty(constantsName))
        {
            zodSchema = ReplaceValidationConstants(zodSchema, property, constantsName);
        }
        
        var optional = property.IsRequired ? "" : ".optional()";
        var camelCaseName = _namingConverter.ToCamelCase(property.Name);
        
        CodeFormatter.AppendLine(sb, indentLevel, $"{camelCaseName}: {zodSchema}{optional},");
    }
    
    /// <summary>
    /// Заменяет жестко закодированные значения валидации на ссылки на константы
    /// </summary>
    private string ReplaceValidationConstants(string zodSchema, PropertyTemplateData<ZodPropertyTemplateDataOptions> property, string constantsName)
    {
        var camelCasePropertyName = _namingConverter.ToCamelCase(property.Name);
        var result = zodSchema;
        
        // Заменяем min/max/minLength/maxLength/pattern/default если они есть
        if (property.Min.HasValue)
        {
            var value = FormatNumberValue(property.Min.Value);
            result = Regex.Replace(
                result, 
                $@"\.min\({Regex.Escape(value)}\)",
                $".min({constantsName}.{camelCasePropertyName}.min)");
        }
        
        if (property.Max.HasValue)
        {
            var value = FormatNumberValue(property.Max.Value);
            result = Regex.Replace(
                result, 
                $@"\.max\({Regex.Escape(value)}\)",
                $".max({constantsName}.{camelCasePropertyName}.max)");
        }
        
        if (property.MinLength.HasValue)
        {
            var value = property.MinLength.Value.ToString();
            result = Regex.Replace(
                result, 
                $@"\.min\({Regex.Escape(value)}\)",
                $".min({constantsName}.{camelCasePropertyName}.minLength)");
        }
        
        if (property.MaxLength.HasValue)
        {
            var value = property.MaxLength.Value.ToString();
            result = Regex.Replace(
                result, 
                $@"\.max\({Regex.Escape(value)}\)",
                $".max({constantsName}.{camelCasePropertyName}.maxLength)");
        }
        
        if (!string.IsNullOrEmpty(property.Pattern))
        {
            var escapedPattern = Regex.Escape(property.Pattern);
            result = Regex.Replace(
                result, 
                $@"\.regex\(/{escapedPattern}/\)",
                $".regex(/{constantsName}.{camelCasePropertyName}.pattern/)");
        }
        
        return result;
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

    /// <summary>
    /// Проверяет наличие циклической ссылки (рекурсивной схемы)
    /// </summary>
    private bool CheckForCircularReference(IModelTemplateData data, ISchemaResolver schemaResolver, string currentSchemaName)
    {
        if (string.IsNullOrEmpty(currentSchemaName))
            return false;

        // Проверяем свойства на наличие ссылок на текущую схему
        foreach (var property in data.Properties)
        {
            if (HasReferenceToSchema(property.TypeInfo, currentSchemaName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Проверяет, содержит ли TypeInfo ссылку на указанную схему
    /// </summary>
    private bool HasReferenceToSchema(TypeInfo typeInfo, string schemaName)
    {
        if (typeInfo == null)
            return false;

        // Проверяем базовый тип
        if (typeInfo.BaseType.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Проверяем generic аргументы
        if (typeInfo.GenericArguments != null && typeInfo.GenericArguments.Count > 0)
        {
            foreach (var genericArg in typeInfo.GenericArguments)
            {
                if (HasReferenceToSchema(genericArg, schemaName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Собирает TypeScript типы из зависимостей для рекурсивных схем
    /// </summary>
    private void CollectTypeDependencies(TypeInfo typeInfo, HashSet<string> typeDependencies, string currentSchemaName)
    {
        if (typeInfo == null)
            return;

        // Если это не примитивный тип и не текущая схема, добавляем в зависимости
        if (!typeInfo.IsPrimitive && 
            !string.IsNullOrEmpty(typeInfo.BaseType) &&
            !typeInfo.BaseType.Equals(currentSchemaName, StringComparison.OrdinalIgnoreCase) &&
            !IsBuiltInType(typeInfo.BaseType))
        {
            typeDependencies.Add(typeInfo.BaseType);
        }

        // Рекурсивно обрабатываем generic аргументы
        if (typeInfo.GenericArguments != null && typeInfo.GenericArguments.Count > 0)
        {
            foreach (var genericArg in typeInfo.GenericArguments)
            {
                CollectTypeDependencies(genericArg, typeDependencies, currentSchemaName);
            }
        }
    }

    /// <summary>
    /// Проверяет, является ли тип встроенным
    /// </summary>
    private bool IsBuiltInType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return false;

        var builtInTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "any", "unknown", "void", "null", "undefined",
            "string", "number", "boolean", "object", "Array",
            "Promise", "Record", "int", "long", "float", "double",
            "decimal", "bool", "DateTime", "DateTimeOffset", "Guid",
            "byte[]", "Stream", "List", "Dictionary"
        };

        return builtInTypes.Contains(typeName);
    }

    /// <summary>
    /// Рендерит ZodSchemaExpression в строку TypeScript кода
    /// </summary>
    private string RenderExpression(ZodSchemaExpression expression)
    {
        return expression switch
        {
            ZodReferenceExpression refExpr => refExpr.SchemaName,
            
            ZodLazyExpression lazyExpr => $"z.lazy(() => {lazyExpr.SchemaName})",
            
            ZodPrimitiveExpression primExpr => RenderPrimitiveExpression(primExpr),
            
            ZodObjectExpression objExpr => RenderObjectExpression(objExpr),
            
            ZodArrayExpression arrExpr => $"z.array({RenderExpression(arrExpr.ItemExpression)})",
            
            ZodUnionExpression unionExpr => RenderUnionExpression(unionExpr),
            
            ZodIntersectionExpression interExpr => RenderIntersectionExpression(interExpr),
            
            ZodRecordExpression recordExpr => $"z.record(z.string(), {RenderExpression(recordExpr.ValueExpression)})",
            
            ZodEnumExpression enumExpr => RenderEnumExpression(enumExpr),
            
            _ => "z.any()"
        };
    }

    /// <summary>
    /// Рендерит примитивное выражение
    /// </summary>
    private string RenderPrimitiveExpression(ZodPrimitiveExpression expr)
    {
        var baseType = expr.Type switch
        {
            ZodPrimitiveType.Any => "z.any()",
            ZodPrimitiveType.Boolean => "z.boolean()",
            ZodPrimitiveType.Number => "z.number()",
            ZodPrimitiveType.String => "z.string()",
            ZodPrimitiveType.InstanceOf => "z.instanceof(Blob)",
            _ => "z.any()"
        };

        // Применяем формат для строк
        if (expr.Type == ZodPrimitiveType.String && !string.IsNullOrEmpty(expr.Format))
        {
            baseType = expr.Format switch
            {
                "date" => "z.string().date()",
                "date-time" => "z.string().datetime()",
                "uuid" => "z.string().uuid()",
                "uri" => "z.string().url()",
                "email" => "z.string().email()",
                _ => baseType
            };
        }
        
        // Применяем формат для number (int32, int64 -> .int())
        if (expr.Type == ZodPrimitiveType.Number && !string.IsNullOrEmpty(expr.Format))
        {
            if (expr.Format == "int32" || expr.Format == "int64")
            {
                baseType = "z.number().int()";
            }
        }

        // Применяем валидации
        if (expr.Validations != null)
        {
            if (!string.IsNullOrEmpty(expr.Validations.Minimum))
            {
                baseType += $".min({expr.Validations.Minimum})";
            }
            if (!string.IsNullOrEmpty(expr.Validations.Maximum))
            {
                baseType += $".max({expr.Validations.Maximum})";
            }
            if (expr.Validations.MinLength.HasValue)
            {
                baseType += $".min({expr.Validations.MinLength})";
            }
            if (expr.Validations.MaxLength.HasValue)
            {
                baseType += $".max({expr.Validations.MaxLength})";
            }
            if (!string.IsNullOrEmpty(expr.Validations.Pattern))
            {
                baseType += $".regex(/{expr.Validations.Pattern}/)";
            }
        }

        return baseType;
    }

    /// <summary>
    /// Рендерит объектное выражение
    /// </summary>
    private string RenderObjectExpression(ZodObjectExpression expr)
    {
        var props = new List<string>();
        foreach (var prop in expr.Properties)
        {
            var camelCaseName = _namingConverter.ToCamelCase(prop.Key);
            var zodSchema = RenderExpression(prop.Value.Expression);
            var optional = prop.Value.IsRequired ? "" : ".optional()";
            props.Add($"{camelCaseName}: {zodSchema}{optional}");
        }
        
        return $"z.object({{{string.Join(", ", props)}}})";
    }

    /// <summary>
    /// Рендерит union выражение
    /// </summary>
    private string RenderUnionExpression(ZodUnionExpression expr)
    {
        var schemas = expr.Expressions.Select(RenderExpression).ToList();
        return $"z.union([{string.Join(", ", schemas)}])";
    }

    /// <summary>
    /// Рендерит intersection выражение (для allOf)
    /// </summary>
    private string RenderIntersectionExpression(ZodIntersectionExpression expr)
    {
        // Для allOf в Zod мы объединяем свойства объектов, поэтому это должно быть z.object
        // Но если это не объекты, используем intersection
        // Пока упрощаем - для allOf всегда создаем объект с объединенными свойствами
        // Это уже обрабатывается в MapAllOfToZodSchema, который создает ZodObjectExpression
        var schemas = expr.Expressions.Select(RenderExpression).ToList();
        return $"z.intersection({string.Join(", ", schemas)})";
    }

    /// <summary>
    /// Рендерит enum выражение
    /// </summary>
    private string RenderEnumExpression(ZodEnumExpression expr)
    {
        // Если это ссылка на enum схему
        if (!string.IsNullOrEmpty(expr.SchemaName))
        {
            return expr.SchemaName;
        }

        // Если это inline enum
        if (expr.Values != null && expr.Values.Count > 0)
        {
            if (expr.UnderlyingType == "string")
            {
                var values = expr.Values.Select(v => v.ValueString).ToList();
                return $"z.enum([{string.Join(", ", values)}])";
            }
            else
            {
                // Для числовых enum используем z.union с z.literal
                var literals = expr.Values.Select(v => $"z.literal({v.ValueString})").ToList();
                return $"z.union([{string.Join(", ", literals)}])";
            }
        }

        return "z.any()";
    }

    #endregion
}
