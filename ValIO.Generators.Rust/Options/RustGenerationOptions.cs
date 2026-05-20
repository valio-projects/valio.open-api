namespace ValIO.Generators.Rust.Options;

/// <summary>
/// Generation options for the Rust plugin.
/// </summary>
public sealed class RustGenerationOptions
{
    /// <summary>
    /// Crate name / root directory for generated Rust files (defaults to <c>generated</c>).
    /// </summary>
    public string CrateName { get; set; } = "generated";

    /// <summary>
    /// Strategy for mapping IR <c>Any</c> to a Rust type.
    /// Defaults to <see cref="RustOptionIds.AnyMapping.JsonValue"/>.
    /// </summary>
    public string AnyMapping { get; set; } = RustOptionIds.AnyMapping.JsonValue;
}
