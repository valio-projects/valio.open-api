namespace ValIO.Core.IR.Types;

/// <summary>
/// Represents a value that may be absent independently of JSON nullability.
/// </summary>
/// <remarks>
/// Member presence is recorded by <see cref="IrProperty.IsRequired"/>,
/// <see cref="IrParameter.IsRequired"/>, and <see cref="IrRequestBody.IsRequired"/>.
/// The OpenAPI builder does not wrap optional members in this type. Backends can
/// use it for optional values, such as a response with a documented bodyless success.
/// It does not override the containing member's presence requirement.
/// </remarks>
public sealed record IrOptionalType : IrType
{
    /// <summary>
    /// Gets the value type when a value is present.
    /// </summary>
    /// <value>The wrapped value type, retaining its own nullability.</value>
    public required IrType InnerType { get; init; }
}
