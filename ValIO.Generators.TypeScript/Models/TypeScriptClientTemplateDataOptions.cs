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
}
