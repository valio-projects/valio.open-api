using ValIO.Core.Models;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.TanStackQuery.Models;

/// <summary>
/// Данные для шаблона TanStack Query хука
/// </summary>
public class TanStackQueryHookTemplateData
{
    public string HookName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string QueryKeyFunctionName { get; set; } = string.Empty;
    public ApiOperation Operation { get; set; } = null!;
    public TypeScriptOperationTemplateData OperationTemplate { get; set; } = null!;
    public bool IsQuery { get; set; }
    public bool IsMutation { get; set; }
    public TypeScriptGenerationOptions Options { get; set; } = null!;
    
    /// <summary>
    /// Имя схемы для валидации response (PascalCase)
    /// </summary>
    public string? ResponseSchemaName { get; set; }
    
    /// <summary>
    /// Имя файла схемы для валидации response (camelCase)
    /// </summary>
    public string? ResponseSchemaFileName { get; set; }
    
    /// <summary>
    /// Имя схемы для валидации request body (PascalCase)
    /// </summary>
    public string? RequestSchemaName { get; set; }
    
    /// <summary>
    /// Имя файла схемы для валидации request body (camelCase)
    /// </summary>
    public string? RequestSchemaFileName { get; set; }
    
    /// <summary>
    /// Список имен типов, которые нужно импортировать (PascalCase)
    /// </summary>
    public IReadOnlyList<string> ImportedTypes { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Путь к папке с моделями для импортов
    /// </summary>
    public string? ModelsPath { get; set; }
    
    /// <summary>
    /// Имя метода из базового клиента (axios/fetch), который нужно импортировать и использовать
    /// </summary>
    public string? ClientMethodName { get; set; }
    
    /// <summary>
    /// Путь для импорта базового клиента (например, "./buildings.axios" или "./buildings.fetch")
    /// </summary>
    public string? ClientImportPath { get; set; }
}
