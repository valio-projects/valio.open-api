using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Represents a scalar schema type and its retained format annotation.
/// </summary>
public sealed class IrPrimitiveType : IrType
{
    /// <summary>Creates a type for initialization from source data.</summary>
    public IrPrimitiveType()
    {
    }

    /// <summary>Copies the stored type data before applying initializer overrides.</summary>
    /// <param name="source">The type to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrPrimitiveType(IrPrimitiveType source)
    {
        Constraints = source.Constraints;
        PrimitiveKind = source.PrimitiveKind;
        Format = source.Format;
    }

    /// <summary>
    /// Gets the scalar representation selected independently of language-specific backends.
    /// </summary>
    /// <value>The scalar IR kind used by generator type mappers.</value>
    public required IrPrimitiveKind PrimitiveKind { get; init; }

    /// <summary>Gets the schema's scalar format without replacing its primitive representation.</summary>
    /// <value>The normalized known format, verbatim custom format, or null when no format is present.</value>
    /// <remarks>
    /// Known formats are trimmed and lowercased by the IR producer. Custom format identifiers retain
    /// their exact spelling so case-sensitive validator contracts remain distinct.
    /// </remarks>
    public string? Format { get; init; }

    /// <inheritdoc />
    public override IrType WithConstraints(IrPropertyConstraints? constraints) => new IrPrimitiveType(this) { Constraints = constraints };

    /// <inheritdoc />
    protected override bool EqualsType(IrType other) =>
        EqualityComparer<IrPrimitiveKind>.Default.Equals(PrimitiveKind, ((IrPrimitiveType)other).PrimitiveKind)
        && EqualityComparer<string?>.Default.Equals(Format, ((IrPrimitiveType)other).Format);

    /// <inheritdoc />
    protected override int GetTypeHashCode() => HashCode.Combine(PrimitiveKind, Format);
}
