using ValIO.Core.Plugins;

namespace ValIO.Application.Services;

/// <summary>
/// Outcome of a generation request. <see cref="Files"/> are <i>not</i> written anywhere by the
/// orchestrator — callers materialize them via <c>IOutputSinkFactory</c>.
/// </summary>
/// <remarks>
/// Named <c>OrchestrationResult</c> (not <c>GenerationResult</c>) to avoid clashing with
/// the older <see cref="ValIO.Core.Models.GenerationResult"/> shape that some legacy code paths still use.
/// </remarks>
public sealed record OrchestrationResult(
    bool Success,
    IReadOnlyList<GeneratedFile> Files,
    IReadOnlyList<string> Warnings,
    string? ErrorMessage)
{
    public static OrchestrationResult Failure(string message)
        => new(false, Array.Empty<GeneratedFile>(), Array.Empty<string>(), message);
}
