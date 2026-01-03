using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Generators.CSharp.Models;

/// <summary>
/// C# специфичные опции для ModelTemplateData
/// </summary>
public class CSharpModelTemplateDataOptions : ModelTemplateDataOptions
{
    /// <summary>
    /// Тип модели: class, record или struct
    /// </summary>
    public ModelType ModelType { get; set; } = ModelType.Class;

    /// <summary>
    /// Делать классы sealed
    /// </summary>
    public bool IsSealed { get; set; } = false;

    /// <summary>
    /// Генерировать методы сравнения (Equals, GetHashCode) для record
    /// </summary>
    public bool GenerateEqualityMembers { get; set; } = true;

    /// <summary>
    /// Генерировать статические классы с константами валидации
    /// </summary>
    public bool GenerateValidationConstants { get; set; } = false;

    /// <summary>
    /// Генерировать атрибуты валидации для свойств
    /// </summary>
    public bool GenerateValidationAttributes { get; set; } = false;
}
