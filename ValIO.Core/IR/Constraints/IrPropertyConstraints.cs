namespace ValIO.Core.IR.Constraints;

/// <summary>
/// Ограничения для свойства (constraints)
/// </summary>
public sealed record IrPropertyConstraints
{
    /// <summary>
    /// Минимальное значение (для чисел)
    /// </summary>
    public decimal? Min { get; init; }
    
    /// <summary>
    /// Максимальное значение (для чисел)
    /// </summary>
    public decimal? Max { get; init; }
    
    /// <summary>
    /// Исключающее минимальное значение (для чисел, > вместо >=)
    /// </summary>
    public decimal? ExclusiveMin { get; init; }
    
    /// <summary>
    /// Исключающее максимальное значение (для чисел, < вместо <=)
    /// </summary>
    public decimal? ExclusiveMax { get; init; }
    
    /// <summary>
    /// Минимальная длина (для строк)
    /// </summary>
    public int? MinLength { get; init; }
    
    /// <summary>
    /// Максимальная длина (для строк)
    /// </summary>
    public int? MaxLength { get; init; }
    
    /// <summary>
    /// Регулярное выражение для валидации (для строк)
    /// </summary>
    public string? Pattern { get; init; }
    
    /// <summary>
    /// Значение по умолчанию
    /// </summary>
    public object? DefaultValue { get; init; }
}
