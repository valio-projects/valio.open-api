using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Utilities;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>Builds response decoding that respects protocol and documented no-body responses.</summary>
public static class TypeScriptResponseBody
{
    /// <summary>Decodes a declared plain-text string response directly and uses the existing decoder for other payloads.</summary>
    /// <param name="operation">The operation whose documented statuses determine how the body is read.</param>
    /// <param name="response">The native Response expression from the fetch transport.</param>
    /// <param name="decoder">The existing JSON decoder, including the exact-number variant when enabled.</param>
    /// <returns>A bodyless, text, or JSON expression selected from the response contract.</returns>
    internal static ExpressionNode ReadFetch(IrOperation operation, ExpressionNode response, ExpressionNode decoder)
    {
        var status = new MemberAccessExpressionNode(response, new IdentifierNode("status"));
        ExpressionNode? textCondition = null;
        foreach (var variant in operation.Responses)
        {
            if (variant.ContentTypes.Count != 1 || !string.Equals(variant.ContentTypes[0], "text/plain", StringComparison.OrdinalIgnoreCase)
                || variant.Type is not { } type || IrTypeQueries.UnwrapNullableOptional(type) is not IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.String }
                || !int.TryParse(variant.StatusCode, out var code))
                continue;

            var matches = new BinaryExpressionNode(status, BinaryOperator.Equal, new LiteralExpressionNode(code, LiteralKind.Number));
            textCondition = textCondition is null ? matches : new BinaryExpressionNode(textCondition, BinaryOperator.LogicalOr, matches);
        }

        if (textCondition is not null)
        {
            var textDecoder = new AwaitExpressionNode(new CallExpressionNode(
                new MemberAccessExpressionNode(response, new IdentifierNode("text")), []));
            decoder = new ConditionalExpressionNode(textCondition, textDecoder, decoder);
        }

        return Read(operation, status, decoder);
    }

    /// <summary>Skips a response body decoder for HEAD, 204/205, and explicitly bodyless response variants.</summary>
    /// <param name="operation">The operation whose response contract determines decoding.</param>
    /// <param name="status">The received response status expression.</param>
    /// <param name="decoder">The body decoder expression, evaluated only when content is permitted.</param>
    /// <returns>The conditional decoded value, or undefined for HEAD.</returns>
    public static ExpressionNode Read(IrOperation operation, ExpressionNode status, ExpressionNode decoder)
    {
        var undefined = new IdentifierExpressionNode(new IdentifierNode("undefined"));
        if (string.Equals(operation.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
            return undefined;

        var bodylessStatuses = new HashSet<int> { 204, 205 };
        foreach (var response in operation.Responses)
        {
            if (response.Type == null && int.TryParse(response.StatusCode, out var statusCode))
                bodylessStatuses.Add(statusCode);
        }

        ExpressionNode? condition = null;
        foreach (var statusCode in bodylessStatuses.Order())
        {
            var matches = new BinaryExpressionNode(status, BinaryOperator.Equal, new LiteralExpressionNode(statusCode, LiteralKind.Number));
            condition = condition == null ? matches : new BinaryExpressionNode(condition, BinaryOperator.LogicalOr, matches);
        }

        return new ConditionalExpressionNode(condition!, undefined, decoder);
    }
}
