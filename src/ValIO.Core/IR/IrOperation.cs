using ValIO.Core.IR.Types;

namespace ValIO.Core.IR;

/// <summary>
/// IR представление HTTP операции
/// </summary>
public sealed record IrOperation
{
    /// <summary>
    /// Имя метода (sanitized, с учётом affix-преобразования, если оно применялось)
    /// </summary>
    public required string MethodName { get; init; }

    private readonly string? _originalMethodName;

    /// <summary>
    /// Исходное (до affix) имя метода. Используется для имён файлов, путей импорта и
    /// производных типов (result/params), которые не должны менять базу при добавлении
    /// method-prefix/suffix. По умолчанию равно <see cref="MethodName"/>, если affix не применялся.
    /// </summary>
    public string OriginalMethodName
    {
        get => _originalMethodName ?? MethodName;
        init => _originalMethodName = value;
    }

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

    /// <summary>
    /// Значение по умолчанию из OpenAPI (<c>parameter.schema.default</c>), или <see langword="null"/>.
    /// Используется для генерации <c>.default(...)</c> в схеме параметра.
    /// </summary>
    public object? DefaultValue { get; init; }
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

    /// <summary>
    /// True когда content type — application/json или null.
    /// </summary>
    public bool IsJson => ContentType is null or "application/json";

    /// <summary>
    /// True когда content type — multipart/form-data или application/x-www-form-urlencoded.
    /// </summary>
    public bool IsForm => ContentType is "multipart/form-data" or "application/x-www-form-urlencoded";

    /// <summary>
    /// True когда content type — multipart/form-data.
    /// </summary>
    public bool IsMultipartFormData => ContentType == "multipart/form-data";

    /// <summary>
    /// True когда content type — application/x-www-form-urlencoded.
    /// </summary>
    public bool IsFormUrlEncoded => ContentType == "application/x-www-form-urlencoded";
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
