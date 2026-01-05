using ValIO.Core.IR.Types;

namespace ValIO.Core.IR;

/// <summary>
/// Тип композиции схемы
/// </summary>
public enum IrCompositionKind
{
    /// <summary>
    /// AllOf (intersection)
    /// </summary>
    AllOf,
    
    /// <summary>
    /// OneOf (discriminated union)
    /// </summary>
    OneOf,
    
    /// <summary>
    /// AnyOf (union)
    /// </summary>
    AnyOf
}

/// <summary>
/// Композиция схемы (allOf, oneOf, anyOf)
/// </summary>
public sealed record IrComposition
{
    /// <summary>
    /// Тип композиции
    /// </summary>
    public required IrCompositionKind Kind { get; init; }
    
    /// <summary>
    /// Типы в композиции
    /// </summary>
    public required IReadOnlyList<IrType> Types { get; init; }
    
    /// <summary>
    /// Discriminator для oneOf (если есть)
    /// </summary>
    public string? DiscriminatorPropertyName { get; init; }
    
    /// <summary>
    /// Mapping значений discriminator для oneOf (если есть)
    /// </summary>
    public IReadOnlyDictionary<string, string>? DiscriminatorMapping { get; init; }
}
