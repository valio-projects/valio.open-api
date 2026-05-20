namespace ValIO.Generators.Rust.Options;

/// <summary>
/// Normalized identifiers for Rust plugin options.
/// </summary>
public static class RustOptionIds
{
    public static class AnyMapping
    {
        /// <summary>Map IR <c>Any</c> to <c>serde_json::Value</c> (default).</summary>
        public const string JsonValue = "json-value";

        /// <summary>Map IR <c>Any</c> to plain <c>String</c>.</summary>
        public const string String = "string";
    }
}
