namespace ValIO.Generators.TypeScript.Zod.Models;

/// <summary>
/// Результат маппинга схемы OpenAPI в Zod схему с зависимостями
/// </summary>
public class ZodSchemaMappingResult
{
    /// <summary>
    /// Zod схема (строка)
    /// </summary>
    public string ZodSchema { get; set; } = string.Empty;
    
    /// <summary>
    /// Список зависимостей - схем, которые нужно импортировать
    /// </summary>
    public HashSet<string> SchemaDependencies { get; set; } = new();
    
    /// <summary>
    /// Список зависимостей - enum схем, которые нужно импортировать
    /// </summary>
    public HashSet<string> EnumDependencies { get; set; } = new();
    
    /// <summary>
    /// Список вложенных схем, которые нужно сгенерировать отдельно
    /// </summary>
    public List<ZodNestedSchemaInfo> NestedSchemas { get; set; } = new();
}

/// <summary>
/// Информация о вложенной схеме, которую нужно сгенерировать отдельно
/// </summary>
public class ZodNestedSchemaInfo
{
    /// <summary>
    /// Имя схемы (PascalCase)
    /// </summary>
    public string SchemaName { get; set; } = string.Empty;
    
    /// <summary>
    /// Имя типа (PascalCase)
    /// </summary>
    public string TypeName { get; set; } = string.Empty;
    
    /// <summary>
    /// Описание схемы
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Схема OpenAPI
    /// </summary>
    public Microsoft.OpenApi.IOpenApiSchema Schema { get; set; } = null!;
    
    /// <summary>
    /// Свойства схемы
    /// </summary>
    public List<ZodPropertySchemaTemplateData> Properties { get; set; } = new();
}
