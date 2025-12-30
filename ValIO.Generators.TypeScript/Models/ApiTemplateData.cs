namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// Данные для шаблона API TypeScript
/// </summary>
public class ApiTemplateData
{
    public IReadOnlyList<TypeScriptOperationTemplateData> Operations { get; set; } = Array.Empty<TypeScriptOperationTemplateData>();
}

/// <summary>
/// Данные для шаблона операции TypeScript
/// </summary>
public class TypeScriptOperationTemplateData
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

