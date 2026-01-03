using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;
using ValIO.Generators.CSharp.Models;

namespace ValIO.Generators.CSharp;

/// <summary>
/// Рендерер для генерации C# кода через StringBuilders
/// </summary>
public class CSharpCodeRenderer : ICodeRenderer
{
    public GenerationLanguage Language => GenerationLanguage.CSharp;

    /// <summary>
    /// Рендерит модель (класс/record/struct)
    /// </summary>
    public string RenderModel(IModelTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Получаем типизированные Options через проверку типа через object
        CSharpModelTemplateDataOptions typedOptions;
        try
        {
            var typedData = (ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions>)(object)data;
            typedOptions = typedData.Options;
        }
        catch
        {
            // Если это базовый тип, создаем Options по умолчанию
            typedOptions = new CSharpModelTemplateDataOptions();
        }
        
        // Определяем тип модели из Format и Options
        var (typeKeyword, isRecord) = GetModelTypeInfo(data.Format, typedOptions);
        var isSealed = typedOptions.IsSealed;
        var generateEqualityMembers = typedOptions.GenerateEqualityMembers;
        var generateValidationConstants = typedOptions.GenerateValidationConstants;
        var generateValidationAttributes = typedOptions.GenerateValidationAttributes;
        
        // Определяем нужны ли DataAnnotations (если есть валидационные атрибуты)
        var needsDataAnnotations = generateValidationAttributes;
        
        // Определяем нужны ли регулярные выражения (проверяем свойства на наличие Pattern)
        var needsRegularExpressions = data.Properties.Any(p => !string.IsNullOrEmpty(p.Pattern));
        
        // Определяем модификатор sealed
        var sealedModifier = isSealed && data.Format == ModelFormat.Class ? "sealed " : string.Empty;
        
        // Генерируем using директивы
        if (needsDataAnnotations)
        {
            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        }
        if (needsRegularExpressions)
        {
            sb.AppendLine("using System.Text.RegularExpressions;");
        }
        
        // Генерируем namespace
        if (!string.IsNullOrEmpty(data.Namespace))
        {
            sb.AppendLine($"namespace {data.Namespace};");
            sb.AppendLine();
        }
        
        // Генерируем XML комментарий для класса
        CommentGenerator.GenerateXmlComment(sb, 0, data.Description);
        
        // Генерируем объявление класса
        var baseClassDeclaration = !string.IsNullOrEmpty(data.BaseClassName) ? $" : {data.BaseClassName}" : string.Empty;
        
        if (isRecord)
        {
            // Record синтаксис с primary constructor
            sb.AppendLine($"{sealedModifier}public {typeKeyword} {data.Name}{baseClassDeclaration}");
            sb.AppendLine("{");
            
            // Генерируем константы валидации для record
            if (generateValidationConstants)
            {
                var baseProperties = data.Properties.Cast<PropertyTemplateData>().ToList();
                GenerateValidationConstants(sb, baseProperties);
            }
            
            // Генерируем свойства для record
            foreach (var property in data.Properties)
            {
                var propertyOptions = ExtractPropertyOptions(property);
                GenerateProperty(sb, property, propertyOptions, generateValidationAttributes, generateValidationConstants, data.Name);
            }
            
            sb.AppendLine("}");
            
            if (!generateEqualityMembers)
            {
                sb.AppendLine();
                sb.AppendLine("// Методы сравнения (Equals, GetHashCode) генерируются автоматически компилятором для record");
            }
        }
        else
        {
            // Class/Struct синтаксис
            sb.AppendLine($"{sealedModifier}public {typeKeyword} {data.Name}{baseClassDeclaration}");
            sb.AppendLine("{");
            
            // Генерируем константы валидации для class
            if (generateValidationConstants)
            {
                var baseProperties = data.Properties.Cast<PropertyTemplateData>().ToList();
                GenerateValidationConstants(sb, baseProperties);
            }
            
            // Генерируем свойства для class
            foreach (var property in data.Properties)
            {
                var propertyOptions = ExtractPropertyOptions(property);
                GenerateProperty(sb, property, propertyOptions, generateValidationAttributes, generateValidationConstants, data.Name);
            }
            
            // Генерируем методы сравнения для class (если нужно)
            if (generateEqualityMembers && data.Properties.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("    public override bool Equals(object? obj)");
                sb.AppendLine("    {");
                sb.AppendLine($"        if (obj is not {data.Name} other)");
                sb.AppendLine("            return false;");
                sb.AppendLine();
                
                if (data.Properties.Count > 0)
                {
                    sb.Append("        return ");
                    for (int i = 0; i < data.Properties.Count; i++)
                    {
                        var property = data.Properties[i];
                        if (i > 0)
                        {
                            sb.AppendLine();
                            sb.Append("            && ");
                        }
                        sb.Append($"{property.Name} == other.{property.Name}");
                    }
                    sb.AppendLine(";");
                }
                else
                {
                    sb.AppendLine("        return true;");
                }
                
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    public override int GetHashCode()");
                sb.AppendLine("    {");
                sb.AppendLine("        var hash = new HashCode();");
                foreach (var property in data.Properties)
                {
                    sb.AppendLine($"        hash.Add({property.Name});");
                }
                sb.AppendLine("        return hash.ToHashCode();");
                sb.AppendLine("    }");
            }
            
            sb.AppendLine("}");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит enum
    /// </summary>
    public string RenderEnum(EnumTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Генерируем namespace
        if (!string.IsNullOrEmpty(data.Namespace))
        {
            sb.AppendLine($"namespace {data.Namespace};");
            sb.AppendLine();
        }
        
        // Генерируем XML комментарий для enum
        CommentGenerator.GenerateXmlComment(sb, 0, data.Description);
        
        // Генерируем объявление enum
        var underlyingType = data.UnderlyingType ?? "int";
        sb.AppendLine($"public enum {data.Name} : {underlyingType}");
        sb.AppendLine("{");
        
        // Генерируем значения enum
        for (int i = 0; i < data.Values.Count; i++)
        {
            var value = data.Values[i];
            
            // Генерируем XML комментарий для значения
            CommentGenerator.GenerateXmlComment(sb, 1, value.Description);
            
            // Генерируем значение enum
            var comma = i < data.Values.Count - 1 ? "," : "";
            sb.AppendLine($"    {value.Name} = {value.ValueString}{comma}");
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит клиент (HTTP клиент)
    /// </summary>
    public string RenderClient(IClientTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Генерируем using директивы
        sb.AppendLine("using System;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using System.Net.Http.Headers;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine();
        
        // Генерируем namespace
        if (!string.IsNullOrEmpty(data.Namespace))
        {
            sb.AppendLine($"namespace {data.Namespace};");
            sb.AppendLine();
        }
        
        // Генерируем XML комментарий для клиента
        var clientName = data.Name;
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// HTTP клиент для {clientName}");
        sb.AppendLine("/// </summary>");
        
        // Генерируем объявление класса клиента
        var className = data.Name;
        sb.AppendLine($"public partial class {className}");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly HttpClient _httpClient;");
        sb.AppendLine("    private readonly JsonSerializerOptions? _jsonSerializerOptions;");
        sb.AppendLine();
        sb.AppendLine($"    public {className}(HttpClient httpClient) : this(httpClient, null)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public {className}(HttpClient httpClient, JsonSerializerOptions? jsonSerializerOptions)");
        sb.AppendLine("    {");
        sb.AppendLine("        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));");
        sb.AppendLine("        _jsonSerializerOptions = jsonSerializerOptions;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Генерируем partial методы для кастомизации
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Вызывается перед отправкой HTTP запроса. Позволяет кастомизировать запрос.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"request\">HTTP запрос</param>");
        sb.AppendLine("    /// <param name=\"url\">URL запроса</param>");
        sb.AppendLine("    /// <param name=\"cancellationToken\">Токен отмены</param>");
        sb.AppendLine("    partial void PrepareRequestAsync(HttpRequestMessage request, string url, CancellationToken cancellationToken);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Вызывается после получения HTTP ответа, но до десериализации. Позволяет кастомизировать обработку ответа.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"response\">HTTP ответ</param>");
        sb.AppendLine("    /// <param name=\"cancellationToken\">Токен отмены</param>");
        sb.AppendLine("    partial void ProcessResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken);");
        sb.AppendLine();
        
        // Генерируем операции
        foreach (var operationBase in data.Operations)
        {
            // Приводим типизированную операцию к базовому типу
            var operation = operationBase as OperationTemplateData ?? (OperationTemplateData)(object)operationBase;
            CodeFormatter.AppendEmptyLine(sb);
            RenderOperation(sb, operation, indentLevel: 1);
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит операцию (метод HTTP клиента)
    /// </summary>
    public string RenderOperation(IOperationTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        RenderOperation(sb, data, indentLevel: 0);
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит using директивы
    /// </summary>
    public string RenderUsings(IReadOnlyList<string> usings)
    {
        if (usings == null || usings.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var usingDirective in usings.Distinct().OrderBy(u => u))
        {
            sb.AppendLine($"using {usingDirective};");
        }
        return sb.ToString();
    }

    #region Private Helper Methods

    /// <summary>
    /// Извлекает CSharpPropertyTemplateDataOptions из property через приведение типов
    /// </summary>
    private static CSharpPropertyTemplateDataOptions? ExtractPropertyOptions(IPropertyTemplateData property)
    {
        // Пытаемся привести к типизированному типу через object
        try
        {
            var typedPropertyCast = (PropertyTemplateData<CSharpPropertyTemplateDataOptions>)(object)property;
            return typedPropertyCast.Options;
        }
        catch
        {
            // Если не удалось привести, возвращаем null (будет создан по умолчанию)
            return null;
        }
    }

    private void RenderOperation(StringBuilder sb, IOperationTemplateData operation, int indentLevel)
    {
        // Генерируем XML комментарий для метода
        CommentGenerator.GenerateXmlComment(sb, indentLevel, operation.Summary);
        
        // Генерируем сигнатуру метода
        var returnType = operation.ReturnTypeInfo.FullTypeName;
        var parameters = BuildParameterString(operation);
        CodeFormatter.AppendLine(sb, indentLevel, $"public async {returnType} {operation.MethodName}({parameters}, CancellationToken cancellationToken = default)");
        CodeFormatter.AppendLine(sb, indentLevel, "{");
        
        // Генерируем валидацию параметров
        GenerateParameterValidation(sb, operation, indentLevel + 1);
        
        // Генерируем построение пути
        GeneratePathBuilding(sb, operation, indentLevel + 1);
        
        // Генерируем построение query параметров
        GenerateQueryParameters(sb, operation, indentLevel + 1);
        
        // Генерируем создание запроса
        CodeFormatter.AppendLine(sb, indentLevel + 1, $"var request = new HttpRequestMessage(HttpMethod.{operation.HttpMethod}, url);");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, indentLevel + 1, "// Вызываем partial method для кастомизации запроса перед отправкой");
        CodeFormatter.AppendLine(sb, indentLevel + 1, "PrepareRequestAsync(request, url, cancellationToken);");
        CodeFormatter.AppendEmptyLine(sb);
        
        // Генерируем header параметры
        GenerateHeaderParameters(sb, operation, indentLevel + 1);
        
        // Генерируем request body
        GenerateRequestBody(sb, operation, indentLevel + 1);
        
        // Генерируем отправку запроса и обработку ответа
        CodeFormatter.AppendLine(sb, indentLevel + 1, "var response = await _httpClient.SendAsync(request, cancellationToken);");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, indentLevel + 1, "// Вызываем partial method для кастомизации ответа после получения");
        CodeFormatter.AppendLine(sb, indentLevel + 1, "ProcessResponseAsync(response, cancellationToken);");
        CodeFormatter.AppendEmptyLine(sb);
        
        // Генерируем обработку ответов
        GenerateResponseHandling(sb, operation, indentLevel + 1);
        
        CodeFormatter.AppendLine(sb, indentLevel, "}");
    }

    private void GenerateProperty(StringBuilder sb, IPropertyTemplateData property, CSharpPropertyTemplateDataOptions? propertyOptions, bool generateValidationAttributes, bool generateValidationConstants, string className)
    {
        // Используем переданные Options или создаем по умолчанию
        propertyOptions ??= new CSharpPropertyTemplateDataOptions();
        
        // Генерируем XML комментарий для свойства
        CommentGenerator.GenerateXmlComment(sb, 1, property.Description);
        
        // Генерируем атрибуты валидации
        if (generateValidationAttributes)
        {
            GenerateValidationAttributes(sb, property, generateValidationConstants, className);
        }
        
        // Генерируем объявление свойства
        var requiredModifier = property.IsRequired ? "required " : string.Empty;
        var typeString = property.TypeInfo.FullTypeName;
        var nullableModifier = property.IsNullable && !property.TypeInfo.IsNullable && !typeString.EndsWith("?") ? "?" : string.Empty;
        var accessorType = propertyOptions.AccessorType;
        var accessor = accessorType == PropertyAccessorType.Init ? "init" : "set";
        
        CodeFormatter.AppendLine(sb, 1, $"public {requiredModifier}{typeString}{nullableModifier} {property.Name} {{ get; {accessor}; }}");
    }

    private void GenerateValidationConstants(StringBuilder sb, IReadOnlyList<IPropertyTemplateData> propertiesBase)
    {
        foreach (var property in propertiesBase)
        {
            var hasConstants = property.DefaultValue != null || property.Min.HasValue || property.Max.HasValue ||
                               property.ExclusiveMin.HasValue || property.ExclusiveMax.HasValue ||
                               property.MinLength.HasValue || property.MaxLength.HasValue ||
                               !string.IsNullOrEmpty(property.Pattern);
            
            if (!hasConstants)
                continue;
            
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Константы валидации для свойства {property.Name}");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public static class {property.Name}Constants");
            sb.AppendLine("    {");
            
            if (property.MinLength.HasValue)
            {
                sb.AppendLine($"        public const int MinLength = {property.MinLength.Value};");
            }
            if (property.MaxLength.HasValue)
            {
                sb.AppendLine($"        public const int MaxLength = {property.MaxLength.Value};");
            }
            if (property.Min.HasValue)
            {
                sb.AppendLine($"        public const decimal Min = {property.Min.Value};");
            }
            if (property.Max.HasValue)
            {
                sb.AppendLine($"        public const decimal Max = {property.Max.Value};");
            }
            if (property.ExclusiveMin.HasValue)
            {
                sb.AppendLine($"        public const decimal ExclusiveMin = {property.ExclusiveMin.Value};");
            }
            if (property.ExclusiveMax.HasValue)
            {
                sb.AppendLine($"        public const decimal ExclusiveMax = {property.ExclusiveMax.Value};");
            }
            if (!string.IsNullOrEmpty(property.Pattern))
            {
                var escapedPattern = CodeEscapeHelper.EscapeString(property.Pattern);
                CodeFormatter.AppendLine(sb, 2, $"public const string Pattern = \"{escapedPattern}\";");
                CodeFormatter.AppendEmptyLine(sb);
                CodeFormatter.AppendLine(sb, 2, "public static readonly Regex PatternRegex = new Regex(Pattern, RegexOptions.Compiled);");
            }
            if (property.DefaultValue != null)
            {
                var defaultValueString = DefaultValueFormatter.FormatDefaultValue(property.DefaultValue, property.TypeInfo);
                var typeString = property.TypeInfo.FullTypeName;
                CodeFormatter.AppendLine(sb, 2, $"public const {typeString} DefaultValue = {defaultValueString};");
            }
            
            CodeFormatter.AppendLine(sb, 1, "}");
            CodeFormatter.AppendEmptyLine(sb);
        }
    }

    private void GenerateValidationAttributes(StringBuilder sb, IPropertyTemplateData property, bool useConstants, string className)
    {
        if (property.IsRequired)
        {
            sb.AppendLine("    [Required]");
        }
        
        var propertyHasConstants = useConstants && (property.DefaultValue != null || property.Min.HasValue || property.Max.HasValue ||
                                                    property.ExclusiveMin.HasValue || property.ExclusiveMax.HasValue ||
                                                    property.MinLength.HasValue || property.MaxLength.HasValue ||
                                                    !string.IsNullOrEmpty(property.Pattern));
        
        if (propertyHasConstants)
        {
            // Используем константы из класса
            if (property.MinLength.HasValue && property.MaxLength.HasValue)
            {
                sb.AppendLine($"    [StringLength({property.Name}Constants.MaxLength, MinimumLength = {property.Name}Constants.MinLength)]");
            }
            else
            {
                if (property.MinLength.HasValue)
                {
                    sb.AppendLine($"    [MinLength({property.Name}Constants.MinLength)]");
                }
                if (property.MaxLength.HasValue)
                {
                    sb.AppendLine($"    [MaxLength({property.Name}Constants.MaxLength)]");
                }
            }
            if (property.Min.HasValue && property.Max.HasValue)
            {
                sb.AppendLine($"    [Range({property.Name}Constants.Min, {property.Name}Constants.Max)]");
            }
            else
            {
                if (property.Min.HasValue)
                {
                    sb.AppendLine($"    [Range({property.Name}Constants.Min, double.MaxValue)]");
                }
                if (property.Max.HasValue)
                {
                    sb.AppendLine($"    [Range(double.MinValue, {property.Name}Constants.Max)]");
                }
            }
            if (!string.IsNullOrEmpty(property.Pattern))
            {
                sb.AppendLine($"    [RegularExpression({property.Name}Constants.Pattern)]");
            }
        }
        else
        {
            // Используем прямые значения
            if (property.MinLength.HasValue && property.MaxLength.HasValue)
            {
                sb.AppendLine($"    [StringLength({property.MaxLength.Value}, MinimumLength = {property.MinLength.Value})]");
            }
            else
            {
                if (property.MinLength.HasValue)
                {
                    sb.AppendLine($"    [MinLength({property.MinLength.Value})]");
                }
                if (property.MaxLength.HasValue)
                {
                    sb.AppendLine($"    [MaxLength({property.MaxLength.Value})]");
                }
            }
            if (property.Min.HasValue && property.Max.HasValue)
            {
                sb.AppendLine($"    [Range({property.Min.Value}, {property.Max.Value})]");
            }
            else
            {
                if (property.Min.HasValue)
                {
                    sb.AppendLine($"    [Range({property.Min.Value}, double.MaxValue)]");
                }
                if (property.Max.HasValue)
                {
                    sb.AppendLine($"    [Range(double.MinValue, {property.Max.Value})]");
                }
            }
            if (!string.IsNullOrEmpty(property.Pattern))
            {
                var escapedPattern = CodeEscapeHelper.EscapeString(property.Pattern);
                sb.AppendLine($"    [RegularExpression(\"{escapedPattern}\")]");
            }
        }
    }

    private void GenerateParameterValidation(StringBuilder sb, IOperationTemplateData operation, int indentLevel)
    {
        foreach (var param in operation.PathParameters)
        {
            if (param.IsRequired && !param.TypeInfo.IsNonNullableValueType())
            {
                CodeFormatter.AppendLine(sb, indentLevel, $"if ({param.VariableName} == null)");
                CodeFormatter.AppendLine(sb, indentLevel + 1, $"throw new ArgumentNullException(nameof({param.VariableName}));");
            }
        }
        
        foreach (var param in operation.QueryParameters)
        {
            if (param.IsRequired && !param.TypeInfo.IsNonNullableValueType())
            {
                CodeFormatter.AppendLine(sb, indentLevel, $"if ({param.VariableName} == null)");
                CodeFormatter.AppendLine(sb, indentLevel + 1, $"throw new ArgumentNullException(nameof({param.VariableName}));");
            }
        }
        
        if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            if (operation.RequestBodyContentType == "application/json" && 
                operation.RequestBodyTypeInfo != null && 
                !operation.RequestBodyTypeInfo.IsNonNullableValueType())
            {
                CodeFormatter.AppendLine(sb, indentLevel, $"if ({operation.RequestBodyParameter} == null)");
                CodeFormatter.AppendLine(sb, indentLevel + 1, $"throw new ArgumentNullException(nameof({operation.RequestBodyParameter}));");
            }
        }
    }

    private void GeneratePathBuilding(StringBuilder sb, IOperationTemplateData operation, int indentLevel)
    {
        if (operation.PathParameters.Count > 0)
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"var path = \"{operation.Path}\";");
            foreach (var param in operation.PathParameters)
            {
                if (param.TypeInfo.IsNonNullableValueType())
                {
                    CodeFormatter.AppendLine(sb, indentLevel, $"path = path.Replace(\"{{{param.Name}}}\", Uri.EscapeDataString({param.VariableName}.ToString()));");
                }
                else
                {
                    CodeFormatter.AppendLine(sb, indentLevel, $"path = path.Replace(\"{{{param.Name}}}\", Uri.EscapeDataString({param.VariableName}?.ToString() ?? string.Empty));");
                }
            }
            CodeFormatter.AppendLine(sb, indentLevel, "var url = path;");
        }
        else
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"var url = \"{operation.Path}\";");
        }
    }

    private void GenerateQueryParameters(StringBuilder sb, IOperationTemplateData operation, int indentLevel)
    {
        if (operation.QueryParameters.Count == 0)
            return;
        
        CodeFormatter.AppendLine(sb, indentLevel, "var queryParams = new List<string>();");
        
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            CodeFormatter.AppendLine(sb, indentLevel, "if (query != null)");
            CodeFormatter.AppendLine(sb, indentLevel, "{");
            foreach (var param in operation.QueryParameters)
            {
                CodeFormatter.AppendLine(sb, indentLevel + 1, $"var {param.VariableName} = query.{param.PropertyName};");
                CodeFormatter.AppendLine(sb, indentLevel + 1, $"if ({param.VariableName} != null)");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "{");
                GenerateQueryParameterValue(sb, param, indentLevel + 2);
                CodeFormatter.AppendLine(sb, indentLevel + 1, "}");
            }
            CodeFormatter.AppendLine(sb, indentLevel, "}");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                CodeFormatter.AppendLine(sb, indentLevel, $"if ({param.VariableName} != null)");
                CodeFormatter.AppendLine(sb, indentLevel, "{");
                GenerateQueryParameterValue(sb, param, indentLevel + 1);
                CodeFormatter.AppendLine(sb, indentLevel, "}");
            }
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, "if (queryParams.Count > 0)");
        CodeFormatter.AppendLine(sb, indentLevel, "{");
        CodeFormatter.AppendLine(sb, indentLevel + 1, "url += (url.Contains(\"?\") ? \"&\" : \"?\") + string.Join(\"&\", queryParams);");
        CodeFormatter.AppendLine(sb, indentLevel, "}");
    }

    private void GenerateQueryParameterValue(StringBuilder sb, ParameterTemplateData param, int indentLevel)
    {
        var paramNameEscaped = CodeEscapeHelper.EscapeString(param.Name);
        
        if (param.TypeInfo.IsCollection)
        {
            var itemType = param.TypeInfo.GenericArguments.FirstOrDefault();
            var isItemNonNullableValueType = itemType?.IsNonNullableValueType() ?? false;
            CodeFormatter.AppendLine(sb, indentLevel, $"foreach (var item in {param.VariableName})");
            CodeFormatter.AppendLine(sb, indentLevel, "{");
            if (isItemNonNullableValueType)
            {
                CodeFormatter.AppendLine(sb, indentLevel + 1, $"queryParams.Add($\"{paramNameEscaped}={{Uri.EscapeDataString(item.ToString())}}\");");
            }
            else
            {
                CodeFormatter.AppendLine(sb, indentLevel + 1, "if (item != null)");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 2, $"queryParams.Add($\"{paramNameEscaped}={{Uri.EscapeDataString(item.ToString() ?? string.Empty)}}\");");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "}");
            }
            CodeFormatter.AppendLine(sb, indentLevel, "}");
        }
        else if (param.TypeInfo.IsDictionary)
        {
            var valueType = param.TypeInfo.GenericArguments.Count > 1 ? param.TypeInfo.GenericArguments[1] : null;
            var isValueNonNullableValueType = valueType?.IsNonNullableValueType() ?? false;
            CodeFormatter.AppendLine(sb, indentLevel, $"foreach (var kvp in {param.VariableName})");
            CodeFormatter.AppendLine(sb, indentLevel, "{");
            if (isValueNonNullableValueType)
            {
                CodeFormatter.AppendLine(sb, indentLevel + 1, "queryParams.Add($\"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value.ToString())}\");");
            }
            else
            {
                CodeFormatter.AppendLine(sb, indentLevel + 1, "queryParams.Add($\"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value?.ToString() ?? string.Empty)}\");");
            }
            CodeFormatter.AppendLine(sb, indentLevel, "}");
        }
        else if (!param.TypeInfo.IsPrimitive && param.TypeInfo.BaseType != "object")
        {
            CodeFormatter.AppendLine(sb, indentLevel, "// Complex object - serialize to JSON and add as query parameter");
            CodeFormatter.AppendLine(sb, indentLevel, $"var jsonValue = System.Text.Json.JsonSerializer.Serialize({param.VariableName});");
            CodeFormatter.AppendLine(sb, indentLevel, $"queryParams.Add($\"{paramNameEscaped}={{Uri.EscapeDataString(jsonValue)}}\");");
        }
        else
        {
            if (param.TypeInfo.IsNonNullableValueType())
            {
                CodeFormatter.AppendLine(sb, indentLevel, $"queryParams.Add($\"{paramNameEscaped}={{Uri.EscapeDataString({param.VariableName}.ToString())}}\");");
            }
            else
            {
                CodeFormatter.AppendLine(sb, indentLevel, $"queryParams.Add($\"{paramNameEscaped}={{Uri.EscapeDataString({param.VariableName}?.ToString() ?? string.Empty)}}\");");
            }
        }
    }

    private void GenerateHeaderParameters(StringBuilder sb, IOperationTemplateData operation, int indentLevel)
    {
        if (operation.HeaderParameters.Count == 0)
            return;
        
        foreach (var param in operation.HeaderParameters)
        {
            if (param.IsRequired && !param.TypeInfo.IsNonNullableValueType())
            {
                CodeFormatter.AppendLine(sb, indentLevel, $"if ({param.VariableName} == null)");
                CodeFormatter.AppendLine(sb, indentLevel + 1, $"throw new ArgumentNullException(nameof({param.VariableName}));");
            }
            if (param.TypeInfo.IsNonNullableValueType())
            {
                CodeFormatter.AppendLine(sb, indentLevel, $"request.Headers.Add(\"{param.Name}\", {param.VariableName}.ToString());");
            }
            else
            {
                CodeFormatter.AppendLine(sb, indentLevel, $"if ({param.VariableName} != null)");
                CodeFormatter.AppendLine(sb, indentLevel, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 1, $"request.Headers.Add(\"{param.Name}\", {param.VariableName}.ToString());");
                CodeFormatter.AppendLine(sb, indentLevel, "}");
            }
        }
        CodeFormatter.AppendEmptyLine(sb);
    }

    private void GenerateRequestBody(StringBuilder sb, IOperationTemplateData operation, int indentLevel)
    {
        if (!operation.HasRequestBody)
            return;
        
        if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            if (operation.RequestBodyContentType == "application/x-www-form-urlencoded")
            {
                CodeFormatter.AppendLine(sb, indentLevel, "var formData = new Dictionary<string, string>();");
                foreach (var prop in operation.FormDataProperties)
                {
                    if (prop.IsRequired && !prop.TypeInfo.IsNonNullableValueType())
                    {
                        CodeFormatter.AppendLine(sb, indentLevel, $"if ({prop.VariableName} == null)");
                        CodeFormatter.AppendLine(sb, indentLevel + 1, $"throw new ArgumentNullException(nameof({prop.VariableName}));");
                    }
                    // Генерация form-data свойств (упрощенная версия)
                    if (prop.TypeInfo.IsCollection)
                    {
                        CodeFormatter.AppendLine(sb, indentLevel, $"if ({prop.VariableName} != null)");
                        CodeFormatter.AppendLine(sb, indentLevel, "{");
                        CodeFormatter.AppendLine(sb, indentLevel + 1, $"foreach (var item in {prop.VariableName})");
                        CodeFormatter.AppendLine(sb, indentLevel + 1, "{");
                        CodeFormatter.AppendLine(sb, indentLevel + 2, $"formData.Add(\"{prop.Name}\", item?.ToString() ?? string.Empty);");
                        CodeFormatter.AppendLine(sb, indentLevel + 1, "}");
                        CodeFormatter.AppendLine(sb, indentLevel, "}");
                    }
                    else
                    {
                        if (prop.TypeInfo.IsNonNullableValueType())
                        {
                            CodeFormatter.AppendLine(sb, indentLevel, $"formData.Add(\"{prop.Name}\", {prop.VariableName}.ToString());");
                        }
                        else
                        {
                            CodeFormatter.AppendLine(sb, indentLevel, $"if ({prop.VariableName} != null)");
                            CodeFormatter.AppendLine(sb, indentLevel, "{");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, $"formData.Add(\"{prop.Name}\", {prop.VariableName}.ToString() ?? string.Empty);");
                            CodeFormatter.AppendLine(sb, indentLevel, "}");
                        }
                    }
                }
                CodeFormatter.AppendLine(sb, indentLevel, "request.Content = new FormUrlEncodedContent(formData);");
            }
            else if (operation.RequestBodyContentType == "multipart/form-data")
            {
                CodeFormatter.AppendLine(sb, indentLevel, "var multipartContent = new MultipartFormDataContent();");
                foreach (var prop in operation.FormDataProperties)
                {
                    if (prop.IsRequired && !prop.TypeInfo.IsNonNullableValueType() && !prop.TypeInfo.IsFile)
                    {
                        CodeFormatter.AppendLine(sb, indentLevel, $"if ({prop.VariableName} == null)");
                        CodeFormatter.AppendLine(sb, indentLevel + 1, $"throw new ArgumentNullException(nameof({prop.VariableName}));");
                    }
                    if (prop.TypeInfo.IsFile)
                    {
                        if (prop.TypeInfo.IsCollection)
                        {
                            CodeFormatter.AppendLine(sb, indentLevel, $"if ({prop.VariableName} != null)");
                            CodeFormatter.AppendLine(sb, indentLevel, "{");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, $"foreach (var file in {prop.VariableName})");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, "{");
                            CodeFormatter.AppendLine(sb, indentLevel + 2, "if (file != null)");
                            CodeFormatter.AppendLine(sb, indentLevel + 2, "{");
                            CodeFormatter.AppendLine(sb, indentLevel + 3, "var streamContent = new StreamContent(file);");
                            CodeFormatter.AppendLine(sb, indentLevel + 3, $"multipartContent.Add(streamContent, \"{prop.Name}\", \"file\");");
                            CodeFormatter.AppendLine(sb, indentLevel + 2, "}");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, "}");
                            CodeFormatter.AppendLine(sb, indentLevel, "}");
                        }
                        else
                        {
                            CodeFormatter.AppendLine(sb, indentLevel, $"if ({prop.VariableName} != null)");
                            CodeFormatter.AppendLine(sb, indentLevel, "{");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, $"var streamContent = new StreamContent({prop.VariableName});");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, $"multipartContent.Add(streamContent, \"{prop.Name}\", \"file\");");
                            CodeFormatter.AppendLine(sb, indentLevel, "}");
                        }
                    }
                    // Другие типы form-data свойств (упрощенная версия)
                }
                CodeFormatter.AppendLine(sb, indentLevel, "request.Content = multipartContent;");
            }
        }
        else if (operation.RequestBodyContentType == "application/json")
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"var jsonContent = System.Text.Json.JsonSerializer.Serialize({operation.RequestBodyParameter}, _jsonSerializerOptions);");
            CodeFormatter.AppendLine(sb, indentLevel, "request.Content = new StringContent(jsonContent, Encoding.UTF8, \"application/json\");");
        }
        else
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"request.Content = new StringContent({operation.RequestBodyParameter}?.ToString() ?? string.Empty, Encoding.UTF8, \"{operation.RequestBodyContentType ?? "application/json"}\");");
        }
        CodeFormatter.AppendEmptyLine(sb);
    }

    private void GenerateResponseHandling(StringBuilder sb, IOperationTemplateData operation, int indentLevel)
    {
        CodeFormatter.AppendLine(sb, indentLevel, "// Обрабатываем ответы и выбрасываем исключения для ошибок");
        CodeFormatter.AppendLine(sb, indentLevel, "var statusCode = (int)response.StatusCode;");
        
        if (operation.Responses.Count > 0)
        {
            foreach (var responseInfo in operation.Responses)
            {
                if (!responseInfo.IsSuccess && !string.IsNullOrEmpty(responseInfo.ExceptionClassName))
                {
                    var statusCodeValue = int.TryParse(responseInfo.StatusCode, out var code) ? code : 0;
                    if (statusCodeValue > 0)
                    {
                        CodeFormatter.AppendLine(sb, indentLevel, $"if (statusCode == {statusCodeValue})");
                        CodeFormatter.AppendLine(sb, indentLevel, "{");
                        if (responseInfo.ResponseTypeInfo != null)
                        {
                            var errorResponseType = responseInfo.ResponseTypeInfo.FullTypeName;
                            CodeFormatter.AppendLine(sb, indentLevel + 1, "// Десериализуем ответ ошибки");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, $"{errorResponseType}? errorResponse = null;");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, "if (response.Content != null && response.Content.Headers.ContentLength > 0)");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, "{");
                            CodeFormatter.AppendLine(sb, indentLevel + 2, "var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);");
                            CodeFormatter.AppendLine(sb, indentLevel + 2, "if (!string.IsNullOrWhiteSpace(responseContent))");
                            CodeFormatter.AppendLine(sb, indentLevel + 2, "{");
                            CodeFormatter.AppendLine(sb, indentLevel + 3, "try");
                            CodeFormatter.AppendLine(sb, indentLevel + 3, "{");
                            CodeFormatter.AppendLine(sb, indentLevel + 4, $"errorResponse = System.Text.Json.JsonSerializer.Deserialize<{errorResponseType}>(responseContent, _jsonSerializerOptions);");
                            CodeFormatter.AppendLine(sb, indentLevel + 3, "}");
                            CodeFormatter.AppendLine(sb, indentLevel + 3, "catch (JsonException ex)");
                            CodeFormatter.AppendLine(sb, indentLevel + 3, "{");
                            CodeFormatter.AppendLine(sb, indentLevel + 4, $"throw new {responseInfo.ExceptionClassName}(response, default({errorResponseType}), ex, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                            CodeFormatter.AppendLine(sb, indentLevel + 3, "}");
                            CodeFormatter.AppendLine(sb, indentLevel + 2, "}");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, "}");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, $"throw new {responseInfo.ExceptionClassName}(response, errorResponse ?? default({errorResponseType}), null, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                        }
                        else
                        {
                            CodeFormatter.AppendLine(sb, indentLevel + 1, $"throw new {responseInfo.ExceptionClassName}(response, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                        }
                        CodeFormatter.AppendLine(sb, indentLevel, "}");
                    }
                }
            }
        }
        
        if (operation.HasResponseType)
        {
            var responseType = operation.ReturnTypeInfo.FullTypeName;
            if (!string.IsNullOrEmpty(responseType) && operation.ReturnTypeInfo.BaseType != "void")
            {
                CodeFormatter.AppendLine(sb, indentLevel, "// Обрабатываем успешный ответ (2xx)");
                CodeFormatter.AppendLine(sb, indentLevel, "if (statusCode < 200 || statusCode >= 300)");
                CodeFormatter.AppendLine(sb, indentLevel, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "response.EnsureSuccessStatusCode();");
                CodeFormatter.AppendLine(sb, indentLevel, "}");
                CodeFormatter.AppendEmptyLine(sb);
                CodeFormatter.AppendLine(sb, indentLevel, "// Десериализуем успешный ответ");
                CodeFormatter.AppendLine(sb, indentLevel, "try");
                CodeFormatter.AppendLine(sb, indentLevel, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "if (response.Content == null || response.Content.Headers.ContentLength == 0)");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 2, $"return default({responseType});");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "}");
                CodeFormatter.AppendEmptyLine(sb);
                CodeFormatter.AppendLine(sb, indentLevel + 1, "var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;");
                CodeFormatter.AppendEmptyLine(sb);
                CodeFormatter.AppendLine(sb, indentLevel + 1, "if (contentType.Contains(\"json\") || string.IsNullOrEmpty(contentType))");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);");
                CodeFormatter.AppendEmptyLine(sb);
                CodeFormatter.AppendLine(sb, indentLevel + 2, "if (string.IsNullOrWhiteSpace(responseContent))");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 3, $"return default({responseType});");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "}");
                CodeFormatter.AppendEmptyLine(sb);
                CodeFormatter.AppendLine(sb, indentLevel + 2, "try");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 3, $"var result = System.Text.Json.JsonSerializer.Deserialize<{responseType}>(responseContent, _jsonSerializerOptions);");
                CodeFormatter.AppendLine(sb, indentLevel + 3, $"return result ?? throw new ValioHttpRequestException($\"Response deserialization returned null for type {{{responseType}}}\", response, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "}");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "catch (JsonException ex)");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 3, "var preview = responseContent.Length > 500 ? responseContent.Substring(0, 500) + \"...\" : responseContent;");
                CodeFormatter.AppendLine(sb, indentLevel + 3, $"throw new ValioHttpRequestException($\"Failed to deserialize response to type {{{responseType}}}. Response preview: {{preview}}\", response, ex, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "}");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "}");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "else");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "using var memoryStream = new MemoryStream();");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "await responseStream.CopyToAsync(memoryStream, cancellationToken);");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "memoryStream.Position = 0;");
                CodeFormatter.AppendEmptyLine(sb);
                CodeFormatter.AppendLine(sb, indentLevel + 2, "try");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 3, $"var result = await System.Text.Json.JsonSerializer.DeserializeAsync<{responseType}>(memoryStream, _jsonSerializerOptions, cancellationToken);");
                CodeFormatter.AppendLine(sb, indentLevel + 3, $"return result ?? throw new ValioHttpRequestException($\"Response deserialization returned null for type {{{responseType}}}\", response, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "}");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "catch (JsonException ex)");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 3, $"throw new ValioHttpRequestException($\"Failed to deserialize response stream to type {{{responseType}}}. Content-Type: {{contentType}}\", response, ex, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "}");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "}");
                CodeFormatter.AppendLine(sb, indentLevel, "}");
                CodeFormatter.AppendLine(sb, indentLevel, "catch (ValioHttpRequestException)");
                CodeFormatter.AppendLine(sb, indentLevel, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "throw;");
                CodeFormatter.AppendLine(sb, indentLevel, "}");
                CodeFormatter.AppendLine(sb, indentLevel, "catch (HttpRequestException ex)");
                CodeFormatter.AppendLine(sb, indentLevel, "{");
                CodeFormatter.AppendLine(sb, indentLevel + 1, $"throw new ValioHttpRequestException($\"HTTP request failed with status {{response.StatusCode}} ({{response.ReasonPhrase}}). Operation: {{{operation.MethodName}}}\", response, ex, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                CodeFormatter.AppendLine(sb, indentLevel, "}");
            }
            else
            {
                CodeFormatter.AppendLine(sb, indentLevel, "response.EnsureSuccessStatusCode();");
                CodeFormatter.AppendLine(sb, indentLevel, "return response;");
            }
        }
        else
        {
            CodeFormatter.AppendLine(sb, indentLevel, "response.EnsureSuccessStatusCode();");
            CodeFormatter.AppendLine(sb, indentLevel, "return response;");
        }
    }

    private string BuildParameterString(IOperationTemplateData operation)
    {
        var parameters = new List<string>();
        
        // Path параметры
        foreach (var param in operation.PathParameters)
        {
            var type = param.TypeInfo.FullTypeName;
            parameters.Add($"{type} {param.VariableName}");
        }
        
        // Query параметры
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            parameters.Add($"{operation.QueryParametersModelTypeName} query");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                var type = param.TypeInfo.FullTypeName;
                var nullable = !param.IsRequired && !param.TypeInfo.IsNonNullableValueType() ? "?" : "";
                parameters.Add($"{type}{nullable} {param.VariableName} = null");
            }
        }
        
        // Header параметры
        foreach (var param in operation.HeaderParameters)
        {
            var type = param.TypeInfo.FullTypeName;
            var nullable = !param.IsRequired && !param.TypeInfo.IsNonNullableValueType() ? "?" : "";
            parameters.Add($"{type}{nullable} {param.VariableName} = null");
        }
        
        // Request body
        if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            var type = operation.RequestBodyTypeInfo?.FullTypeName ?? "object";
            var nullable = operation.RequestBodyTypeInfo != null && !operation.RequestBodyTypeInfo.IsNonNullableValueType() ? "?" : "";
            parameters.Add($"{type}{nullable} {operation.RequestBodyParameter} = null");
        }
        
        return string.Join(", ", parameters);
    }

    // Методы FormatDefaultValue, EscapeXmlComment и EscapeString теперь в ValIO.Core.Utilities

    /// <summary>
    /// Определяет ключевое слово типа модели и флаг isRecord на основе ModelFormat и Options
    /// </summary>
    private (string typeKeyword, bool isRecord) GetModelTypeInfo(ModelFormat format, CSharpModelTemplateDataOptions options)
    {
        // Проверяем Options для RecordStruct (который не может быть в ModelFormat)
        if (options.ModelType == ModelType.RecordStruct)
        {
            return ("record struct", true);
        }
        
        // Используем ModelFormat и ModelType для основных типов
        if (options.ModelType == ModelType.Record)
        {
            return ("record", true);
        }
        else if (options.ModelType == ModelType.Struct)
        {
            return ("struct", false);
        }
        
        // Если ModelType не установлен, используем ModelFormat
        return format switch
        {
            ModelFormat.Record => ("record", true),
            ModelFormat.Struct => ("struct", false),
            ModelFormat.Class => ("class", false),
            _ => ("class", false)
        };
    }

    #endregion
}
