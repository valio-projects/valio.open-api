namespace ValIO.Core.IR.Constraints;

/// <summary>
/// Ограничения для свойства (constraints)
/// </summary>
public sealed class IrPropertyConstraints : IEquatable<IrPropertyConstraints>
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

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(IrPropertyConstraints? other) => other is not null
        && EqualityComparer<decimal?>.Default.Equals(Min, other.Min)
        && EqualityComparer<decimal?>.Default.Equals(Max, other.Max)
        && EqualityComparer<decimal?>.Default.Equals(ExclusiveMin, other.ExclusiveMin)
        && EqualityComparer<decimal?>.Default.Equals(ExclusiveMax, other.ExclusiveMax)
        && EqualityComparer<int?>.Default.Equals(MinLength, other.MinLength)
        && EqualityComparer<int?>.Default.Equals(MaxLength, other.MaxLength)
        && EqualityComparer<string?>.Default.Equals(Pattern, other.Pattern)
        && EqualityComparer<object?>.Default.Equals(DefaultValue, other.DefaultValue);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IrPropertyConstraints other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Min, Max, ExclusiveMin, ExclusiveMax, MinLength, MaxLength, Pattern, DefaultValue);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(IrPropertyConstraints? left, IrPropertyConstraints? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(IrPropertyConstraints? left, IrPropertyConstraints? right) => !Equals(left, right);
}
