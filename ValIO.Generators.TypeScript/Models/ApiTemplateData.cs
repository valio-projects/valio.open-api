using ValIO.Core.Models;
using ValIO.Generators.CSharp.Models;

namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// Данные для шаблона клиента TypeScript
/// </summary>
public class TypeScriptClientTemplateData
{
    public string ClientName { get; set; } = string.Empty;
    public IReadOnlyList<TypeScriptOperationTemplateData> Operations { get; set; } = Array.Empty<TypeScriptOperationTemplateData>();
    
    /// <summary>
    /// HTTP клиент: Fetch или Axios
    /// </summary>
    public HttpClientLibrary HttpClientLibrary { get; set; } = HttpClientLibrary.Fetch;
    
    /// <summary>
    /// Список имен типов, которые нужно импортировать (PascalCase)
    /// </summary>
    public IReadOnlyList<string> ImportedTypes { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Путь к папке с моделями для импортов
    /// </summary>
    public string? ModelsPath { get; set; }
    
    /// <summary>
    /// Настройки для Axios HTTP клиента
    /// </summary>
    public AxiosOptions? AxiosOptions { get; set; }
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
    public string? Description { get; set; }
    
    /// <summary>
    /// Параметры метода как строка (для простых случаев)
    /// </summary>
    public string Parameters { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о типе возвращаемого значения
    /// </summary>
    public TypeInfo ReturnTypeInfo { get; set; } = null!;
    
    /// <summary>
    /// Строковое представление типа возврата (для обратной совместимости)
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;
    
    public bool HasRequestBody { get; set; }
    public string RequestBodyParameter { get; set; } = "request";
    
    /// <summary>
    /// Информация о типе request body
    /// </summary>
    public TypeInfo? RequestBodyTypeInfo { get; set; }
    
    /// <summary>
    /// Content type для request body
    /// </summary>
    public string? RequestBodyContentType { get; set; }
    
    /// <summary>
    /// Path параметры
    /// </summary>
    public IReadOnlyList<TypeScriptParameterTemplateData> PathParameters { get; set; } = Array.Empty<TypeScriptParameterTemplateData>();
    
    /// <summary>
    /// Query параметры
    /// </summary>
    public IReadOnlyList<TypeScriptParameterTemplateData> QueryParameters { get; set; } = Array.Empty<TypeScriptParameterTemplateData>();
    
    /// <summary>
    /// Header параметры
    /// </summary>
    public IReadOnlyList<TypeScriptParameterTemplateData> HeaderParameters { get; set; } = Array.Empty<TypeScriptParameterTemplateData>();
    
    /// <summary>
    /// Является ли request body form-data
    /// </summary>
    public bool IsFormData { get; set; }
    
    /// <summary>
    /// Свойства form-data
    /// </summary>
    public IReadOnlyList<TypeScriptFormDataPropertyTemplateData>? FormDataProperties { get; set; }
    
    /// <summary>
    /// Имя схемы для валидации request body (например, "CreateUserSchema")
    /// </summary>
    public string? RequestSchemaName { get; set; }
    
    /// <summary>
    /// Имя файла схемы для request body (для импорта, например, "createUser")
    /// </summary>
    public string? RequestSchemaFileName { get; set; }
    
    /// <summary>
    /// Имя схемы для валидации response (например, "UserSchema")
    /// </summary>
    public string? ResponseSchemaName { get; set; }
    
    /// <summary>
    /// Имя файла схемы для response (для импорта, например, "user")
    /// </summary>
    public string? ResponseSchemaFileName { get; set; }
    
    /// <summary>
    /// Использовать модель для query параметров (вместо отдельных параметров)
    /// </summary>
    public bool UseQueryParametersModel { get; set; }
    
    /// <summary>
    /// Имя типа модели для query параметров (например, "SearchChessboardProjectsListParams")
    /// </summary>
    public string? QueryParametersModelTypeName { get; set; }
    
    /// <summary>
    /// Имя файла модели для query параметров (для импорта, например, "searchChessboardProjectsListParams")
    /// </summary>
    public string? QueryParametersModelFileName { get; set; }
}

/// <summary>
/// Данные для шаблона параметра TypeScript
/// </summary>
public class TypeScriptParameterTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string VariableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о типе параметра
    /// </summary>
    public TypeInfo TypeInfo { get; set; } = null!;
    
    /// <summary>
    /// Строковое представление типа (для обратной совместимости)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Данные для шаблона свойства form-data TypeScript
/// </summary>
public class TypeScriptFormDataPropertyTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string VariableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о типе свойства
    /// </summary>
    public TypeInfo TypeInfo { get; set; } = null!;
    
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}

