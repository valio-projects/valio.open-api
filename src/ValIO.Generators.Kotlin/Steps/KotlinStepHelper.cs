using ValIO.Generators.Kotlin.Options;

namespace ValIO.Generators.Kotlin.Steps;

/// <summary>
/// Helpers shared by Kotlin pipeline steps.
/// </summary>
internal static class KotlinStepHelper
{
    /// <summary>Returns the package for generated models given the root package.</summary>
    public static string ModelsPackage(KotlinGenerationOptions opts) => $"{opts.Package}.models";

    /// <summary>Returns the package for generated API/client classes given the root package.</summary>
    public static string ApiPackage(KotlinGenerationOptions opts) => $"{opts.Package}.api";

    /// <summary>Converts a Kotlin package (dot-separated) into a directory path (slash-separated).</summary>
    public static string PackageToPath(string package_) =>
        package_.Replace('.', '/');
}
