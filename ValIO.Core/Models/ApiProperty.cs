namespace ValIO.Core.Models;

/// <summary>
/// Свойство модели API
/// </summary>
public class ApiProperty
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsNullable { get; set; }
    public string? Description { get; set; }
    public object? DefaultValue { get; set; }
    
    /// <summary>
    /// Минимальное значение (для чисел)
    /// </summary>
    public decimal? Min { get; set; }
    
    /// <summary>
    /// Максимальное значение (для чисел)
    /// </summary>
    public decimal? Max { get; set; }
    
    /// <summary>
    /// Минимальная длина (для строк)
    /// </summary>
    public int? MinLength { get; set; }
    
    /// <summary>
    /// Максимальная длина (для строк)
    /// </summary>
    public int? MaxLength { get; set; }
    
    /// <summary>
    /// Исключающее минимальное значение (для чисел, > вместо >=)
    /// </summary>
    public decimal? ExclusiveMin { get; set; }
    
    /// <summary>
    /// Исключающее максимальное значение (для чисел, < вместо <=)
    /// </summary>
    public decimal? ExclusiveMax { get; set; }
    
    /// <summary>
    /// Регулярное выражение для валидации (для строк)
    /// </summary>
    public string? Pattern { get; set; }
}

