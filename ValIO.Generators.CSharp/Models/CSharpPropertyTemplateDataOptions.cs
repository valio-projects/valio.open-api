using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Generators.CSharp.Models;

/// <summary>
/// C# специфичные опции для PropertyTemplateData
/// </summary>
public class CSharpPropertyTemplateDataOptions : PropertyTemplateDataOptions
{
    /// <summary>
    /// Тип аксессора свойства: Set или Init
    /// </summary>
    public PropertyAccessorType AccessorType { get; set; } = PropertyAccessorType.Set;
}
