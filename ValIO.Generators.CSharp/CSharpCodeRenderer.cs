using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

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
    public string RenderModel(ModelTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Определяем тип модели из Format и Options
        var (typeKeyword, isRecord) = GetModelTypeInfo(data.Format, data.Options);
        var isSealed = GetOption<bool>(data.Options, "IsSealed", false);
        var generateEqualityMembers = GetOption<bool>(data.Options, "GenerateEqualityMembers", true);
        var generateValidationConstants = GetOption<bool>(data.Options, "GenerateValidationConstants", false);
        var generateValidationAttributes = GetOption<bool>(data.Options, "GenerateValidationAttributes", false);
        var needsDataAnnotations = GetOption<bool>(data.Options, "NeedsDataAnnotations", false);
        var needsRegularExpressions = GetOption<bool>(data.Options, "NeedsRegularExpressions", false);
        
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
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {EscapeXmlComment(data.Description)}");
            sb.AppendLine("/// </summary>");
        }
        
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
                GenerateValidationConstants(sb, data.Properties);
            }
            
            // Генерируем свойства для record
            foreach (var property in data.Properties)
            {
                GenerateProperty(sb, property, generateValidationAttributes, generateValidationConstants, data.Name);
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
                GenerateValidationConstants(sb, data.Properties);
            }
            
            // Генерируем свойства для class
            foreach (var property in data.Properties)
            {
                GenerateProperty(sb, property, generateValidationAttributes, generateValidationConstants, data.Name);
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
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {EscapeXmlComment(data.Description)}");
            sb.AppendLine("/// </summary>");
        }
        
        // Генерируем объявление enum
        var underlyingType = data.UnderlyingType ?? "int";
        sb.AppendLine($"public enum {data.Name} : {underlyingType}");
        sb.AppendLine("{");
        
        // Генерируем значения enum
        for (int i = 0; i < data.Values.Count; i++)
        {
            var value = data.Values[i];
            
            // Генерируем XML комментарий для значения
            if (!string.IsNullOrEmpty(value.Description))
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// {EscapeXmlComment(value.Description)}");
                sb.AppendLine("    /// </summary>");
            }
            
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
    public string RenderClient(ClientTemplateData data)
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
        var clientName = GetOption<string>(data.Options, "ClientName", data.Name);
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// HTTP клиент для {clientName}");
        sb.AppendLine("/// </summary>");
        
        // Генерируем объявление класса клиента
        var className = GetOption<string>(data.Options, "ClassName", data.Name);
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
        foreach (var operation in data.Operations)
        {
            sb.AppendLine();
            RenderOperation(sb, operation, indent: "    ");
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит операцию (метод HTTP клиента)
    /// </summary>
    public string RenderOperation(OperationTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        RenderOperation(sb, data, indent: "");
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

    private void RenderOperation(StringBuilder sb, OperationTemplateData operation, string indent)
    {
        // Генерируем XML комментарий для метода
        if (!string.IsNullOrEmpty(operation.Summary))
        {
            sb.AppendLine($"{indent}/// <summary>");
            sb.AppendLine($"{indent}/// {EscapeXmlComment(operation.Summary)}");
            sb.AppendLine($"{indent}/// </summary>");
        }
        
        // Генерируем сигнатуру метода
        var returnType = FormatType(operation.ReturnTypeInfo);
        var parameters = BuildParameterString(operation);
        sb.AppendLine($"{indent}public async {returnType} {operation.MethodName}({parameters}, CancellationToken cancellationToken = default)");
        sb.AppendLine($"{indent}{{");
        
        // Генерируем валидацию параметров
        GenerateParameterValidation(sb, operation, indent + "    ");
        
        // Генерируем построение пути
        GeneratePathBuilding(sb, operation, indent + "    ");
        
        // Генерируем построение query параметров
        GenerateQueryParameters(sb, operation, indent + "    ");
        
        // Генерируем создание запроса
        sb.AppendLine($"{indent}    var request = new HttpRequestMessage(HttpMethod.{operation.HttpMethod}, url);");
        sb.AppendLine();
        sb.AppendLine($"{indent}    // Вызываем partial method для кастомизации запроса перед отправкой");
        sb.AppendLine($"{indent}    PrepareRequestAsync(request, url, cancellationToken);");
        sb.AppendLine();
        
        // Генерируем header параметры
        GenerateHeaderParameters(sb, operation, indent + "    ");
        
        // Генерируем request body
        GenerateRequestBody(sb, operation, indent + "    ");
        
        // Генерируем отправку запроса и обработку ответа
        sb.AppendLine($"{indent}    var response = await _httpClient.SendAsync(request, cancellationToken);");
        sb.AppendLine();
        sb.AppendLine($"{indent}    // Вызываем partial method для кастомизации ответа после получения");
        sb.AppendLine($"{indent}    ProcessResponseAsync(response, cancellationToken);");
        sb.AppendLine();
        
        // Генерируем обработку ответов
        GenerateResponseHandling(sb, operation, indent + "    ");
        
        sb.AppendLine($"{indent}}}");
    }

    private void GenerateProperty(StringBuilder sb, PropertyTemplateData property, bool generateValidationAttributes, bool generateValidationConstants, string className)
    {
        // Генерируем XML комментарий для свойства
        if (!string.IsNullOrEmpty(property.Description))
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// {EscapeXmlComment(property.Description)}");
            sb.AppendLine("    /// </summary>");
        }
        
        // Генерируем атрибуты валидации
        if (generateValidationAttributes)
        {
            GenerateValidationAttributes(sb, property, generateValidationConstants, className);
        }
        
        // Генерируем объявление свойства
        var requiredModifier = property.IsRequired ? "required " : string.Empty;
        var typeString = FormatType(property.TypeInfo);
        var nullableModifier = property.IsNullable && !property.TypeInfo.IsNullable && !typeString.EndsWith("?") ? "?" : string.Empty;
        var accessorType = GetOption<ValIO.Generators.CSharp.Models.PropertyAccessorType>(property.Options, "AccessorType", ValIO.Generators.CSharp.Models.PropertyAccessorType.Set);
        var accessor = accessorType == ValIO.Generators.CSharp.Models.PropertyAccessorType.Init ? "init" : "set";
        
        sb.AppendLine($"    public {requiredModifier}{typeString}{nullableModifier} {property.Name} {{ get; {accessor}; }}");
    }

    private void GenerateValidationConstants(StringBuilder sb, IReadOnlyList<PropertyTemplateData> properties)
    {
        foreach (var property in properties)
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
                var escapedPattern = EscapeString(property.Pattern);
                sb.AppendLine($"        public const string Pattern = \"{escapedPattern}\";");
                sb.AppendLine();
                sb.AppendLine("        public static readonly Regex PatternRegex = new Regex(Pattern, RegexOptions.Compiled);");
            }
            if (property.DefaultValue != null)
            {
                var defaultValueString = FormatDefaultValue(property.DefaultValue, property.TypeInfo);
                var typeString = FormatType(property.TypeInfo);
                sb.AppendLine($"        public const {typeString} DefaultValue = {defaultValueString};");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    private void GenerateValidationAttributes(StringBuilder sb, PropertyTemplateData property, bool useConstants, string className)
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
                var escapedPattern = EscapeString(property.Pattern);
                sb.AppendLine($"    [RegularExpression(\"{escapedPattern}\")]");
            }
        }
    }

    private void GenerateParameterValidation(StringBuilder sb, OperationTemplateData operation, string indent)
    {
        foreach (var param in operation.PathParameters)
        {
            if (param.IsRequired && !param.TypeInfo.IsNonNullableValueType())
            {
                sb.AppendLine($"{indent}if ({param.VariableName} == null)");
                sb.AppendLine($"{indent}    throw new ArgumentNullException(nameof({param.VariableName}));");
            }
        }
        
        foreach (var param in operation.QueryParameters)
        {
            if (param.IsRequired && !param.TypeInfo.IsNonNullableValueType())
            {
                sb.AppendLine($"{indent}if ({param.VariableName} == null)");
                sb.AppendLine($"{indent}    throw new ArgumentNullException(nameof({param.VariableName}));");
            }
        }
        
        if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            if (operation.RequestBodyContentType == "application/json" && 
                operation.RequestBodyTypeInfo != null && 
                !operation.RequestBodyTypeInfo.IsNonNullableValueType())
            {
                sb.AppendLine($"{indent}if ({operation.RequestBodyParameter} == null)");
                sb.AppendLine($"{indent}    throw new ArgumentNullException(nameof({operation.RequestBodyParameter}));");
            }
        }
    }

    private void GeneratePathBuilding(StringBuilder sb, OperationTemplateData operation, string indent)
    {
        if (operation.PathParameters.Count > 0)
        {
            sb.AppendLine($"{indent}var path = \"{operation.Path}\";");
            foreach (var param in operation.PathParameters)
            {
                if (param.TypeInfo.IsNonNullableValueType())
                {
                    sb.AppendLine($"{indent}path = path.Replace(\"{{{param.Name}}}\", Uri.EscapeDataString({param.VariableName}.ToString()));");
                }
                else
                {
                    sb.AppendLine($"{indent}path = path.Replace(\"{{{param.Name}}}\", Uri.EscapeDataString({param.VariableName}?.ToString() ?? string.Empty));");
                }
            }
            sb.AppendLine($"{indent}var url = path;");
        }
        else
        {
            sb.AppendLine($"{indent}var url = \"{operation.Path}\";");
        }
    }

    private void GenerateQueryParameters(StringBuilder sb, OperationTemplateData operation, string indent)
    {
        if (operation.QueryParameters.Count == 0)
            return;
        
        sb.AppendLine($"{indent}var queryParams = new List<string>();");
        
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            sb.AppendLine($"{indent}if (query != null)");
            sb.AppendLine($"{indent}{{");
            foreach (var param in operation.QueryParameters)
            {
                sb.AppendLine($"{indent}    var {param.VariableName} = query.{param.PropertyName};");
                sb.AppendLine($"{indent}    if ({param.VariableName} != null)");
                sb.AppendLine($"{indent}    {{");
                GenerateQueryParameterValue(sb, param, indent + "        ");
                sb.AppendLine($"{indent}    }}");
            }
            sb.AppendLine($"{indent}}}");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                sb.AppendLine($"{indent}if ({param.VariableName} != null)");
                sb.AppendLine($"{indent}{{");
                GenerateQueryParameterValue(sb, param, indent + "    ");
                sb.AppendLine($"{indent}}}");
            }
        }
        
        sb.AppendLine($"{indent}if (queryParams.Count > 0)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    url += (url.Contains(\"?\") ? \"&\" : \"?\") + string.Join(\"&\", queryParams);");
        sb.AppendLine($"{indent}}}");
    }

    private void GenerateQueryParameterValue(StringBuilder sb, ParameterTemplateData param, string indent)
    {
        var paramNameEscaped = EscapeString(param.Name);
        
        if (param.TypeInfo.IsCollection)
        {
            var itemType = param.TypeInfo.GenericArguments.FirstOrDefault();
            var isItemNonNullableValueType = itemType?.IsNonNullableValueType() ?? false;
            sb.AppendLine($"{indent}foreach (var item in {param.VariableName})");
            sb.AppendLine($"{indent}{{");
            if (isItemNonNullableValueType)
            {
                sb.AppendLine($"{indent}    queryParams.Add($\"{paramNameEscaped}={{Uri.EscapeDataString(item.ToString())}}\");");
            }
            else
            {
                sb.AppendLine($"{indent}    if (item != null)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        queryParams.Add($\"{paramNameEscaped}={{Uri.EscapeDataString(item.ToString() ?? string.Empty)}}\");");
                sb.AppendLine($"{indent}    }}");
            }
            sb.AppendLine($"{indent}}}");
        }
        else if (param.TypeInfo.IsDictionary)
        {
            var valueType = param.TypeInfo.GenericArguments.Count > 1 ? param.TypeInfo.GenericArguments[1] : null;
            var isValueNonNullableValueType = valueType?.IsNonNullableValueType() ?? false;
            sb.AppendLine($"{indent}foreach (var kvp in {param.VariableName})");
            sb.AppendLine($"{indent}{{");
            if (isValueNonNullableValueType)
            {
                sb.AppendLine($"{indent}    queryParams.Add($\"{{Uri.EscapeDataString(kvp.Key)}}={{Uri.EscapeDataString(kvp.Value.ToString())}}\");");
            }
            else
            {
                sb.AppendLine($"{indent}    queryParams.Add($\"{{Uri.EscapeDataString(kvp.Key)}}={{Uri.EscapeDataString(kvp.Value?.ToString() ?? string.Empty)}}\");");
            }
            sb.AppendLine($"{indent}}}");
        }
        else if (!param.TypeInfo.IsPrimitive && param.TypeInfo.BaseType != "object")
        {
            sb.AppendLine($"{indent}// Complex object - serialize to JSON and add as query parameter");
            sb.AppendLine($"{indent}var jsonValue = System.Text.Json.JsonSerializer.Serialize({param.VariableName});");
            sb.AppendLine($"{indent}queryParams.Add($\"{paramNameEscaped}={{Uri.EscapeDataString(jsonValue)}}\");");
        }
        else
        {
            if (param.TypeInfo.IsNonNullableValueType())
            {
                sb.AppendLine($"{indent}queryParams.Add($\"{paramNameEscaped}={{Uri.EscapeDataString({param.VariableName}.ToString())}}\");");
            }
            else
            {
                sb.AppendLine($"{indent}queryParams.Add($\"{paramNameEscaped}={{Uri.EscapeDataString({param.VariableName}?.ToString() ?? string.Empty)}}\");");
            }
        }
    }

    private void GenerateHeaderParameters(StringBuilder sb, OperationTemplateData operation, string indent)
    {
        if (operation.HeaderParameters.Count == 0)
            return;
        
        foreach (var param in operation.HeaderParameters)
        {
            if (param.IsRequired && !param.TypeInfo.IsNonNullableValueType())
            {
                sb.AppendLine($"{indent}if ({param.VariableName} == null)");
                sb.AppendLine($"{indent}    throw new ArgumentNullException(nameof({param.VariableName}));");
            }
            if (param.TypeInfo.IsNonNullableValueType())
            {
                sb.AppendLine($"{indent}request.Headers.Add(\"{param.Name}\", {param.VariableName}.ToString());");
            }
            else
            {
                sb.AppendLine($"{indent}if ({param.VariableName} != null)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    request.Headers.Add(\"{param.Name}\", {param.VariableName}.ToString());");
                sb.AppendLine($"{indent}}}");
            }
        }
        sb.AppendLine();
    }

    private void GenerateRequestBody(StringBuilder sb, OperationTemplateData operation, string indent)
    {
        if (!operation.HasRequestBody)
            return;
        
        if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            if (operation.RequestBodyContentType == "application/x-www-form-urlencoded")
            {
                sb.AppendLine($"{indent}var formData = new Dictionary<string, string>();");
                foreach (var prop in operation.FormDataProperties)
                {
                    if (prop.IsRequired && !prop.TypeInfo.IsNonNullableValueType())
                    {
                        sb.AppendLine($"{indent}if ({prop.VariableName} == null)");
                        sb.AppendLine($"{indent}    throw new ArgumentNullException(nameof({prop.VariableName}));");
                    }
                    // Генерация form-data свойств (упрощенная версия)
                    if (prop.TypeInfo.IsCollection)
                    {
                        sb.AppendLine($"{indent}if ({prop.VariableName} != null)");
                        sb.AppendLine($"{indent}{{");
                        sb.AppendLine($"{indent}    foreach (var item in {prop.VariableName})");
                        sb.AppendLine($"{indent}    {{");
                        sb.AppendLine($"{indent}        formData.Add(\"{prop.Name}\", item?.ToString() ?? string.Empty);");
                        sb.AppendLine($"{indent}    }}");
                        sb.AppendLine($"{indent}}}");
                    }
                    else
                    {
                        if (prop.TypeInfo.IsNonNullableValueType())
                        {
                            sb.AppendLine($"{indent}formData.Add(\"{prop.Name}\", {prop.VariableName}.ToString());");
                        }
                        else
                        {
                            sb.AppendLine($"{indent}if ({prop.VariableName} != null)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    formData.Add(\"{prop.Name}\", {prop.VariableName}.ToString() ?? string.Empty);");
                            sb.AppendLine($"{indent}}}");
                        }
                    }
                }
                sb.AppendLine($"{indent}request.Content = new FormUrlEncodedContent(formData);");
            }
            else if (operation.RequestBodyContentType == "multipart/form-data")
            {
                sb.AppendLine($"{indent}var multipartContent = new MultipartFormDataContent();");
                foreach (var prop in operation.FormDataProperties)
                {
                    if (prop.IsRequired && !prop.TypeInfo.IsNonNullableValueType() && !prop.TypeInfo.IsFile)
                    {
                        sb.AppendLine($"{indent}if ({prop.VariableName} == null)");
                        sb.AppendLine($"{indent}    throw new ArgumentNullException(nameof({prop.VariableName}));");
                    }
                    if (prop.TypeInfo.IsFile)
                    {
                        if (prop.TypeInfo.IsCollection)
                        {
                            sb.AppendLine($"{indent}if ({prop.VariableName} != null)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    foreach (var file in {prop.VariableName})");
                            sb.AppendLine($"{indent}    {{");
                            sb.AppendLine($"{indent}        if (file != null)");
                            sb.AppendLine($"{indent}        {{");
                            sb.AppendLine($"{indent}            var streamContent = new StreamContent(file);");
                            sb.AppendLine($"{indent}            multipartContent.Add(streamContent, \"{prop.Name}\", \"file\");");
                            sb.AppendLine($"{indent}        }}");
                            sb.AppendLine($"{indent}    }}");
                            sb.AppendLine($"{indent}}}");
                        }
                        else
                        {
                            sb.AppendLine($"{indent}if ({prop.VariableName} != null)");
                            sb.AppendLine($"{indent}{{");
                            sb.AppendLine($"{indent}    var streamContent = new StreamContent({prop.VariableName});");
                            sb.AppendLine($"{indent}    multipartContent.Add(streamContent, \"{prop.Name}\", \"file\");");
                            sb.AppendLine($"{indent}}}");
                        }
                    }
                    // Другие типы form-data свойств (упрощенная версия)
                }
                sb.AppendLine($"{indent}request.Content = multipartContent;");
            }
        }
        else if (operation.RequestBodyContentType == "application/json")
        {
            sb.AppendLine($"{indent}var jsonContent = System.Text.Json.JsonSerializer.Serialize({operation.RequestBodyParameter}, _jsonSerializerOptions);");
            sb.AppendLine($"{indent}request.Content = new StringContent(jsonContent, Encoding.UTF8, \"application/json\");");
        }
        else
        {
            sb.AppendLine($"{indent}request.Content = new StringContent({operation.RequestBodyParameter}?.ToString() ?? string.Empty, Encoding.UTF8, \"{operation.RequestBodyContentType ?? "application/json"}\");");
        }
        sb.AppendLine();
    }

    private void GenerateResponseHandling(StringBuilder sb, OperationTemplateData operation, string indent)
    {
        sb.AppendLine($"{indent}// Обрабатываем ответы и выбрасываем исключения для ошибок");
        sb.AppendLine($"{indent}var statusCode = (int)response.StatusCode;");
        
        if (operation.Responses.Count > 0)
        {
            foreach (var responseInfo in operation.Responses)
            {
                if (!responseInfo.IsSuccess && !string.IsNullOrEmpty(responseInfo.ExceptionClassName))
                {
                    var statusCodeValue = int.TryParse(responseInfo.StatusCode, out var code) ? code : 0;
                    if (statusCodeValue > 0)
                    {
                        sb.AppendLine($"{indent}if (statusCode == {statusCodeValue})");
                        sb.AppendLine($"{indent}{{");
                        if (responseInfo.ResponseTypeInfo != null)
                        {
                            var errorResponseType = FormatType(responseInfo.ResponseTypeInfo);
                            sb.AppendLine($"{indent}    // Десериализуем ответ ошибки");
                            sb.AppendLine($"{indent}    {errorResponseType}? errorResponse = null;");
                            sb.AppendLine($"{indent}    if (response.Content != null && response.Content.Headers.ContentLength > 0)");
                            sb.AppendLine($"{indent}    {{");
                            sb.AppendLine($"{indent}        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);");
                            sb.AppendLine($"{indent}        if (!string.IsNullOrWhiteSpace(responseContent))");
                            sb.AppendLine($"{indent}        {{");
                            sb.AppendLine($"{indent}            try");
                            sb.AppendLine($"{indent}            {{");
                            sb.AppendLine($"{indent}                errorResponse = System.Text.Json.JsonSerializer.Deserialize<{errorResponseType}>(responseContent, _jsonSerializerOptions);");
                            sb.AppendLine($"{indent}            }}");
                            sb.AppendLine($"{indent}            catch (JsonException ex)");
                            sb.AppendLine($"{indent}            {{");
                            sb.AppendLine($"{indent}                throw new {responseInfo.ExceptionClassName}(response, default({errorResponseType}), ex, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                            sb.AppendLine($"{indent}            }}");
                            sb.AppendLine($"{indent}        }}");
                            sb.AppendLine($"{indent}    }}");
                            sb.AppendLine($"{indent}    throw new {responseInfo.ExceptionClassName}(response, errorResponse ?? default({errorResponseType}), null, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                        }
                        else
                        {
                            sb.AppendLine($"{indent}    throw new {responseInfo.ExceptionClassName}(response, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                        }
                        sb.AppendLine($"{indent}}}");
                    }
                }
            }
        }
        
        if (operation.HasResponseType)
        {
            var responseType = GetResponseType(operation.ReturnTypeInfo);
            if (!string.IsNullOrEmpty(responseType) && !operation.ReturnTypeInfo.IsVoid && operation.ReturnTypeInfo.BaseType != "HttpResponseMessage")
            {
                sb.AppendLine($"{indent}// Обрабатываем успешный ответ (2xx)");
                sb.AppendLine($"{indent}if (statusCode < 200 || statusCode >= 300)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    response.EnsureSuccessStatusCode();");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($"{indent}// Десериализуем успешный ответ");
                sb.AppendLine($"{indent}try");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    if (response.Content == null || response.Content.Headers.ContentLength == 0)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        return default({responseType});");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}    var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;");
                sb.AppendLine();
                sb.AppendLine($"{indent}    if (contentType.Contains(\"json\") || string.IsNullOrEmpty(contentType))");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);");
                sb.AppendLine();
                sb.AppendLine($"{indent}        if (string.IsNullOrWhiteSpace(responseContent))");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            return default({responseType});");
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}        try");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            var result = System.Text.Json.JsonSerializer.Deserialize<{responseType}>(responseContent, _jsonSerializerOptions);");
                sb.AppendLine($"{indent}            return result ?? throw new ValioHttpRequestException($\"Response deserialization returned null for type {{{responseType}}}\", response, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine($"{indent}        catch (JsonException ex)");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            var preview = responseContent.Length > 500 ? responseContent.Substring(0, 500) + \"...\" : responseContent;");
                sb.AppendLine($"{indent}            throw new ValioHttpRequestException($\"Failed to deserialize response to type {{{responseType}}}. Response preview: {{preview}}\", response, ex, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}    else");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);");
                sb.AppendLine($"{indent}        using var memoryStream = new MemoryStream();");
                sb.AppendLine($"{indent}        await responseStream.CopyToAsync(memoryStream, cancellationToken);");
                sb.AppendLine($"{indent}        memoryStream.Position = 0;");
                sb.AppendLine();
                sb.AppendLine($"{indent}        try");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            var result = await System.Text.Json.JsonSerializer.DeserializeAsync<{responseType}>(memoryStream, _jsonSerializerOptions, cancellationToken);");
                sb.AppendLine($"{indent}            return result ?? throw new ValioHttpRequestException($\"Response deserialization returned null for type {{{responseType}}}\", response, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine($"{indent}        catch (JsonException ex)");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            throw new ValioHttpRequestException($\"Failed to deserialize response stream to type {{{responseType}}}. Content-Type: {{contentType}}\", response, ex, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}catch (ValioHttpRequestException)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    throw;");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}catch (HttpRequestException ex)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    throw new ValioHttpRequestException($\"HTTP request failed with status {{response.StatusCode}} ({{response.ReasonPhrase}}). Operation: {{{operation.MethodName}}}\", response, ex, \"{operation.MethodName}\", \"{operation.HttpMethod}\", \"{operation.Path}\");");
                sb.AppendLine($"{indent}}}");
            }
            else
            {
                sb.AppendLine($"{indent}response.EnsureSuccessStatusCode();");
                sb.AppendLine($"{indent}return response;");
            }
        }
        else
        {
            sb.AppendLine($"{indent}response.EnsureSuccessStatusCode();");
            sb.AppendLine($"{indent}return response;");
        }
    }

    private string BuildParameterString(OperationTemplateData operation)
    {
        var parameters = new List<string>();
        
        // Path параметры
        foreach (var param in operation.PathParameters)
        {
            var type = FormatType(param.TypeInfo);
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
                var type = FormatType(param.TypeInfo);
                var nullable = !param.IsRequired && !param.TypeInfo.IsNonNullableValueType() ? "?" : "";
                parameters.Add($"{type}{nullable} {param.VariableName} = null");
            }
        }
        
        // Header параметры
        foreach (var param in operation.HeaderParameters)
        {
            var type = FormatType(param.TypeInfo);
            var nullable = !param.IsRequired && !param.TypeInfo.IsNonNullableValueType() ? "?" : "";
            parameters.Add($"{type}{nullable} {param.VariableName} = null");
        }
        
        // Request body
        if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            var type = operation.RequestBodyTypeInfo != null ? FormatType(operation.RequestBodyTypeInfo) : "object";
            var nullable = operation.RequestBodyTypeInfo != null && !operation.RequestBodyTypeInfo.IsNonNullableValueType() ? "?" : "";
            parameters.Add($"{type}{nullable} {operation.RequestBodyParameter} = null");
        }
        
        return string.Join(", ", parameters);
    }

    private string FormatType(TypeInfo typeInfo)
    {
        return typeInfo.FullTypeName;
    }

    private string GetResponseType(TypeInfo returnTypeInfo)
    {
        if (returnTypeInfo.BaseType == "Task" && returnTypeInfo.GenericArguments.Count > 0)
        {
            return returnTypeInfo.GenericArguments[0].FullTypeName;
        }
        return returnTypeInfo.FullTypeName;
    }

    private string FormatDefaultValue(object? value, TypeInfo typeInfo)
    {
        if (value == null)
            return "null";
        
        var baseType = typeInfo.BaseType;
        
        if (baseType == "string")
        {
            return $"\"{EscapeString(value.ToString() ?? "")}\"";
        }
        
        if (baseType == "bool")
        {
            return value.ToString()?.ToLowerInvariant() ?? "false";
        }
        
        if (baseType == "float")
        {
            return $"{value}f";
        }
        
        if (baseType == "double")
        {
            return value.ToString() ?? "0.0";
        }
        
        return value.ToString() ?? "0";
    }

    private string EscapeXmlComment(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private string EscapeString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
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

    /// <summary>
    /// Определяет ключевое слово типа модели и флаг isRecord на основе ModelFormat и Options
    /// </summary>
    private (string typeKeyword, bool isRecord) GetModelTypeInfo(ModelFormat format, Dictionary<string, object>? options)
    {
        // Проверяем Options для RecordStruct (который не может быть в ModelFormat)
        if (options != null && options.TryGetValue("ModelType", out var modelTypeValue))
        {
            // Проверяем, является ли это ModelType.RecordStruct
            var modelTypeStr = modelTypeValue.ToString();
            if (modelTypeStr?.Contains("RecordStruct") == true || modelTypeStr == "3")
            {
                return ("record struct", true);
            }
        }
        
        // Используем ModelFormat для основных типов
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
