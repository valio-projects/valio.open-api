using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Utilities;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>Builds content-type dispatch for responses with distinct schemas or decoding rules.</summary>
internal static class TypeScriptResponseMedia
{
    /// <summary>Returns the supported alternatives for a status that needs media negotiation.</summary>
    /// <param name="operation">The operation carrying the response.</param>
    /// <param name="response">The status-specific response contract.</param>
    /// <returns>Alternatives in document order, or an empty list when the existing response path applies.</returns>
    internal static IReadOnlyList<IrResponseMediaType> Alternatives(IrOperation operation, IrResponse response)
    {
        if (operation.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(response.StatusCode, out var status) || status is 204 or 205 or 304
            || response.MediaTypes.Count < 2 || response.MediaTypes.Any(media => !IsSupported(media))
            || response.MediaTypes.Select(media => media.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != response.MediaTypes.Count
            || operation.Responses.Any(Axios.Ast.Builders.AxiosAstBuilder.IsBinarySuccessResponse))
            return [];

        return response.MediaTypes.Any(IsPlainText) || HasDistinctSchemas(response.MediaTypes)
            ? response.MediaTypes
            : [];
    }

    /// <summary>Determines whether the supported representations require a union in the result body.</summary>
    /// <param name="mediaTypes">The supported response representations.</param>
    /// <returns>True when at least two body schemas differ.</returns>
    internal static bool HasDistinctSchemas(IReadOnlyList<IrResponseMediaType> mediaTypes) =>
        mediaTypes.Count > 1 && mediaTypes.Skip(1).Any(media =>
            !IrTypeComparer.AreSemanticallyEqual(mediaTypes[0].Type, media.Type, ignorePresence: false));

    /// <summary>Determines whether the operation needs content-type dispatch at runtime.</summary>
    /// <param name="operation">The endpoint contract.</param>
    /// <returns>True when any response has supported distinct media schemas.</returns>
    internal static bool HasAlternatives(IrOperation operation) => operation.Responses.Any(response => Alternatives(operation, response).Count != 0);

    /// <summary>Returns all response schema inputs, including stable names for media-specific validators.</summary>
    /// <param name="operation">The endpoint requiring response validators.</param>
    /// <param name="includeAlternatives">Whether the consuming transport uses content-type dispatch.</param>
    /// <param name="preservePrimary">Whether to emit primary definitions other transports may still consume.</param>
    /// <returns>The existing schema inputs followed by the media-specific inputs.</returns>
    internal static IEnumerable<IrResponse> SchemaInputs(IrOperation operation, bool includeAlternatives, bool preservePrimary = false)
    {
        foreach (var response in operation.Responses.Where(response => int.TryParse(response.StatusCode, out _)))
        {
            IReadOnlyList<IrResponseMediaType> alternatives = includeAlternatives ? Alternatives(operation, response) : [];
            if (alternatives.Count == 0 || preservePrimary)
                yield return response;
            for (var index = 0; index < alternatives.Count; index++)
                yield return new IrResponse(response) { StatusCode = SchemaCode(response, index), Type = alternatives[index].Type };
        }
    }

    /// <summary>Collects the actual media type and rejects a missing or undeclared value on a negotiated status.</summary>
    /// <param name="operation">The endpoint defining negotiated responses.</param>
    /// <param name="response">The native Fetch or Axios response.</param>
    /// <param name="header">The Content-Type header value obtained by the transport.</param>
    /// <param name="statements">The generated statements, before body decoding and validation.</param>
    internal static void AddContentTypeGuards(IrOperation operation, ExpressionNode response, ExpressionNode header, List<StatementNode> statements)
    {
        if (!HasAlternatives(operation))
            return;

        var headerText = Call(Id("String"), new BinaryExpressionNode(header, BinaryOperator.NullishCoalescing, Str(string.Empty)));
        var firstPart = Call(Member(Call(Member(headerText, "split"), Str(";")), "shift"));
        var trimmed = Call(Member(new BinaryExpressionNode(firstPart, BinaryOperator.NullishCoalescing, Str(string.Empty)), "trim"));
        var mediaType = Call(Member(trimmed, "toLowerCase"));
        statements.Add(new VariableDeclarationStatementNode(new IdentifierNode("responseMediaType"), type: null, initializer: mediaType));

        foreach (var variant in operation.Responses)
        {
            var alternatives = Alternatives(operation, variant);
            if (alternatives.Count == 0)
                continue;

            var media = new ArrayLiteralExpressionNode(alternatives.Select(alternative => (ExpressionNode)Str(alternative.Name.ToLowerInvariant())).ToArray());
            var unsupported = new UnaryExpressionNode(UnaryOperator.LogicalNot, Call(Member(media, "includes"), Id("responseMediaType")));
            var message = new TemplateLiteralExpressionNode([
                new TemplateLiteralPart($"HTTP {variant.StatusCode} unexpected Content-Type: "), new TemplateLiteralPart(Id("responseMediaType"))
            ]);
            statements.Add(new IfStatementNode(
                new BinaryExpressionNode(StatusMatches(Member(response, "status"), variant.StatusCode), BinaryOperator.LogicalAnd, unsupported),
                new BlockNode([new ThrowStatementNode(new CallExpressionNode(Id("Error"), [message], isNewExpression: true))])));
        }
    }

    /// <summary>Selects the matching body validator for a response after its content-type guard.</summary>
    /// <param name="operation">The endpoint describing the response.</param>
    /// <param name="response">The status-specific contract.</param>
    /// <param name="buildBody">Builds the body for one variant, with its media-specific schema identifier.</param>
    /// <returns>The selected body expression or the existing single-variant body.</returns>
    internal static ExpressionNode SelectBody(IrOperation operation, IrResponse response, Func<IrResponse, ExpressionNode> buildBody)
    {
        var alternatives = Alternatives(operation, response);
        if (alternatives.Count == 0)
            return buildBody(response);

        var last = alternatives.Count - 1;
        var body = buildBody(new IrResponse(response) { StatusCode = SchemaCode(response, last), Type = alternatives[last].Type });
        for (var index = last - 1; index >= 0; index--)
            body = new ConditionalExpressionNode(Matches(alternatives[index].Name),
                buildBody(new IrResponse(response) { StatusCode = SchemaCode(response, index), Type = alternatives[index].Type }), body);
        return body;
    }

    /// <summary>Matches the normalized response media type against one declared type.</summary>
    /// <param name="name">The media type from the OpenAPI response.</param>
    /// <returns>The generated exact-match expression.</returns>
    internal static ExpressionNode Matches(string name) =>
        new BinaryExpressionNode(Id("responseMediaType"), BinaryOperator.Equal, Str(name.ToLowerInvariant()));

    /// <summary>Decodes a text response from Axios after the negotiated Content-Type has been checked.</summary>
    /// <param name="data">The response data before Axios's JSON transform.</param>
    /// <param name="exactNumbers">Whether JSON must retain original numeric tokens.</param>
    /// <returns>The decoded JSON or the unchanged plain-text data.</returns>
    internal static ExpressionNode DecodeAxiosResponse(ExpressionNode data, bool exactNumbers)
    {
        var jsonMedia = new BinaryExpressionNode(Matches("application/json"), BinaryOperator.LogicalOr, Matches("text/json"));
        jsonMedia = new BinaryExpressionNode(jsonMedia, BinaryOperator.LogicalOr,
            Call(Member(Id("responseMediaType"), "endsWith"), Str("+json")));
        // When an unrelated status omits Content-Type, retain Axios's usual JSON interpretation.
        jsonMedia = new BinaryExpressionNode(jsonMedia, BinaryOperator.LogicalOr, Matches(string.Empty));
        var parser = exactNumbers ? TypeScriptExactJson.Runtime("parse") : Member(Id("JSON"), "parse");
        var isString = new BinaryExpressionNode(Member(data, "constructor"), BinaryOperator.Equal, Id("String"));
        var hasRawText = new BinaryExpressionNode(data, BinaryOperator.LogicalAnd, isString);
        return new ConditionalExpressionNode(new BinaryExpressionNode(hasRawText, BinaryOperator.LogicalAnd, jsonMedia), Call(parser, data), data);
    }

    /// <summary>Matches the received numeric status against a documented status.</summary>
    /// <param name="status">The received HTTP status expression.</param>
    /// <param name="code">The documented numeric code.</param>
    /// <returns>The generated status match expression.</returns>
    internal static ExpressionNode StatusMatches(ExpressionNode status, string code) =>
        new BinaryExpressionNode(status, BinaryOperator.Equal, new LiteralExpressionNode(int.Parse(code), LiteralKind.Number));

    /// <summary>Identifies supported text candidates independently of the first media schema.</summary>
    /// <param name="media">A declared response representation.</param>
    /// <returns>True for a plain-text string response.</returns>
    internal static bool IsPlainText(IrResponseMediaType media) =>
        media.Name.Equals("text/plain", StringComparison.OrdinalIgnoreCase)
        && media.Type is { } type && IrTypeQueries.UnwrapNullableOptional(type) is IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.String };

    /// <summary>Checks whether a representation can use the supported media-specific decoding path.</summary>
    /// <param name="media">A declared response representation.</param>
    /// <returns>True for schema-bearing JSON or a plain-text string.</returns>
    private static bool IsSupported(IrResponseMediaType media) => media.Type is not null
        && (IsPlainText(media) || media.Name.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || media.Name.Equals("text/json", StringComparison.OrdinalIgnoreCase) || media.Name.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds a stable schema suffix distinct from the status schema used by existing generators.</summary>
    /// <param name="response">The response declaring the schema.</param>
    /// <param name="index">The zero-based media position.</param>
    /// <returns>The status and media suffix.</returns>
    private static string SchemaCode(IrResponse response, int index) => $"{response.StatusCode}Media{index + 1}";

    /// <summary>References a generated local or JavaScript global.</summary>
    /// <param name="name">The identifier.</param>
    /// <returns>The identifier expression.</returns>
    private static IdentifierExpressionNode Id(string name) => new(new IdentifierNode(name));

    /// <summary>References a property of a generated value.</summary>
    /// <param name="value">The owning expression.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The member access expression.</returns>
    private static MemberAccessExpressionNode Member(ExpressionNode value, string name) => new(value, new IdentifierNode(name));

    /// <summary>Invokes a generated JavaScript function.</summary>
    /// <param name="target">The callable expression.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The generated invocation.</returns>
    private static CallExpressionNode Call(ExpressionNode target, params ExpressionNode[] arguments) => new(target, arguments);

    /// <summary>Builds an escaped string literal.</summary>
    /// <param name="value">The unescaped source text.</param>
    /// <returns>The literal expression.</returns>
    private static LiteralExpressionNode Str(string value) => new(value, LiteralKind.String);
}
