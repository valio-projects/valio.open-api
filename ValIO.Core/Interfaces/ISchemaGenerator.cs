using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для генераторов схем валидации (Zod, Yup, Valli и т.д.)
/// </summary>
/// <remarks>
/// Устаревший интерфейс. Используйте IModelFormatRenderer и IEnumFormatRenderer вместо этого.
/// </remarks>
[Obsolete("Use IModelFormatRenderer and IEnumFormatRenderer instead. This interface will be removed in a future version.")]
public interface ISchemaGenerator
{
    /// <summary>
    /// Генерирует схему валидации для модели
    /// </summary>
    [Obsolete("Use IModelFormatRenderer.Render(ModelTemplateData) instead.")]
    string GenerateSchema(ApiModel model, ISchemaResolver schemaResolver);

    /// <summary>
    /// Генерирует схему валидации для enum
    /// </summary>
    [Obsolete("Use IEnumFormatRenderer.Render(EnumTemplateData) instead.")]
    string GenerateEnumSchema(ApiEnum apiEnum);

    /// <summary>
    /// Поддерживаемая библиотека валидации
    /// </summary>
    ValidationLibrary ValidationLibrary { get; }
}
