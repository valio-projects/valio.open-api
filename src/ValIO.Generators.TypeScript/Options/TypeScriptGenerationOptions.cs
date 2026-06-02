namespace ValIO.Generators.TypeScript.Options;

public sealed class TypeScriptGenerationOptions
{
    public List<string> Transports { get; set; } = ["fetch"];
    public string EnumFormat { get; set; } = "union";
    public string? ValidationLibrary { get; set; }
    public string? QueryWrapper { get; set; }

    /// <summary>
    /// Mock library to emit alongside the client. Currently supports <c>msw</c> —
    /// generates a single <c>mocks/handlers.ts</c> file containing an
    /// <c>http.{verb}(...)</c> handler per IR operation. Normalised by
    /// <see cref="TypeScriptOptionsDescriptor"/> from the <c>mocks</c> /
    /// <c>mockLibrary</c> config key.
    /// </summary>
    public string? MockLibrary { get; set; }

    /// <summary>
    /// Suffix for the per-operation discriminated-union result type name
    /// (<c>{Operation}{ResultTypeSuffix}</c>, e.g. <c>GetUserByIdResults</c>). Only the
    /// union alias is affected — the per-status variant interfaces keep their fixed
    /// <c>Result</c> base (<c>...Result200</c>, <c>...ResultOther</c>). Defaults to
    /// <see cref="TypeScriptOptionIds.ResultSuffix.Default"/> (<c>Results</c>). Normalised by
    /// <see cref="TypeScriptOptionsDescriptor"/> from the <c>resultTypeSuffix</c> /
    /// <c>resultSuffix</c> config key.
    /// </summary>
    public string ResultTypeSuffix { get; set; } = TypeScriptOptionIds.ResultSuffix.Default;

    /// <summary>
    /// Optional custom axios instance to use instead of the global <c>axios</c> import.
    /// Parsed by <see cref="TypeScriptOptionsDescriptor"/> from the nested <c>axios</c> config
    /// object. When both path and name are set, generated axios clients import the instance and
    /// assign it to the local <c>axiosInstance</c> variable.
    /// </summary>
    public TypeScriptAxiosOptions? Axios { get; set; }

    /// <summary>
    /// How <c>Guid</c>/<c>Uuid</c> fields are validated in generated schemas:
    /// <c>guid</c> (default — built-in uuid validator), <c>string</c> (no refinement), or
    /// <c>regex</c> (shared format-regex constant). Normalised by
    /// <see cref="TypeScriptOptionsDescriptor"/> from the <c>uuidFormat</c> config key.
    /// </summary>
    public string UuidFormat { get; set; } = TypeScriptOptionIds.UuidFormat.Default;
}

/// <summary>
/// Axios-transport options. Mirrors the <c>options.axios</c> config object.
/// </summary>
public sealed class TypeScriptAxiosOptions
{
    /// <summary>
    /// Module specifier (or file path) exporting the custom axios instance,
    /// e.g. <c>@shared/api/api-mutator</c>. A trailing <c>.ts</c>/<c>.tsx</c> extension is
    /// stripped when the import is emitted.
    /// </summary>
    public string? CustomInstancePath { get; set; }

    /// <summary>
    /// Name of the exported axios instance, e.g. <c>apiMutator</c>. Imported as a named export.
    /// </summary>
    public string? CustomInstanceName { get; set; }
}
