namespace ValIO.Core.IR;

/// <summary>
/// Метаданные IR документа
/// </summary>
public sealed record IrDocumentMetadata
{
    /// <summary>
    /// Название API
    /// </summary>
    public string? Title { get; init; }
    
    /// <summary>
    /// Версия API
    /// </summary>
    public string? Version { get; init; }
    
    /// <summary>
    /// Описание API
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Базовый URL сервера
    /// </summary>
    public string? ServerUrl { get; init; }
}
