using ValIO.Generators.FSharp.Options;

namespace ValIO.Generators.FSharp.Steps;

/// <summary>
/// Helpers shared by F# pipeline steps.
/// </summary>
internal static class FSharpStepHelper
{
    /// <summary>Relative output directory for generated data models / enums.</summary>
    public const string ModelsDir = "models";

    /// <summary>Relative output directory for generated API clients.</summary>
    public const string ApiDir = "api";

    /// <summary>
    /// Returns the fully qualified module name (e.g. <c>Generated.Models.User</c>)
    /// to emit at the top of the file.
    /// </summary>
    public static string ModelModuleName(FSharpGenerationOptions opts, string typeName) =>
        $"{opts.Namespace}.Models.{typeName}";

    /// <summary>
    /// Returns the fully qualified API client module name (e.g. <c>Generated.Api.UsersClient</c>).
    /// </summary>
    public static string ApiModuleName(FSharpGenerationOptions opts, string typeName) =>
        $"{opts.Namespace}.Api.{typeName}";
}
