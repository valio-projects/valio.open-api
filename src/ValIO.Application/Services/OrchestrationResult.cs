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
public sealed class OrchestrationResult : IEquatable<OrchestrationResult>
{
    /// <summary>Initializes the values carried by this model.</summary>
    /// <param name="Success">Whether generation completed successfully.</param>
    /// <param name="Files">The generated files in pipeline order.</param>
    /// <param name="Warnings">The warnings emitted during generation.</param>
    /// <param name="ErrorMessage">The error from a failed generation, when available.</param>
    public OrchestrationResult(
        bool Success,
        IReadOnlyList<GeneratedFile> Files,
        IReadOnlyList<string> Warnings,
        string? ErrorMessage)
    {
        this.Success = Success;
        this.Files = Files;
        this.Warnings = Warnings;
        this.ErrorMessage = ErrorMessage;
    }

    /// <summary>Gets whether generation completed successfully.</summary>
    /// <value>Whether generation completed successfully.</value>
    public bool Success { get; init; }

    /// <summary>Gets the generated files in pipeline order.</summary>
    /// <value>The generated files in pipeline order.</value>
    public IReadOnlyList<GeneratedFile> Files { get; init; }

    /// <summary>Gets the warnings emitted during generation.</summary>
    /// <value>The warnings emitted during generation.</value>
    public IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Gets the error from a failed generation, when available.</summary>
    /// <value>The error from a failed generation, when available.</value>
    public string? ErrorMessage { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(OrchestrationResult? other) => other is not null
        && EqualityComparer<bool>.Default.Equals(Success, other.Success)
        && EqualityComparer<IReadOnlyList<GeneratedFile>>.Default.Equals(Files, other.Files)
        && EqualityComparer<IReadOnlyList<string>>.Default.Equals(Warnings, other.Warnings)
        && EqualityComparer<string?>.Default.Equals(ErrorMessage, other.ErrorMessage);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OrchestrationResult other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Success, Files, Warnings, ErrorMessage);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(OrchestrationResult? left, OrchestrationResult? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(OrchestrationResult? left, OrchestrationResult? right) => !Equals(left, right);

    /// <summary>Creates a failed result without generated output.</summary>
    /// <param name="message">The generation failure shown to the caller.</param>
    /// <returns>The failed generation result.</returns>
    public static OrchestrationResult Failure(string message)
        => new(false, Array.Empty<GeneratedFile>(), Array.Empty<string>(), message);
}
