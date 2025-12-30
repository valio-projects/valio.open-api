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
    
    /// <summary>
    /// Информация о типе request body с мета-данными
    /// </summary>
    public TypeInfo? RequestBodyTypeInfo { get; set; }
    
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
    
    /// <summary>
    /// Информация о всех ответах операции (включая ошибки)
    /// </summary>
    public IReadOnlyList<ResponseTemplateData> Responses { get; set; } = Array.Empty<ResponseTemplateData>();
    
    /// <summary>
    /// Имя класса модели для query параметров (если используется ModelClass стиль)
    /// </summary>
    public string? QueryParametersModelClassName { get; set; }
    
    /// <summary>
    /// Используется ли модель для query параметров
    /// </summary>
    public bool UseQueryParametersModel { get; set; }
}

/// <summary>
/// Данные для шаблона ответа операции
/// </summary>
public class ResponseTemplateData
{
    /// <summary>
    /// HTTP статус код (например, "200", "400", "404", "500")
    /// </summary>
    public string StatusCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Описание ответа
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Информация о типе ответа с мета-данными (если есть модель)
    /// </summary>
    public TypeInfo? ResponseTypeInfo { get; set; }
    
    /// <summary>
    /// Является ли это успешным ответом (2xx)
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Имя класса исключения для этого статус кода (если это ошибка)
    /// </summary>
    public string? ExceptionClassName { get; set; }
}

/// <summary>
/// Данные для шаблона параметра
/// </summary>
public class ParameterTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string VariableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Имя свойства в PascalCase (для использования в моделях)
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;
    
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

