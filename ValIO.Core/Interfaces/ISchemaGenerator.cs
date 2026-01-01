using ValIO.Core.Models;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для генераторов схем валидации (Zod, Yup, Valli и т.д.)
/// </summary>
public interface ISchemaGenerator
{
    /// <summary>
    /// Генерирует схему валидации для модели
    /// </summary>
    string GenerateSchema(ApiModel model, ISchemaResolver schemaResolver);

    /// <summary>
    /// Генерирует схему валидации для enum
    /// </summary>
    string GenerateEnumSchema(ApiEnum apiEnum);

    /// <summary>
    /// Поддерживаемая библиотека валидации
    /// </summary>
    ValidationLibrary ValidationLibrary { get; }
}
