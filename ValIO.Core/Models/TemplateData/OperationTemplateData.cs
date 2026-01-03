using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Core.Models.TemplateData;

/// <summary>
/// Универсальные данные для шаблона операции (метод HTTP клиента)
/// </summary>
/// <typeparam name="TOptions">Тип опций (язык-специфичный)</typeparam>
public class OperationTemplateData<TOptions> where TOptions : OperationTemplateDataOptions, new()
{
    /// <summary>
    /// Имя метода
    /// </summary>
    public string MethodName { get; set; } = string.Empty;
    
    /// <summary>
    /// HTTP метод (GET, POST, PUT, DELETE и т.д.)
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;
    
    /// <summary>
    /// Путь операции (например, "/api/users/{id}")
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// Краткое описание операции
    /// </summary>
    public string? Summary { get; set; }
    
    /// <summary>
    /// Подробное описание операции
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Информация о типе возвращаемого значения
    /// </summary>
    public TypeInfo ReturnTypeInfo { get; set; } = null!;
    
    /// <summary>
    /// Есть ли request body
    /// </summary>
    public bool HasRequestBody { get; set; }
    
    /// <summary>
    /// Имя параметра для request body
    /// </summary>
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
    /// Есть ли тип ответа (не void, не просто Task)
    /// </summary>
    public bool HasResponseType { get; set; }
    
    /// <summary>
    /// Path параметры
    /// </summary>
    public IReadOnlyList<ParameterTemplateData> PathParameters { get; set; } = Array.Empty<ParameterTemplateData>();
    
    /// <summary>
    /// Query параметры
    /// </summary>
    public IReadOnlyList<ParameterTemplateData> QueryParameters { get; set; } = Array.Empty<ParameterTemplateData>();
    
    /// <summary>
    /// Header параметры
    /// </summary>
    public IReadOnlyList<ParameterTemplateData> HeaderParameters { get; set; } = Array.Empty<ParameterTemplateData>();
    
    /// <summary>
    /// Является ли request body form-data
    /// </summary>
    public bool IsFormData { get; set; }
    
    /// <summary>
    /// Свойства form-data для request body
    /// </summary>
    public IReadOnlyList<FormDataPropertyTemplateData>? FormDataProperties { get; set; }
    
    /// <summary>
    /// Информация о всех ответах операции (включая ошибки)
    /// </summary>
    public IReadOnlyList<ResponseTemplateData> Responses { get; set; } = Array.Empty<ResponseTemplateData>();
    
    /// <summary>
    /// Язык генерации
    /// </summary>
    public GenerationLanguage Language { get; set; }
    
    /// <summary>
    /// Список зависимостей (имена типов, которые нужно импортировать)
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Использовать модель для query параметров (вместо отдельных параметров)
    /// </summary>
    public bool UseQueryParametersModel { get; set; }
    
    /// <summary>
    /// Имя типа модели для query параметров
    /// </summary>
    public string? QueryParametersModelTypeName { get; set; }
    
    /// <summary>
    /// Путь к файлу модели для query параметров (для импорта)
    /// </summary>
    public string? QueryParametersModelImportPath { get; set; }
    
    /// <summary>
    /// Имя схемы для валидации request body (для Zod и т.д.)
    /// </summary>
    public string? RequestSchemaName { get; set; }
    
    /// <summary>
    /// Путь к файлу схемы для request body (для импорта)
    /// </summary>
    public string? RequestSchemaImportPath { get; set; }
    
    /// <summary>
    /// Имя схемы для валидации response (для Zod и т.д.)
    /// </summary>
    public string? ResponseSchemaName { get; set; }
    
    /// <summary>
    /// Путь к файлу схемы для response (для импорта)
    /// </summary>
    public string? ResponseSchemaImportPath { get; set; }
    
    /// <summary>
    /// Теги операции (для группировки)
    /// </summary>
    public IReadOnlyList<string>? Tags { get; set; }
    
    /// <summary>
    /// Дополнительные опции для генерации (язык-специфичные)
    /// </summary>
    public TOptions Options { get; set; } = new();
}

/// <summary>
/// Универсальные данные для шаблона операции (метод HTTP клиента)
/// Безтипная версия для обратной совместимости
/// </summary>
public class OperationTemplateData : OperationTemplateData<OperationTemplateDataOptions>
{
}

/// <summary>
/// Универсальные данные для шаблона параметра
/// </summary>
public class ParameterTemplateData
{
    /// <summary>
    /// Имя параметра
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Имя переменной (camelCase для TypeScript, PascalCase для C#)
    /// </summary>
    public string VariableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Имя свойства в PascalCase (для использования в моделях)
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о типе параметра
    /// </summary>
    public TypeInfo TypeInfo { get; set; } = null!;
    
    /// <summary>
    /// Является ли параметр обязательным
    /// </summary>
    public bool IsRequired { get; set; }
    
    /// <summary>
    /// Описание параметра
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Универсальные данные для шаблона свойства form-data
/// </summary>
public class FormDataPropertyTemplateData
{
    /// <summary>
    /// Имя свойства
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Имя переменной (camelCase для TypeScript, PascalCase для C#)
    /// </summary>
    public string VariableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о типе свойства
    /// </summary>
    public TypeInfo TypeInfo { get; set; } = null!;
    
    /// <summary>
    /// Является ли свойство обязательным
    /// </summary>
    public bool IsRequired { get; set; }
    
    /// <summary>
    /// Описание свойства
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Универсальные данные для шаблона ответа операции
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
    /// Информация о типе ответа (если есть модель)
    /// </summary>
    public TypeInfo? ResponseTypeInfo { get; set; }
    
    /// <summary>
    /// Является ли это успешным ответом (2xx)
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Имя класса исключения для этого статус кода (если это ошибка, для C#)
    /// </summary>
    public string? ExceptionClassName { get; set; }
}
