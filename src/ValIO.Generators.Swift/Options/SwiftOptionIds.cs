namespace ValIO.Generators.Swift.Options;

/// <summary>
/// Normalized identifiers for Swift plugin options.
/// </summary>
public static class SwiftOptionIds
{
    public static class AnyMapping
    {
        /// <summary>Map IR <c>Any</c> to a custom <c>AnyCodable</c> wrapper (default).</summary>
        public const string AnyCodable = "anycodable";

        /// <summary>Map IR <c>Any</c> to plain <c>String</c> (fallback).</summary>
        public const string String = "string";
    }
}
