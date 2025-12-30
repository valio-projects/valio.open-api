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
