namespace ValIO.Core.Ast.Enums;

/// <summary>
/// Тип request body
/// </summary>
public enum RequestBodyKind
{
    Json,
    FormData,
    MultipartFormData,
    Raw
}
