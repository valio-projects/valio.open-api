namespace ValIO.Generators.TypeScript.Zod.Models;

/// <summary>
/// Данные для шаблона Zod схемы модели
/// </summary>
public class ZodSchemaTemplateData
{
    public string SchemaName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<ZodPropertySchemaTemplateData> Properties { get; set; } = Array.Empty<ZodPropertySchemaTemplateData>();
    
    /// <summary>
    /// Список зависимостей - схем, которые нужно импортировать
    /// </summary>
    public IReadOnlyList<string> SchemaDependencies { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Список зависимостей - enum схем, которые нужно импортировать
    /// </summary>
    public IReadOnlyList<string> EnumDependencies { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Путь к папке с моделями для импортов (относительный путь)
    /// </summary>
    public string? ModelsPath { get; set; }
    
    /// <summary>
    /// Функция для преобразования имени схемы в имя файла (camelCase)
    /// </summary>
    public string SchemaNameToFileName(string schemaName)
    {
        var typeName = schemaName.Replace("Schema", "");
        if (string.IsNullOrEmpty(typeName))
            return schemaName.ToLowerInvariant();
        
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < typeName.Length; i++)
        {
            if (i == 0)
            {
                result.Append(char.ToLowerInvariant(typeName[i]));
            }
            else if (char.IsUpper(typeName[i]))
            {
                result.Append(char.ToLowerInvariant(typeName[i]));
            }
            else
            {
                result.Append(typeName[i]);
            }
        }
        return result.ToString();
    }
}

/// <summary>
/// Данные для шаблона Zod схемы свойства
/// </summary>
public class ZodPropertySchemaTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string ZodSchema { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Данные для шаблона Zod схемы enum
/// </summary>
public class ZodEnumSchemaTemplateData
{
    public string SchemaName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<string> EnumValues { get; set; } = Array.Empty<string>();
}
