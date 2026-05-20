namespace ValIO.Core.Models;

/// <summary>
/// Результат генерации кода
/// </summary>
public class GenerationResult
{
    public bool Success { get; set; }
    public IReadOnlyDictionary<string, string> GeneratedFiles { get; set; } = new Dictionary<string, string>();
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

