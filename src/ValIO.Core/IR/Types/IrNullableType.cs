namespace ValIO.Core.IR.Types;

/// <summary>
/// Represents a value that permits JSON null independently of member presence.
/// </summary>
/// <remarks>
/// A required member can have a nullable value. This wrapper does not make a
/// property, parameter, or request body optional; its own IsRequired flag controls omission.
/// </remarks>
public sealed record IrNullableType : IrType
{
    /// <summary>
    /// Gets the value type before adding JSON null.
    /// </summary>
    /// <value>The wrapped value type.</value>
    public required IrType InnerType { get; init; }
}
