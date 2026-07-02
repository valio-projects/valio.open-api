using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Core.IR;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Builds the request-URL expression for an operation by substituting <c>{name}</c> path placeholders with the
/// corresponding route-parameter access expressions from a <see cref="TypeScriptParameterModel"/>.
/// </summary>
/// <remarks>
/// This is the single implementation of the path-templating that was previously copy-pasted across every
/// transport client builder (fetch/nextjs/ky/got/ofetch/undici/angular/axios). The output is a plain string
/// literal when the path has no parameters, or a template literal such as <c>`/api/apartments/${params.route.id}`</c>
/// otherwise. Placeholder matching is case-insensitive because a spec may write the placeholder
/// (<c>{dictionaryId}</c>) with different casing than the sanitized parameter name (<c>DictionaryId</c>).
/// </remarks>
/// <seealso cref="TypeScriptParameterModel"/>
public static class TypeScriptRequestPath
{
    /// <summary>
    /// Builds the URL expression for <paramref name="operation"/> using <paramref name="model"/> for route access.
    /// </summary>
    /// <param name="operation">The IR operation whose <see cref="IrOperation.Path"/> is templated.</param>
    /// <param name="model">The parameter model providing route-parameter access expressions.</param>
    /// <returns>
    /// A <see cref="LiteralExpressionNode"/> when the path has no substitutable placeholders, otherwise a
    /// <see cref="TemplateLiteralExpressionNode"/> interleaving the literal path fragments with route accesses.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> or <paramref name="model"/> is <see langword="null"/>.</exception>
    public static ExpressionNode BuildUrl(IrOperation operation, TypeScriptParameterModel model)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(model);

        if (operation.PathParameters.Count == 0 || !model.HasRoute)
            return new LiteralExpressionNode(operation.Path, LiteralKind.String);

        var parts = new List<TemplateLiteralPart>();
        var cursor = 0;
        foreach (var parameter in operation.PathParameters)
        {
            var token = $"{{{parameter.Name}}}";
            var index = operation.Path.IndexOf(token, cursor, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;
            if (index > cursor)
                parts.Add(new TemplateLiteralPart(operation.Path[cursor..index]));
            parts.Add(new TemplateLiteralPart(model.RouteAccess(parameter.Name)));
            cursor = index + token.Length;
        }
        if (cursor < operation.Path.Length)
            parts.Add(new TemplateLiteralPart(operation.Path[cursor..]));

        return parts.Count == 0
            ? new LiteralExpressionNode(operation.Path, LiteralKind.String)
            : new TemplateLiteralExpressionNode(parts);
    }

    /// <summary>
    /// Builds the URL expression from an explicit <paramref name="routeSource"/> object expression rather than the
    /// parameter model, so a transport that has rebound its route source (e.g. to a validated <c>parsedRoute</c>)
    /// templates the path against that rebound source.
    /// </summary>
    /// <param name="operation">The IR operation whose <see cref="IrOperation.Path"/> is templated.</param>
    /// <param name="routeSource">
    /// The route object the placeholders resolve against (e.g. <c>params.route</c>, <c>params</c>, or a validated
    /// <c>parsedRoute</c>), or <see langword="null"/> when the operation has no route source.
    /// </param>
    /// <returns>
    /// A <see cref="LiteralExpressionNode"/> when the path has no substitutable placeholders (or no route source),
    /// otherwise a <see cref="TemplateLiteralExpressionNode"/> interleaving literal fragments with per-parameter
    /// access built by <see cref="BuildParameterAccess"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
    /// <seealso cref="BuildParameterAccess"/>
    public static ExpressionNode BuildUrl(IrOperation operation, ExpressionNode? routeSource)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.PathParameters.Count == 0 || routeSource == null)
            return new LiteralExpressionNode(operation.Path, LiteralKind.String);

        var parts = new List<TemplateLiteralPart>();
        var cursor = 0;
        foreach (var parameter in operation.PathParameters)
        {
            var token = $"{{{parameter.Name}}}";
            var index = operation.Path.IndexOf(token, cursor, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;
            if (index > cursor)
                parts.Add(new TemplateLiteralPart(operation.Path[cursor..index]));
            parts.Add(new TemplateLiteralPart(BuildParameterAccess(routeSource, parameter.Name, mayBeUndefined: false)));
            cursor = index + token.Length;
        }
        if (cursor < operation.Path.Length)
            parts.Add(new TemplateLiteralPart(operation.Path[cursor..]));

        return parts.Count == 0
            ? new LiteralExpressionNode(operation.Path, LiteralKind.String)
            : new TemplateLiteralExpressionNode(parts);
    }

    /// <summary>
    /// Builds a per-field member-access expression <c>source.name</c> (optionally with optional chaining) for
    /// reaching a single route/query/header value off its group object — the access the transport clients use
    /// when folding query params into a query string or headers into a header object.
    /// </summary>
    /// <param name="source">The group object expression (e.g. <c>params.query</c>, or a validated <c>parsedQuery</c>).</param>
    /// <param name="name">The (sanitized) parameter name.</param>
    /// <param name="mayBeUndefined">
    /// Whether the group object may be <c>undefined</c> at runtime; when <see langword="true"/> the access uses
    /// optional chaining (<c>source?.name</c>).
    /// </param>
    /// <returns>
    /// The member-access expression, or <paramref name="source"/> unchanged when it is the literal
    /// <c>undefined</c> identifier (a validated source that resolved to nothing).
    /// </returns>
    public static ExpressionNode BuildParameterAccess(ExpressionNode source, string name, bool mayBeUndefined) =>
        source is IdentifierExpressionNode { Identifier.Name: "undefined" }
            ? source
            : new MemberAccessExpressionNode(source, new IdentifierNode(name), useOptionalChaining: mayBeUndefined);
}
