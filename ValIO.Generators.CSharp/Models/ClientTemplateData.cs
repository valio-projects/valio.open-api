namespace ValIO.Generators.CSharp.Models;

/// <summary>
/// Данные для шаблона клиента C#
/// </summary>
public class ClientTemplateData
{
    public string Namespace { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public IReadOnlyList<OperationTemplateData> Operations { get; set; } = Array.Empty<OperationTemplateData>();
}

/// <summary>
/// Данные для шаблона операции
/// </summary>
public class OperationTemplateData
{
    public string MethodName { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Parameters { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о типе возвращаемого значения с мета-данными
    /// </summary>
    public TypeInfo ReturnTypeInfo { get; set; } = null!;
    
    public bool HasRequestBody { get; set; }
    public string RequestBodyParameter { get; set; } = "request";
    public string? RequestBodyContentType { get; set; }
    public bool HasResponseType { get; set; }
    public bool HasPathParameters { get; set; }
    public bool HasQueryParameters { get; set; }
    public bool HasHeaderParameters { get; set; }
    public IReadOnlyList<ParameterTemplateData> PathParameters { get; set; } = Array.Empty<ParameterTemplateData>();
    public IReadOnlyList<ParameterTemplateData> QueryParameters { get; set; } = Array.Empty<ParameterTemplateData>();
    public IReadOnlyList<ParameterTemplateData> HeaderParameters { get; set; } = Array.Empty<ParameterTemplateData>();
    
    /// <summary>
    /// Свойства form-data для request body
    /// </summary>
    public IReadOnlyList<FormDataPropertyTemplateData>? FormDataProperties { get; set; }
    
    /// <summary>
    /// Является ли request body form-data
    /// </summary>
    public bool IsFormData { get; set; }
}

/// <summary>
/// Данные для шаблона параметра
/// </summary>
public class ParameterTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string VariableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о типе параметра с мета-данными
    /// </summary>
    public TypeInfo TypeInfo { get; set; } = null!;
    
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Данные для шаблона свойства form-data
/// </summary>
public class FormDataPropertyTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string VariableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о типе свойства с мета-данными
    /// </summary>
    public TypeInfo TypeInfo { get; set; } = null!;
    
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}

