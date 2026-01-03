using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Базовый интерфейс для рендеринга кода
/// </summary>
public interface ICodeRenderer
{
    /// <summary>
    /// Язык генерации, который поддерживает этот renderer
    /// </summary>
    GenerationLanguage Language { get; }
    
    /// <summary>
    /// Рендерит модель (класс, интерфейс, тип, схема валидации)
    /// </summary>
    string RenderModel(ModelTemplateData data);
    
    /// <summary>
    /// Рендерит enum (enum, union type, schema)
    /// </summary>
    string RenderEnum(EnumTemplateData data);
    
    /// <summary>
    /// Рендерит клиент (HTTP клиент)
    /// </summary>
    string RenderClient(ClientTemplateData data);
    
    /// <summary>
    /// Рендерит операцию (метод HTTP клиента)
    /// </summary>
    string RenderOperation(OperationTemplateData data);
}
