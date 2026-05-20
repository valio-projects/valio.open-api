using ValIO.Generators.Ruby.Options;

namespace ValIO.Generators.Ruby.Steps;

/// <summary>
/// Helpers shared by Ruby pipeline steps.
/// </summary>
internal static class RubyStepHelper
{
    /// <summary>Directory under the configured module that holds generated data models.</summary>
    public static string ModelsDir(RubyGenerationOptions opts) => $"{opts.ModuleName}/models";

    /// <summary>Directory under the configured module that holds generated API clients.</summary>
    public static string ApiDir(RubyGenerationOptions opts) => $"{opts.ModuleName}/api";
}
