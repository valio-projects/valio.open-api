using ValIO.Generators.Swift.Options;

namespace ValIO.Generators.Swift.Steps;

/// <summary>
/// Helpers shared by Swift pipeline steps.
/// </summary>
internal static class SwiftStepHelper
{
    /// <summary>Directory under the configured module that holds generated data models.</summary>
    public static string ModelsDir(SwiftGenerationOptions opts) => $"{opts.ModuleName}/Models";

    /// <summary>Directory under the configured module that holds generated API clients.</summary>
    public static string ApiDir(SwiftGenerationOptions opts) => $"{opts.ModuleName}/Api";
}
