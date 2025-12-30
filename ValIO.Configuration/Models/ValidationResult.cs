namespace ValIO.Configuration.Models;

/// <summary>
/// Результат валидации конфигурации
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

