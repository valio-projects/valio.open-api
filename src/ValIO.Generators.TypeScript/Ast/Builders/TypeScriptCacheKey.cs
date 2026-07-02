using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Builds the cache-identity expressions shared by the TanStack query hooks (array query keys) and the Next.js
/// transport (string cache tags), from a single canonical view of an operation's path + query parameters.
/// </summary>
/// <remarks>
/// <para>
/// The idiomatic TanStack query key structures from most-generic to most-specific: literal path segments, the
/// route-parameter <em>values</em> inlined in place of their <c>{name}</c> placeholders, and — when the operation
/// has query parameters — the query object as a single trailing element. TanStack invalidation then supports both
/// prefix matching (invalidate everything under an id) and partial object matching (invalidate a filter subset),
/// so filters intentionally stay one object rather than being spread as positional elements. Headers and the
/// request body are deliberately excluded from cache identity.
/// </para>
/// <para>
/// Next.js cache tags are flat exact-match strings, so <see cref="BuildTagExpressions"/> projects the same scalar
/// segments into hierarchical <c>":"</c>-joined prefix tags (e.g. <c>api</c>, <c>api:apartments</c>,
/// <c>api:apartments:${id}</c>, <c>api:apartments:${id}:investor</c>). A server <c>revalidateTag("api:apartments:123")</c>
/// then mirrors a client <c>invalidateQueries({ queryKey: ['api','apartments','123'] })</c> — the two transports
/// share one cache identity.
/// </para>
/// </remarks>
/// <seealso cref="TypeScriptParameterModel"/>
public static class TypeScriptCacheKey
{
    /// <summary>
    /// Builds the ordered elements of a TanStack query-key array for <paramref name="operation"/>.
    /// </summary>
    /// <param name="operation">The IR operation.</param>
    /// <param name="model">The parameter model providing route/query access expressions.</param>
    /// <param name="includeInfinite">
    /// When <see langword="true"/>, prepends the <c>"infinite"</c> discriminator segment used by
    /// <c>useInfiniteQuery</c> keys so they never collide with the plain <c>useQuery</c> key.
    /// </param>
    /// <returns>The ordered key elements: literal segments, inlined route values, then the query object tail.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> or <paramref name="model"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<ExpressionNode> BuildKeyElements(
        IrOperation operation,
        TypeScriptParameterModel model,
        bool includeInfinite)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(model);

        var elements = new List<ExpressionNode>();
        if (includeInfinite)
            elements.Add(StringLit("infinite"));

        var segments = SplitSegments(operation, model);
        if (segments.Count == 0)
            elements.Add(StringLit("api"));

        foreach (var parts in segments)
            elements.Add(SegmentToKeyElement(parts));

        // Query filter tail: a single object element (idiomatic; supports partial-match invalidation).
        var queryObject = model.QueryObject();
        if (queryObject != null)
        {
            if (model.QueryMayBeUndefined)
            {
                // ...(params.query ? [params.query] : [])
                var conditional = new ConditionalExpressionNode(
                    queryObject,
                    new ArrayLiteralExpressionNode(new List<ExpressionNode> { queryObject }),
                    new ArrayLiteralExpressionNode(new List<ExpressionNode>()));
                elements.Add(new SpreadExpressionNode(conditional));
            }
            else
            {
                elements.Add(queryObject);
            }
        }

        return elements;
    }

    /// <summary>
    /// Builds the hierarchical Next.js cache-tag prefix expressions for <paramref name="operation"/> — one
    /// cumulative <c>":"</c>-joined prefix per scalar path segment (the coarse operation tag is prepended by the
    /// caller so it has a single source of truth).
    /// </summary>
    /// <param name="operation">The IR operation.</param>
    /// <param name="model">The parameter model providing route access expressions.</param>
    /// <returns>
    /// One expression per path prefix: a string literal while the prefix is fully static (e.g. <c>"api"</c>,
    /// <c>"api:apartments"</c>), or a template literal once a route value participates
    /// (e.g. <c>`api:apartments:${params.route.id}`</c>). Empty when the path has no segments.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> or <paramref name="model"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<ExpressionNode> BuildTagExpressions(
        IrOperation operation,
        TypeScriptParameterModel model)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(model);

        var result = new List<ExpressionNode>();

        var segments = SplitSegments(operation, model);
        var accumulated = new List<TemplateLiteralPart>();
        for (var i = 0; i < segments.Count; i++)
        {
            if (i > 0)
                accumulated.Add(new TemplateLiteralPart(":"));
            accumulated.AddRange(segments[i]);
            result.Add(ToTagExpression(accumulated));
        }

        return result;
    }

    /// <summary>
    /// Splits <paramref name="operation"/>'s path into per-URL-segment token lists, mapping each <c>{name}</c>
    /// placeholder to its route-parameter access expression.
    /// </summary>
    /// <param name="operation">The IR operation.</param>
    /// <param name="model">The parameter model providing route access expressions.</param>
    /// <returns>One <see cref="TemplateLiteralPart"/> list per non-empty path segment.</returns>
    private static List<List<TemplateLiteralPart>> SplitSegments(IrOperation operation, TypeScriptParameterModel model)
    {
        var segments = new List<List<TemplateLiteralPart>>();
        var trimmed = operation.Path.Trim('/');
        if (string.IsNullOrEmpty(trimmed))
            return segments;

        foreach (var segment in trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries))
            segments.Add(ParseSegmentParts(segment, operation, model));

        return segments;
    }

    /// <summary>
    /// Parses a single path segment into text/expression parts, substituting <c>{name}</c> tokens with route access.
    /// </summary>
    /// <param name="segment">The raw path segment (no slashes).</param>
    /// <param name="operation">The IR operation (for resolving placeholder names to parameters).</param>
    /// <param name="model">The parameter model providing route access expressions.</param>
    /// <returns>The ordered parts making up this segment.</returns>
    private static List<TemplateLiteralPart> ParseSegmentParts(string segment, IrOperation operation, TypeScriptParameterModel model)
    {
        var parts = new List<TemplateLiteralPart>();
        var cursor = 0;
        while (cursor < segment.Length)
        {
            var open = segment.IndexOf('{', cursor);
            if (open < 0)
            {
                parts.Add(new TemplateLiteralPart(segment[cursor..]));
                break;
            }

            var close = segment.IndexOf('}', open + 1);
            if (close < 0)
            {
                parts.Add(new TemplateLiteralPart(segment[cursor..]));
                break;
            }

            if (open > cursor)
                parts.Add(new TemplateLiteralPart(segment[cursor..open]));

            var inner = segment[(open + 1)..close];
            var parameter = operation.PathParameters.FirstOrDefault(p => p.Name.Equals(inner, StringComparison.OrdinalIgnoreCase));
            if (parameter != null)
                parts.Add(new TemplateLiteralPart(model.RouteAccess(parameter.Name)));
            else
                parts.Add(new TemplateLiteralPart(segment[open..(close + 1)])); // No matching param — keep the raw token.

            cursor = close + 1;
        }

        if (parts.Count == 0)
            parts.Add(new TemplateLiteralPart(string.Empty));

        return parts;
    }

    /// <summary>
    /// Converts a segment's parts into a single query-key element: a string literal for a static segment, the raw
    /// route access for a whole-segment placeholder (preserving the value's native type), or a template literal
    /// for a mixed segment.
    /// </summary>
    /// <param name="parts">The segment parts.</param>
    /// <returns>The key element expression.</returns>
    private static ExpressionNode SegmentToKeyElement(List<TemplateLiteralPart> parts)
    {
        if (parts.Count == 1 && parts[0].IsExpression)
            return parts[0].Expression!;
        if (parts.Count == 1)
            return StringLit(parts[0].Text!);
        return new TemplateLiteralExpressionNode(parts);
    }

    /// <summary>
    /// Renders accumulated tag parts into one tag expression, merging adjacent text runs. Returns a plain string
    /// literal when every part is text, otherwise a template literal.
    /// </summary>
    /// <param name="parts">The accumulated parts for the current cumulative prefix.</param>
    /// <returns>The tag expression.</returns>
    private static ExpressionNode ToTagExpression(List<TemplateLiteralPart> parts)
    {
        var merged = new List<TemplateLiteralPart>();
        var text = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.IsExpression)
            {
                if (text.Length > 0)
                {
                    merged.Add(new TemplateLiteralPart(text.ToString()));
                    text.Clear();
                }
                merged.Add(part);
            }
            else
            {
                text.Append(part.Text);
            }
        }
        if (text.Length > 0)
            merged.Add(new TemplateLiteralPart(text.ToString()));

        return merged.All(p => !p.IsExpression)
            ? StringLit(string.Concat(merged.Select(p => p.Text)))
            : new TemplateLiteralExpressionNode(merged);
    }

    /// <summary>Builds a string literal expression.</summary>
    /// <param name="value">The literal string value.</param>
    /// <returns>A <see cref="LiteralExpressionNode"/> of kind <see cref="LiteralKind.String"/>.</returns>
    private static LiteralExpressionNode StringLit(string value) => new(value, LiteralKind.String);
}
