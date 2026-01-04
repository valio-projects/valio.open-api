using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// TypeScript специфичные опции для ClientTemplateData
/// </summary>
public class TypeScriptClientTemplateDataOptions : ClientTemplateDataOptions
{
    /// <summary>
    /// Настройки для Axios HTTP клиента
    /// </summary>
    public AxiosOptions? AxiosOptions { get; set; }
    
    /// <summary>
    /// Сериализовать/валидировать payload через схемы (только если ValidationLibrary задан)
    /// </summary>
    public bool SerializeWithSchemas { get; set; } = true;
    
    /// <summary>
    /// Десериализовать/валидировать response через схемы (только если ValidationLibrary задан)
    /// </summary>
    public bool DeserializeWithSchemas { get; set; } = true;
}
