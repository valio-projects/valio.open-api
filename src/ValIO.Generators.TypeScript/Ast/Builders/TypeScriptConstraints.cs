using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR.Constraints;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Builds the exported <c>as const</c> "constraints" objects that hold validation-rule values (string lengths,
/// numeric ranges, regex patterns) so the generated schemas reference named constants instead of inlining magic
/// numbers.
/// </summary>
/// <remarks>
/// This is the single implementation shared by model properties (<c>{Model}Constraints</c> in the model
/// interface file) and operation parameters (<c>{Op}RouteParamsConstraints</c> / <c>{Op}QueryParamsConstraints</c>
/// in the params interface file). The emitted shape is
/// <c>export const {name} = { field: { min: …, maxLength: …, pattern: "…" } as const, … } as const;</c>; only the
/// constraint kinds actually present on each entry are emitted, in a stable order.
/// </remarks>
public static class TypeScriptConstraints
{
    /// <summary>Whether <paramref name="constraints"/> carries any emittable validation rule.</summary>
    /// <param name="constraints">The constraints to test.</param>
    /// <returns><see langword="true"/> when at least one range/length/pattern rule is present.</returns>
    public static bool HasAnyConstraint(IrPropertyConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        return constraints.Min.HasValue || constraints.Max.HasValue
            || constraints.ExclusiveMin.HasValue || constraints.ExclusiveMax.HasValue
            || constraints.MinLength.HasValue || constraints.MaxLength.HasValue
            || !string.IsNullOrEmpty(constraints.Pattern);
    }

    /// <summary>
    /// Builds the exported constraints const declaration, or <see langword="null"/> when none of
    /// <paramref name="entries"/> carries a constraint (so callers can skip emitting an empty object).
    /// </summary>
    /// <param name="constName">The exported constant name (e.g. <c>CatConstraints</c>, <c>GetTreeQueryParamsConstraints</c>).</param>
    /// <param name="entries">The (field name, constraints) pairs, in the order they should appear.</param>
    /// <param name="documentation">Optional JSDoc for the declaration.</param>
    /// <returns>The exported <c>as const</c> variable declaration, or <see langword="null"/> when empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="constName"/> or <paramref name="entries"/> is <see langword="null"/>.</exception>
    public static VariableDeclarationNode? BuildConstraintsObject(
        string constName,
        IEnumerable<(string Name, IrPropertyConstraints Constraints)> entries,
        CommentNode? documentation)
    {
        ArgumentNullException.ThrowIfNull(constName);
        ArgumentNullException.ThrowIfNull(entries);

        var withConstraints = entries.Where(e => HasAnyConstraint(e.Constraints)).ToList();
        if (withConstraints.Count == 0)
            return null;

        var properties = withConstraints
            .Select(e => BuildEntry(e.Name, e.Constraints))
            .ToImmutableList();

        return new VariableDeclarationNode(
            new IdentifierNode(constName),
            VariableDeclarationKind.Const,
            initializer: new ObjectLiteralExpressionNode(properties, preferMultilineFormat: true),
            documentation: documentation,
            isExported: true,
            asConstAssertion: true);
    }

    /// <summary>Builds one <c>field: { … } as const</c> entry from its constraints.</summary>
    /// <param name="name">The field (property/parameter) name.</param>
    /// <param name="c">The field's constraints.</param>
    /// <returns>The property-assignment node for the entry.</returns>
    private static PropertyAssignmentNode BuildEntry(string name, IrPropertyConstraints c)
    {
        var props = new List<PropertyAssignmentNode>();
        if (c.Min.HasValue)
            props.Add(new PropertyAssignmentNode(new IdentifierNode("min"), new LiteralExpressionNode(c.Min.Value, LiteralKind.Number)));
        if (c.Max.HasValue)
            props.Add(new PropertyAssignmentNode(new IdentifierNode("max"), new LiteralExpressionNode(c.Max.Value, LiteralKind.Number)));
        if (c.ExclusiveMin.HasValue)
            props.Add(new PropertyAssignmentNode(new IdentifierNode("exclusiveMin"), new LiteralExpressionNode(c.ExclusiveMin.Value, LiteralKind.Number)));
        if (c.ExclusiveMax.HasValue)
            props.Add(new PropertyAssignmentNode(new IdentifierNode("exclusiveMax"), new LiteralExpressionNode(c.ExclusiveMax.Value, LiteralKind.Number)));
        if (c.MinLength.HasValue)
            props.Add(new PropertyAssignmentNode(new IdentifierNode("minLength"), new LiteralExpressionNode(c.MinLength.Value, LiteralKind.Number)));
        if (c.MaxLength.HasValue)
            props.Add(new PropertyAssignmentNode(new IdentifierNode("maxLength"), new LiteralExpressionNode(c.MaxLength.Value, LiteralKind.Number)));
        if (!string.IsNullOrEmpty(c.Pattern))
            props.Add(new PropertyAssignmentNode(new IdentifierNode("pattern"), new LiteralExpressionNode(c.Pattern, LiteralKind.String)));

        return new PropertyAssignmentNode(
            new IdentifierNode(name),
            new ObjectLiteralExpressionNode(
                props.ToImmutableList(),
                preferMultilineFormat: props.Count > 1,
                asConstAssertion: true));
    }
}
