using ValIO.Generators.Cpp.Models;

namespace ValIO.Generators.Cpp.Runtime;

/// <summary>
/// Names the artifacts shared by the standalone C++ generation steps.
/// </summary>
public static class StdRuntimeTemplates
{
    /// <summary>The application UUID provider header.</summary>
    public const string UuidTypesHeaderFileName = "Runtime/ValIOUuidTypes.h";

    /// <summary>The application calendar and clock provider header.</summary>
    public const string TemporalTypesHeaderFileName = "Runtime/ValIOTemporalTypes.h";

    /// <summary>The standalone result contract path.</summary>
    public const string ExpectedHeaderFileName = "Runtime/ValIOExpected.h";
    /// <summary>The structured error contract path.</summary>
    public const string ErrorHeaderFileName = "Runtime/ValIOError.h";
    /// <summary>The independent cancellation contract path.</summary>
    public const string CancellationHeaderFileName = "Runtime/ValIOCancellation.h";
    /// <summary>The HTTP contract path.</summary>
    public const string HttpHeaderFileName = "Runtime/ValIOHttp.h";
    /// <summary>The sole HTTP implementation translation unit path.</summary>
    public const string HttpSourceFileName = "Runtime/ValIOHttp.cpp";
    /// <summary>The checked client JSON helper path.</summary>
    public const string JsonHeaderFileName = "Runtime/ValIOJson.h";
    /// <summary>The JSON type-adapter header used by serializers without pulling in HTTP runtime code.</summary>
    public const string JsonTypesHeaderFileName = "Runtime/ValIOJsonTypes.h";
    /// <summary>The generated CMake project path.</summary>
    public const string CmakeListsFileName = "CMakeLists.txt";

    /// <summary>
    /// Emits separate model, serializer, and client targets with explicit dependency boundaries.
    /// </summary>
    /// <param name="cxxStandard">The target C++ language standard.</param>
    /// <param name="rootNamespace">The generated namespace used to name the CMake project.</param>
    /// <param name="temporalBackend">The explicit application temporal dependency.</param>
    /// <param name="uuidBackend">The explicit application UUID dependency.</param>
    /// <returns>The generated project definition.</returns>
    public static string CmakeLists(int cxxStandard, string rootNamespace,
        CppTemporalBackend temporalBackend = CppTemporalBackend.String, CppUuidBackend uuidBackend = CppUuidBackend.String)
        => CmakeProject.Render(cxxStandard, rootNamespace, temporalBackend, uuidBackend);
}
