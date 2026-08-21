using System.Collections.Immutable;
using System.Text.Json;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Builds TypeScript object literals whose property keys preserve the OpenAPI wire names while their values are
/// read through sanitized TypeScript parameter identifiers.
/// </summary>
/// <remarks>
/// OpenAPI names such as <c>Paging.Offset</c> and <c>X-Request-ID</c> cannot be used as plain TypeScript member
/// identifiers. The generated params contract therefore exposes sanitized members such as
/// <c>Paging_Offset</c> and <c>X_Request_ID</c>, but the HTTP request must retain the original names.
/// </remarks>
public static class TypeScriptWireParameterObject
{
    /// <summary>
    /// Builds an object literal by reading every parameter from <paramref name="source"/> through its sanitized
    /// <see cref="IrParameter.Name"/> and writing it under its original <see cref="IrParameter.OriginalName"/>.
    /// </summary>
    /// <param name="parameters">The ordered parameters to serialize.</param>
    /// <param name="source">The generated params group or validated params object.</param>
    /// <param name="sourceMayBeUndefined">Whether member access must use optional chaining.</param>
    /// <param name="omitUndefinedValues">
    /// Whether optional or unavailable values are emitted through conditional spreads so they are absent from the
    /// resulting object instead of being assigned <c>undefined</c>.
    /// </param>
    /// <returns>A multiline object literal suitable for a transport's query or headers configuration.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="parameters"/> or <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    public static ObjectLiteralExpressionNode BuildFromSource(
        IReadOnlyList<IrParameter> parameters,
        ExpressionNode source,
        bool sourceMayBeUndefined,
        bool omitUndefinedValues)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(source);

        return Build(
            parameters,
            parameter => TypeScriptRequestPath.BuildParameterAccess(source, parameter.Name, sourceMayBeUndefined),
            sourceMayBeUndefined,
            omitUndefinedValues);
    }

    /// <summary>
    /// Builds an object literal for legacy flat client functions whose values are standalone sanitized identifiers.
    /// </summary>
    /// <param name="parameters">The ordered parameters to serialize.</param>
    /// <param name="omitUndefinedValues">
    /// Whether optional values are omitted through conditional spreads instead of being assigned
    /// <c>undefined</c>.
    /// </param>
    /// <returns>An object literal keyed by the parameters' original wire names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <see langword="null"/>.</exception>
    public static ObjectLiteralExpressionNode BuildFromIdentifiers(
        IReadOnlyList<IrParameter> parameters,
        bool omitUndefinedValues)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return Build(
            parameters,
            parameter => Id(parameter.Name),
            sourceMayBeUndefined: false,
            omitUndefinedValues);
    }

    /// <summary>Builds the wire-keyed object from a caller-provided value expression.</summary>
    /// <param name="parameters">The ordered parameters to serialize.</param>
    /// <param name="valueFactory">Creates the sanitized value access for one parameter.</param>
    /// <param name="sourceMayBeUndefined">Whether the containing source may be unavailable.</param>
    /// <param name="omitUndefinedValues">Whether unavailable values must be omitted.</param>
    /// <returns>The generated object literal.</returns>
    private static ObjectLiteralExpressionNode Build(
        IReadOnlyList<IrParameter> parameters,
        Func<IrParameter, ExpressionNode> valueFactory,
        bool sourceMayBeUndefined,
        bool omitUndefinedValues)
    {
        var properties = ImmutableList.CreateBuilder<PropertyAssignmentNode>();

        foreach (var parameter in parameters)
        {
            var value = valueFactory(parameter);
            var assignment = new PropertyAssignmentNode(WireKey(parameter.OriginalName), value);
            var mayBeUndefined = sourceMayBeUndefined || !parameter.IsRequired;

            if (!omitUndefinedValues || !mayBeUndefined)
            {
                properties.Add(assignment);
                continue;
            }

            var populated = new ObjectLiteralExpressionNode(
                ImmutableList.Create(assignment),
                preferMultilineFormat: true);
            var conditional = new ConditionalExpressionNode(
                new BinaryExpressionNode(value, BinaryOperator.NotEqual, Id("undefined")),
                populated,
                EmptyObject());
            properties.Add(new PropertyAssignmentNode(new IdentifierNode("_"), conditional, isSpread: true));
        }

        return new ObjectLiteralExpressionNode(properties.ToImmutable(), preferMultilineFormat: true);
    }

    /// <summary>Creates a quoted TypeScript property key from an arbitrary OpenAPI wire name.</summary>
    /// <param name="wireName">The unmodified OpenAPI parameter name.</param>
    /// <returns>An identifier node containing a JSON-escaped string literal.</returns>
    private static IdentifierNode WireKey(string wireName) => new(JsonSerializer.Serialize(wireName));

    /// <summary>Creates an identifier expression.</summary>
    /// <param name="name">The identifier name.</param>
    /// <returns>The identifier expression.</returns>
    private static IdentifierExpressionNode Id(string name) => new(new IdentifierNode(name));

    /// <summary>Creates an empty object literal.</summary>
    /// <returns>An object literal without properties.</returns>
    private static ObjectLiteralExpressionNode EmptyObject() =>
        new(ImmutableList<PropertyAssignmentNode>.Empty);
}
