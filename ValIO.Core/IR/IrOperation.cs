using ValIO.Core.IR.Types;

namespace ValIO.Core.IR;

/// <summary>
/// IR представление HTTP операции
/// </summary>
public sealed record IrOperation
{
    /// <summary>
    /// Имя метода (sanitized)
    /// </summary>
    public required string MethodName { get; init; }
    
    /// <summary>
    /// HTTP метод (GET, POST, PUT, DELETE и т.д.)
    /// </summary>
    public required string HttpMethod { get; init; }
    
    /// <summary>
    /// Путь операции (например, "/api/users/{id}")
    /// </summary>
    public required string Path { get; init; }
    
    /// <summary>
    /// Краткое описание операции
    /// </summary>
    public string? Summary { get; init; }
    
    /// <summary>
    /// Подробное описание операции
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Path параметры
    /// </summary>
    public required IReadOnlyList<IrParameter> PathParameters { get; init; }
    
    /// <summary>
    /// Query параметры
    /// </summary>
    public required IReadOnlyList<IrParameter> QueryParameters { get; init; }
    
    /// <summary>
    /// Header параметры
    /// </summary>
    public required IReadOnlyList<IrParameter> HeaderParameters { get; init; }
    
    /// <summary>
    /// Request body
    /// </summary>
    public IrRequestBody? RequestBody { get; init; }
    
    /// <summary>
    /// Ответы операции (включая ошибки)
    /// </summary>
    public required IReadOnlyList<IrResponse> Responses { get; init; }
    
    /// <summary>
    /// Теги операции (для группировки)
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>
/// IR представление параметра операции
/// </summary>
public sealed record IrParameter
{
    /// <summary>
    /// Имя параметра (sanitized)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Тип параметра
    /// </summary>
    public required IrType Type { get; init; }
    
    /// <summary>
    /// Является ли параметр обязательным
    /// </summary>
    public required bool IsRequired { get; init; }
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// IR представление request body
/// </summary>
public sealed record IrRequestBody
{
    /// <summary>
    /// Тип request body
    /// </summary>
    public required IrType Type { get; init; }
    
    /// <summary>
    /// Является ли request body обязательным
    /// </summary>
    public required bool IsRequired { get; init; }
    
    /// <summary>
    /// Content type (application/json, application/x-www-form-urlencoded, multipart/form-data)
    /// </summary>
    public string? ContentType { get; init; }
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// IR представление ответа операции
/// </summary>
public sealed record IrResponse
{
    /// <summary>
    /// HTTP статус код (200, 400, 404, "default" и т.д.)
    /// </summary>
    public required string StatusCode { get; init; }
    
    /// <summary>
    /// Тип ответа (если есть)
    /// </summary>
    public IrType? Type { get; init; }
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; init; }
}
