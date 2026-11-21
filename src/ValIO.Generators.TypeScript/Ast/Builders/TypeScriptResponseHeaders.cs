using System.Text.Json;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>Builds the shared response header result shape and copies native headers without transport objects.</summary>
internal static class TypeScriptResponseHeaders
{
    /// <summary>Declares the normalized header map on a shared result variant.</summary>
    /// <param name="declaredHeaders">The headers named by the documented status, when available.</param>
    /// <returns>A type signature that also admits transports that do not expose headers.</returns>
    internal static PropertySignatureNode Signature(IReadOnlyList<IrResponseHeader>? declaredHeaders = null)
    {
        TypeNode headerType = RecordType();
        if (declaredHeaders is { Count: > 0 })
        {
            // A declared header may still be unavailable to browser code, for example after CORS filtering.
            var namedHeaders = declaredHeaders.Select(header => header.Name.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Select(name => new TypeLiteralMember(JsonSerializer.Serialize(name), Primitive("string"), isOptional: true))
                .ToArray();
            headerType = new IntersectionTypeNode([headerType, new TypeLiteralNode(namedHeaders)]);
        }

        return new PropertySignatureNode(new IdentifierNode("headers"), new GenericTypeNode(new IdentifierNode("Readonly"), [headerType]),
            isRequired: false,
            documentation: new CommentNode("Received HTTP headers with lowercase names, when exposed by the transport."));
    }

    /// <summary>Adds the collected response headers to a returned object.</summary>
    /// <param name="result">The transport result object.</param>
    /// <param name="enabled">Whether the configured transport exposes headers.</param>
    /// <returns>The result with its original properties and, if requested, a header map.</returns>
    internal static ObjectLiteralExpressionNode Attach(ObjectLiteralExpressionNode result, bool enabled) =>
        enabled ? new ObjectLiteralExpressionNode([.. result.Properties, new(new IdentifierNode("headers"), Id("responseHeaders"))]) : result;

    /// <summary>Collects native fetch headers into a plain object that can cross a Next.js server boundary.</summary>
    /// <param name="headers">The native fetch header collection.</param>
    /// <returns>The statements creating and populating the result's header map.</returns>
    internal static IReadOnlyList<StatementNode> ReadFetch(ExpressionNode headers) =>
    [
        CreateMap(),
        new ExpressionStatementNode(Call(Member(headers, "forEach"),
            new ArrowFunctionExpressionNode(
                [new(new IdentifierNode("value"), type: null), new(new IdentifierNode("name"), type: null)],
                Write(Id("name"), Id("value")))))
    ];

    /// <summary>Collects Axios headers with lowercase names and string values, omitting the native unset values.</summary>
    /// <param name="headers">The native Axios header collection or a custom client's raw header object.</param>
    /// <returns>The statements creating and populating the result's header map.</returns>
    internal static IReadOnlyList<StatementNode> ReadAxios(ExpressionNode headers)
    {
        var value = Id("value");
        var name = Id("name");
        var hasValue = new BinaryExpressionNode(
            new BinaryExpressionNode(value, BinaryOperator.NotEqual, new LiteralExpressionNode(null, LiteralKind.Null)),
            BinaryOperator.LogicalAnd,
            new BinaryExpressionNode(value, BinaryOperator.NotEqual, Id("undefined")));
        var isPresent = new BinaryExpressionNode(hasValue, BinaryOperator.LogicalAnd,
            new BinaryExpressionNode(value, BinaryOperator.NotEqual, new LiteralExpressionNode(false, LiteralKind.Boolean)));
        var wireValue = new ConditionalExpressionNode(
            Call(Member(Id("Array"), "isArray"), value),
            Call(Member(value, "join"), new LiteralExpressionNode(", ", LiteralKind.String)),
            Call(Id("String"), value));
        var read = new ArrowFunctionExpressionNode([new(new IdentifierNode("name"), type: null)], new BlockNode([
            new VariableDeclarationStatementNode(new IdentifierNode("value"), Primitive("unknown"),
                Call(Member(Id("Reflect"), "get"), headers, name)),
            new IfStatementNode(isPresent, new BlockNode([
                new ExpressionStatementNode(Write(Call(Member(name, "toLowerCase")), wireValue))
            ]))
        ]));

        return [CreateMap(), new ExpressionStatementNode(Call(Member(Call(Member(Id("Object"), "keys"), headers), "forEach"), read))];
    }

    /// <summary>Builds a record type that represents missing names as undefined.</summary>
    /// <returns>The general string-keyed map type shared by the response transports.</returns>
    private static GenericTypeNode RecordType() => new(new IdentifierNode("Record"),
        [Primitive("string"), new UnionTypeNode([Primitive("string"), Primitive("undefined")])]);

    /// <summary>Initializes the result map before entries are copied from the transport.</summary>
    /// <returns>The local declaration of the plain header map.</returns>
    private static VariableDeclarationStatementNode CreateMap() =>
        new(new IdentifierNode("responseHeaders"), RecordType(), new ObjectLiteralExpressionNode([]));

    /// <summary>Writes an own property even if a header name coincides with an inherited object property.</summary>
    /// <param name="name">The normalized header name.</param>
    /// <param name="value">The string value of that header.</param>
    /// <returns>The expression adding an enumerable property to the plain header map.</returns>
    private static CallExpressionNode Write(ExpressionNode name, ExpressionNode value) =>
        Call(Member(Id("Object"), "defineProperty"), Id("responseHeaders"), name,
            new ObjectLiteralExpressionNode([
                new(new IdentifierNode("value"), value),
                new(new IdentifierNode("enumerable"), new LiteralExpressionNode(true, LiteralKind.Boolean)),
                new(new IdentifierNode("configurable"), new LiteralExpressionNode(true, LiteralKind.Boolean))
            ]));

    /// <summary>Builds a TypeScript primitive type.</summary>
    /// <param name="name">The primitive keyword.</param>
    /// <returns>The primitive type node.</returns>
    private static SimpleTypeNode Primitive(string name) => new(new IdentifierNode(name), isPrimitive: true);

    /// <summary>References a generated local or JavaScript global.</summary>
    /// <param name="name">The identifier to reference.</param>
    /// <returns>The identifier expression.</returns>
    private static IdentifierExpressionNode Id(string name) => new(new IdentifierNode(name));

    /// <summary>References a property of a runtime expression.</summary>
    /// <param name="target">The runtime object expression.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property access expression.</returns>
    private static MemberAccessExpressionNode Member(ExpressionNode target, string name) => new(target, new IdentifierNode(name));

    /// <summary>Invokes a generated JavaScript function.</summary>
    /// <param name="target">The callable expression.</param>
    /// <param name="arguments">The arguments to pass.</param>
    /// <returns>The call expression.</returns>
    private static CallExpressionNode Call(ExpressionNode target, params ExpressionNode[] arguments) => new(target, arguments);
}
