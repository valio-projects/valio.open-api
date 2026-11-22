using System.Globalization;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.CSharp.Ast.Nodes;

namespace ValIO.Generators.CSharp.Ast.Builders;

/// <summary>Projects the optional HTTP response metadata methods from operation contracts.</summary>
public partial class CSharpAstBuilder
{
    /// <summary>Documents the result of a response-aware operation.</summary>
    private const string MetadataReturnDocumentation =
        "The observed HTTP status, response headers, and any payload confirmed by the declared response contract.";

    /// <summary>Appends the opt-in suffix to a resolved C# operation name.</summary>
    /// <param name="operation">The operation with its resolved client member name.</param>
    /// <returns>The name of the response-aware method.</returns>
    private static string MetadataMethodName(IrOperation operation) => operation.MethodName + "WithResponse";

    /// <summary>Gets the operations that expose companion response methods.</summary>
    /// <param name="client">The client with any pre-existing member naming collisions resolved.</param>
    /// <returns>All client operations for a response-aware profile, or an empty sequence.</returns>
    /// <exception cref="InvalidOperationException">A companion method would hide a schema operation or class name.</exception>
    private IReadOnlyList<IrOperation> MetadataOperations(IrClient client)
    {
        if (!_options.GenerateResponseMetadata)
            return [];

        var used = client.Operations.Select(operation => operation.MethodName).ToHashSet(StringComparer.Ordinal);
        used.Add(client.Name);
        used.Add("I" + client.Name);
        used.Add(client.Name + "TelemetryDecorator");
        foreach (var operation in client.Operations)
        {
            if (!used.Add(MetadataMethodName(operation)))
                throw new InvalidOperationException($"C# response metadata method '{MetadataMethodName(operation)}' conflicts with '{client.Name}'.");
        }

        return client.Operations;
    }

    /// <summary>Maps the ordinary success payload into the response-aware return type.</summary>
    /// <param name="success">The selected successful response contract.</param>
    /// <returns>A task returning response metadata and its primary payload type.</returns>
    private TypeNode BuildMetadataReturnType(IrResponse? success) =>
        new GenericTypeNode(new IdentifierNode("Task"),
        [
            new GenericTypeNode(new IdentifierNode("ApiResponse"), [MetadataPrimaryType(success)])
        ]);

    /// <summary>Chooses the preferred success body type for direct access on the response.</summary>
    /// <param name="success">The selected successful response contract.</param>
    /// <returns>The preferred type, or an object when no success body is declared.</returns>
    private TypeNode MetadataPrimaryType(IrResponse? success) => success?.Type is { } type
        ? BuildType(UnwrapNullableOrOptional(type))
        : BuildSimpleType("object");

    /// <summary>Builds one request dispatch with the complete set of declared response representations.</summary>
    /// <param name="operation">The operation being emitted.</param>
    /// <param name="success">The preferred successful response and body type.</param>
    /// <param name="requestName">The allocated local receiving the HTTP request.</param>
    /// <returns>The statement awaiting the single HTTP call and status-specific body decode.</returns>
    private StatementNode BuildMetadataSend(IrOperation operation, IrResponse? success, string requestName)
    {
        var arguments = new List<ExpressionNode>
        {
            new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("this")), new IdentifierNode("_httpClient")),
            new IdentifierExpressionNode(new IdentifierNode(requestName)),
            new LiteralExpressionNode(_options.ResponseMetadataMaxBodyBytes.ToString(CultureInfo.InvariantCulture), LiteralKind.Number),
            new IdentifierExpressionNode(new IdentifierNode("cancellationToken")),
        };

        foreach (var (status, mediaType, bodyType) in MetadataRepresentations(operation))
        {
            arguments.Add(new CallExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("ApiResponseContract")),
                [
                    new LiteralExpressionNode(status.ToString(CultureInfo.InvariantCulture), LiteralKind.Number),
                    mediaType is null ? new LiteralExpressionNode(null, LiteralKind.Null) : new LiteralExpressionNode(mediaType, LiteralKind.String),
                    bodyType is null ? new LiteralExpressionNode(null, LiteralKind.Null) : new TypeOfExpressionNode(BuildType(UnwrapNullableOrOptional(bodyType))),
                ],
                isNewExpression: true));
        }

        var call = new CallExpressionNode(
            new MemberAccessExpressionNode(new IdentifierExpressionNode(new IdentifierNode("ApiResponseReader")), new IdentifierNode("SendAsync")),
            arguments,
            genericTypeArgument: MetadataPrimaryType(success));
        return new ReturnStatementNode(new AwaitExpressionNode(call));
    }

    /// <summary>Projects explicit status and media contracts from the original IR response declarations.</summary>
    /// <remarks>Negative status values represent ranges, such as -2 for 2XX. A declaration without a schema on a nonempty media type is unconfirmed.</remarks>
    /// <param name="operation">The operation whose response contracts are projected.</param>
    /// <returns>One row for each supported media type or declared absence, including exact unsupported statuses to block range fallback.</returns>
    private static IEnumerable<(int Status, string? MediaType, IrType? Type)> MetadataRepresentations(IrOperation operation)
    {
        foreach (var response in operation.Responses)
        {
            int status;
            if (response.StatusCode.Length == 3 && response.StatusCode[0] is >= '1' and <= '5'
                && response.StatusCode[1..].Equals("XX", StringComparison.OrdinalIgnoreCase))
            {
                status = -(response.StatusCode[0] - '0');
            }
            else if (!int.TryParse(response.StatusCode, NumberStyles.None, CultureInfo.InvariantCulture, out status)
                || status is < 100 or > 599)
            {
                continue;
            }

            if (operation.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
                || status is 204 or 205 or 304)
            {
                yield return (status, null, null);
                continue;
            }

            if (response.MediaTypes.Count > 0)
            {
                foreach (var media in response.MediaTypes)
                    yield return (status, NormalizeMetadataMediaType(media.Name), IsJsonMediaType(media.Name) ? media.Type : null);
                continue;
            }

            if (response.Type is null && response.ContentTypes.Count == 0)
            {
                yield return (status, null, null);
                continue;
            }

            foreach (var media in response.ContentTypes.DefaultIfEmpty("application/json"))
                yield return (status, NormalizeMetadataMediaType(media), IsJsonMediaType(media) ? response.Type : null);
        }
    }

    /// <summary>Normalizes a concrete OpenAPI representation to its HTTP media type.</summary>
    /// <param name="name">The media key with any parameters specified in the source.</param>
    /// <returns>The lowercase type and subtype used for response matching.</returns>
    private static string NormalizeMetadataMediaType(string name) => name.Split(';', 2)[0].Trim().ToLowerInvariant();
}
