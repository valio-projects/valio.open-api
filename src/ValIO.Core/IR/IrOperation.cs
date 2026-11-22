using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Core.IR;

/// <summary>
/// IR представление HTTP операции
/// </summary>
public sealed class IrOperation : IEquatable<IrOperation>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrOperation()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrOperation(IrOperation source)
    {
        MethodName = source.MethodName;
        _originalMethodName = source._originalMethodName;
        HttpMethod = source.HttpMethod;
        Path = source.Path;
        Summary = source.Summary;
        Description = source.Description;
        PathParameters = source.PathParameters;
        QueryParameters = source.QueryParameters;
        HeaderParameters = source.HeaderParameters;
        RequestBody = source.RequestBody;
        Responses = source.Responses;
        Tags = source.Tags;
    }

    /// <summary>
    /// Имя метода (sanitized, с учётом affix-преобразования, если оно применялось)
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>Preserves the explicit original name before applying a fallback.</summary>
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

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrOperation? other) => other is not null
        && EqualityComparer<string>.Default.Equals(MethodName, other.MethodName)
        && EqualityComparer<string?>.Default.Equals(_originalMethodName, other._originalMethodName)
        && EqualityComparer<string>.Default.Equals(HttpMethod, other.HttpMethod)
        && EqualityComparer<string>.Default.Equals(Path, other.Path)
        && EqualityComparer<string?>.Default.Equals(Summary, other.Summary)
        && EqualityComparer<string?>.Default.Equals(Description, other.Description)
        && EqualityComparer<IReadOnlyList<IrParameter>>.Default.Equals(PathParameters, other.PathParameters)
        && EqualityComparer<IReadOnlyList<IrParameter>>.Default.Equals(QueryParameters, other.QueryParameters)
        && EqualityComparer<IReadOnlyList<IrParameter>>.Default.Equals(HeaderParameters, other.HeaderParameters)
        && EqualityComparer<IrRequestBody?>.Default.Equals(RequestBody, other.RequestBody)
        && EqualityComparer<IReadOnlyList<IrResponse>>.Default.Equals(Responses, other.Responses)
        && EqualityComparer<IReadOnlyList<string>?>.Default.Equals(Tags, other.Tags);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrOperation other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MethodName);
        hash.Add(_originalMethodName);
        hash.Add(HttpMethod);
        hash.Add(Path);
        hash.Add(Summary);
        hash.Add(Description);
        hash.Add(PathParameters);
        hash.Add(QueryParameters);
        hash.Add(HeaderParameters);
        hash.Add(RequestBody);
        hash.Add(Responses);
        hash.Add(Tags);
        return hash.ToHashCode();
    }

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrOperation? left, IrOperation? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrOperation? left, IrOperation? right) => !Equals(left, right);
}

/// <summary>
/// Represents an operation parameter, preserving source identity, validation, and HTTP serialization metadata.
/// </summary>
public sealed class IrParameter : IEquatable<IrParameter>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrParameter()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrParameter(IrParameter source)
    {
        Name = source.Name;
        _originalName = source._originalName;
        Type = source.Type;
        IsRequired = source.IsRequired;
        Description = source.Description;
        DefaultValue = source.DefaultValue;
        Constraints = source.Constraints;
        Style = source.Style;
        Explode = source.Explode;
    }

    /// <summary>
    /// Gets the sanitized identifier used by generated source declarations.
    /// </summary>
    /// <value>The language-neutral parameter identifier.</value>
    public required string Name { get; init; }

    /// <summary>Stores the original wire name when identifier sanitization changes it.</summary>
    private readonly string? _originalName;

    /// <summary>
    /// Gets the original HTTP query, path, or header name before source identifier sanitization.
    /// </summary>
    /// <value>The original wire name, falling back to <see cref="Name"/> for manually constructed IR.</value>
    public string OriginalName
    {
        get => _originalName ?? Name;
        init => _originalName = value;
    }

    /// <summary>
    /// Gets the language-independent type of the parameter value.
    /// </summary>
    /// <value>The parameter type, including its collection and nullability semantics.</value>
    public required IrType Type { get; init; }
    
    /// <summary>
    /// Gets whether the HTTP operation requires the parameter to be present.
    /// </summary>
    /// <value>Whether omission violates the source contract.</value>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// Gets the parameter description, falling back to its schema description when necessary.
    /// </summary>
    /// <value>The source documentation, or <see langword="null"/> when none is provided.</value>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the source schema default used by generated validators or optional parameter handling.
    /// </summary>
    /// <value>The decoded default value, or <see langword="null"/> when absent.</value>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Validation constraints from the parameter's schema (string lengths, numeric ranges, regex pattern), or
    /// <see langword="null"/> when the schema declares none. Emitted as an exported constants object in the
    /// generated params interface file and referenced by the params schema — mirroring how model-property
    /// constraints (<see cref="IrProperty.Constraints"/>) are handled.
    /// </summary>
    /// <value>The extracted constraints, or <see langword="null"/> when the parameter has none.</value>
    /// <seealso cref="IrPropertyConstraints"/>
    public IrPropertyConstraints? Constraints { get; init; }

    /// <summary>Gets the normalized HTTP parameter serialization style supplied by the source reader.</summary>
    /// <remarks>
    /// The OpenAPI builder always sets this value. For manually constructed legacy IR, generators resolve
    /// an absent value from the parameter location: form for query parameters and simple for path or header parameters.
    /// </remarks>
    /// <value>The serialization style, or <see langword="null"/> when no metadata was supplied.</value>
    public IrParameterStyle? Style { get; init; }

    /// <summary>Gets whether array elements or object properties expand into separate parameter entries.</summary>
    /// <remarks>
    /// The OpenAPI builder preserves the reader's effective value, including source defaults.
    /// For manually constructed legacy IR, the default is true for form or cookie style and false for other styles.
    /// </remarks>
    /// <value>The normalized explode flag, or <see langword="null"/> when no metadata was supplied.</value>
    public bool? Explode { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrParameter? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<string?>.Default.Equals(_originalName, other._originalName)
        && EqualityComparer<IrType>.Default.Equals(Type, other.Type)
        && EqualityComparer<bool>.Default.Equals(IsRequired, other.IsRequired)
        && EqualityComparer<string?>.Default.Equals(Description, other.Description)
        && EqualityComparer<object?>.Default.Equals(DefaultValue, other.DefaultValue)
        && EqualityComparer<IrPropertyConstraints?>.Default.Equals(Constraints, other.Constraints)
        && EqualityComparer<IrParameterStyle?>.Default.Equals(Style, other.Style)
        && EqualityComparer<bool?>.Default.Equals(Explode, other.Explode);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrParameter other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(_originalName);
        hash.Add(Type);
        hash.Add(IsRequired);
        hash.Add(Description);
        hash.Add(DefaultValue);
        hash.Add(Constraints);
        hash.Add(Style);
        hash.Add(Explode);
        return hash.ToHashCode();
    }

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrParameter? left, IrParameter? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrParameter? left, IrParameter? right) => !Equals(left, right);
}

/// <summary>
/// IR представление request body
/// </summary>
public sealed class IrRequestBody : IEquatable<IrRequestBody>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrRequestBody()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrRequestBody(IrRequestBody source)
    {
        Type = source.Type;
        IsRequired = source.IsRequired;
        ContentType = source.ContentType;
        Description = source.Description;
    }

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

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrRequestBody? other) => other is not null
        && EqualityComparer<IrType>.Default.Equals(Type, other.Type)
        && EqualityComparer<bool>.Default.Equals(IsRequired, other.IsRequired)
        && EqualityComparer<string?>.Default.Equals(ContentType, other.ContentType)
        && EqualityComparer<string?>.Default.Equals(Description, other.Description);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrRequestBody other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Type, IsRequired, ContentType, Description);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrRequestBody? left, IrRequestBody? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrRequestBody? left, IrRequestBody? right) => !Equals(left, right);
}

/// <summary>
/// IR представление ответа операции
/// </summary>
public sealed class IrResponse : IEquatable<IrResponse>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrResponse()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrResponse(IrResponse source)
    {
        StatusCode = source.StatusCode;
        Type = source.Type;
        ContentTypes = source.ContentTypes;
        MediaTypes = source.MediaTypes;
        Headers = source.Headers;
        Description = source.Description;
    }

    /// <summary>
    /// HTTP статус код (200, 400, 404, "default" и т.д.)
    /// </summary>
    public required string StatusCode { get; init; }
    
    /// <summary>Gets the payload type of the first declared media type for existing generators.</summary>
    /// <value>The selected payload type, or <see langword="null"/> when it has no schema.</value>
    public IrType? Type { get; init; }

    /// <summary>Gets every media type declared for this response status in document order.</summary>
    /// <value>The declared response content keys, or an empty list for a bodyless response.</value>
    public IReadOnlyList<string> ContentTypes { get; init; } = [];

    /// <summary>Gets the response representations and their independently translated payload schemas.</summary>
    /// <value>The representations keyed by their original media names in document order.</value>
    public IReadOnlyList<IrResponseMediaType> MediaTypes { get; init; } = [];

    /// <summary>Gets the HTTP headers declared for this response status.</summary>
    /// <value>The wire declarations, excluding the Content-Type header described by <see cref="ContentTypes"/>.</value>
    public IReadOnlyList<IrResponseHeader> Headers { get; init; } = [];
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrResponse? other) => other is not null
        && EqualityComparer<string>.Default.Equals(StatusCode, other.StatusCode)
        && EqualityComparer<IrType?>.Default.Equals(Type, other.Type)
        && EqualityComparer<IReadOnlyList<string>>.Default.Equals(ContentTypes, other.ContentTypes)
        && EqualityComparer<IReadOnlyList<IrResponseMediaType>>.Default.Equals(MediaTypes, other.MediaTypes)
        && EqualityComparer<IReadOnlyList<IrResponseHeader>>.Default.Equals(Headers, other.Headers)
        && EqualityComparer<string?>.Default.Equals(Description, other.Description);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrResponse other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(StatusCode, Type, ContentTypes, MediaTypes, Headers, Description);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrResponse? left, IrResponse? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrResponse? left, IrResponse? right) => !Equals(left, right);
}

/// <summary>Describes a response body for one OpenAPI media type.</summary>
public sealed class IrResponseMediaType : IEquatable<IrResponseMediaType>
{
    /// <summary>Creates a language-independent value for initialization from source data.</summary>
    public IrResponseMediaType()
    {
    }

    /// <summary>Copies all stored members before applying initializer overrides.</summary>
    /// <param name="source">The value to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrResponseMediaType(IrResponseMediaType source)
    {
        Name = source.Name;
        Type = source.Type;
    }

    /// <summary>Gets the original media type name.</summary>
    /// <value>The key declared in the OpenAPI response content map.</value>
    public required string Name { get; init; }

    /// <summary>Gets the schema for this response representation.</summary>
    /// <value>The translated payload type, if the source declares a schema.</value>
    public IrType? Type { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrResponseMediaType? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<IrType?>.Default.Equals(Type, other.Type);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrResponseMediaType other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, Type);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrResponseMediaType? left, IrResponseMediaType? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrResponseMediaType? left, IrResponseMediaType? right) => !Equals(left, right);
}

/// <summary>Describes a declared HTTP response header independently of its runtime presence.</summary>
public sealed class IrResponseHeader : IEquatable<IrResponseHeader>
{
    /// <summary>Gets the original header name from the OpenAPI document.</summary>
    /// <value>The case-preserving wire name.</value>
    public required string Name { get; init; }

    /// <summary>Gets the source description of the header.</summary>
    /// <value>The header documentation, when supplied.</value>
    public string? Description { get; init; }

    /// <summary>Gets whether the source contract declares the header required.</summary>
    /// <value>True when an implementing server must send the header.</value>
    public bool IsRequired { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrResponseHeader? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<string?>.Default.Equals(Description, other.Description)
        && EqualityComparer<bool>.Default.Equals(IsRequired, other.IsRequired);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrResponseHeader other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, Description, IsRequired);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrResponseHeader? left, IrResponseHeader? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrResponseHeader? left, IrResponseHeader? right) => !Equals(left, right);
}
