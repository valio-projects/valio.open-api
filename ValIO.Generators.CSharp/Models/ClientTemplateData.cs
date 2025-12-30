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
    public bool HasRequestBody { get; set; }
    public string RequestBodyParameter { get; set; } = "request";
}

