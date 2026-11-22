using ValIO.Core.IR.Constraints;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Словарь
/// </summary>
public sealed class IrDictionaryType : IrType
{
    /// <summary>Creates a type for initialization from source data.</summary>
    public IrDictionaryType()
    {
    }

    /// <summary>Copies the stored type data before applying initializer overrides.</summary>
    /// <param name="source">The type to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrDictionaryType(IrDictionaryType source)
    {
        Constraints = source.Constraints;
        KeyType = source.KeyType;
        ValueType = source.ValueType;
    }

    /// <summary>
    /// Тип ключа
    /// </summary>
    public required IrType KeyType { get; init; }

    /// <summary>
    /// Тип значения
    /// </summary>
    public required IrType ValueType { get; init; }

    /// <inheritdoc />
    public override IrType WithConstraints(IrPropertyConstraints? constraints) => new IrDictionaryType(this) { Constraints = constraints };

    /// <inheritdoc />
    protected override bool EqualsType(IrType other) =>
        EqualityComparer<IrType>.Default.Equals(KeyType, ((IrDictionaryType)other).KeyType)
        && EqualityComparer<IrType>.Default.Equals(ValueType, ((IrDictionaryType)other).ValueType);

    /// <inheritdoc />
    protected override int GetTypeHashCode() => HashCode.Combine(KeyType, ValueType);
}
