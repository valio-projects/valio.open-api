using ValIO.Core.IR.Constraints;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>Compares validation rules independently from initialization defaults.</summary>
internal static class ValidationConstraints
{
    /// <summary>Determines whether two value constraints impose the same validation rules.</summary>
    /// <param name="first">The first set of value constraints.</param>
    /// <param name="second">The second set of value constraints.</param>
    /// <returns>True when validation metadata is equivalent, excluding defaults.</returns>
    public static bool Equivalent(IrPropertyConstraints first, IrPropertyConstraints second) =>
        first.Min == second.Min && first.Max == second.Max &&
        first.ExclusiveMin == second.ExclusiveMin && first.ExclusiveMax == second.ExclusiveMax &&
        first.MinLength == second.MinLength && first.MaxLength == second.MaxLength &&
        string.Equals(first.Pattern, second.Pattern, StringComparison.Ordinal);
}
