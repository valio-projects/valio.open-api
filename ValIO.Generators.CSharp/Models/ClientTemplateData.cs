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
    public string ReturnType { get; set; } = string.Empty;
    public string? ResponseType { get; set; } // Тип ответа без Task<> обертки (например, "User" из "Task<User>")
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
}

/// <summary>
/// Данные для шаблона параметра
/// </summary>
public class ParameterTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string VariableName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}

