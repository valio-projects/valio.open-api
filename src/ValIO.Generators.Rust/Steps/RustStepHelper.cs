using ValIO.Generators.Rust.Options;

namespace ValIO.Generators.Rust.Steps;

/// <summary>
/// Helpers shared by Rust pipeline steps.
/// </summary>
internal static class RustStepHelper
{
    /// <summary>Directory under the configured crate that holds generated data models.</summary>
    public static string ModelsDir(RustGenerationOptions opts) => $"{opts.CrateName}/src/models";

    /// <summary>Directory under the configured crate that holds generated API clients.</summary>
    public static string ApiDir(RustGenerationOptions opts) => $"{opts.CrateName}/src/api";
}
