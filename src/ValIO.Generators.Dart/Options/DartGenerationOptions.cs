namespace ValIO.Generators.Dart.Options;

/// <summary>
/// Generation options for the Dart Dio plugin.
/// </summary>
public sealed class DartGenerationOptions
{
    /// <summary>Top-level Dart package name (used in <c>library</c> directives if any).</summary>
    public string PackageName { get; set; } = "generated";

    /// <summary>How to map IR <c>Any</c> to a Dart type. Defaults to <c>dynamic</c>.</summary>
    public string AnyMapping { get; set; } = DartOptionIds.AnyMapping.Dynamic;
}
