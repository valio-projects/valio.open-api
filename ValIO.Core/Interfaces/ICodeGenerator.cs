using Microsoft.OpenApi.Models;
using ValIO.Core.Models;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Базовый интерфейс для всех генераторов кода
/// </summary>
public interface ICodeGenerator
{
    Task<GenerationResult> GenerateAsync(OpenApiDocument document, GenerationConfiguration config);
    bool SupportsLanguage(GenerationLanguage language);
    bool SupportsOptions(GenerationOptions? options);
}

