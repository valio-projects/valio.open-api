using System.Globalization;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using static ValIO.Generators.TypeScript.Validation.ValidationAst;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>Connects exact OpenAPI JSON numbers to the lossless-json runtime.</summary>
internal static class TypeScriptExactJson
{
    /// <summary>The local namespace used by the generated runtime calls.</summary>
    private const string RuntimeName = "valioLosslessJson";

    /// <summary>The npm package implementing the numeric token parser and writer.</summary>
    public const string PackageName = "lossless-json";

    /// <summary>The runtime import for schema source files assembled as text.</summary>
    public const string ImportLine = """import * as valioLosslessJson from "lossless-json";""";

    /// <summary>The qualified type usable by generated DTOs without a runtime import.</summary>
    public const string LosslessNumberType = "import(\"lossless-json\").LosslessNumber";

    /// <summary>Recognizes numeric formats whose tokens must survive JavaScript conversion.</summary>
    /// <param name="type">The original scalar IR type.</param>
    /// <returns>Whether the scalar requires exact representation.</returns>
    public static bool IsExact(IrPrimitiveType type) =>
        type.PrimitiveKind == IrPrimitiveKind.Number && type.Format == "decimal"
        || type.PrimitiveKind == IrPrimitiveKind.Long && type.Format is "int64" or "uint64";

    /// <summary>Checks whether a schema expression embeds a numeric codec call.</summary>
    /// <param name="type">The schema's local type tree. Referenced schemas import their own codecs.</param>
    /// <returns>Whether the embedding file needs the codec import.</returns>
    public static bool UsesNumberCodec(IrType type) => type switch
    {
        IrPrimitiveType p => p.PrimitiveKind is IrPrimitiveKind.Number or IrPrimitiveKind.Integer or IrPrimitiveKind.Long,
        IrOptionalType optional => UsesNumberCodec(optional.InnerType),
        IrNullableType nullable => UsesNumberCodec(nullable.InnerType),
        IrArrayType array => UsesNumberCodec(array.ElementType),
        IrDictionaryType dictionary => UsesNumberCodec(dictionary.KeyType) || UsesNumberCodec(dictionary.ValueType),
        IrUnionType union => union.Types.Any(UsesNumberCodec),
        IrIntersectionType intersection => intersection.Types.Any(UsesNumberCodec),
        _ => false,
    };

    /// <summary>Finds the scalar primitive underneath nullability and presence wrappers.</summary>
    /// <param name="type">The parameter or property type.</param>
    /// <returns>The exact scalar type, or null for other shapes.</returns>
    public static IrPrimitiveType? ExactPrimitive(IrType type) => type switch
    {
        IrPrimitiveType p when IsExact(p) => p,
        IrOptionalType optional => ExactPrimitive(optional.InnerType),
        IrNullableType nullable => ExactPrimitive(nullable.InnerType),
        _ => null,
    };

    /// <summary>Converts an exact scalar parameter to its textual HTTP representation.</summary>
    /// <param name="type">The parameter schema.</param>
    /// <param name="value">The optional parameter value.</param>
    /// <returns>A scalar text expression or the existing expression for other types.</returns>
    public static ExpressionNode TextParameter(IrType type, ExpressionNode value)
    {
        if (ExactPrimitive(type) != null)
            return Call(new MemberAccessExpressionNode(value, new IdentifierNode("toString"), useOptionalChaining: true));

        return Unwrap(type) is IrArrayType array && ExactPrimitive(array.ElementType) != null
            ? Call(new MemberAccessExpressionNode(value, new IdentifierNode("map"), useOptionalChaining: true),
                Lambda(TextParameter(array.ElementType, Id("value"))))
            : value;
    }

    /// <summary>Rejects operation contracts whose exact values cannot be decoded from a JSON response.</summary>
    /// <param name="document">The finalized schema and model declarations.</param>
    /// <param name="transports">The generated transport names.</param>
    /// <exception cref="InvalidOperationException">A selected client has an unsupported exact numeric wire shape.</exception>
    public static void ValidateOperations(IrDocumentContext document, IReadOnlyList<string> transports)
    {
        foreach (var enumType in document.Document.Enums.Where(enumType =>
            enumType.BaseType == IrEnumBaseType.Integer && enumType.OriginalSchema?.Format is "int64" or "uint64"))
            throw new InvalidOperationException(
                $"Option 'exactJsonNumbers' cannot generate '{enumType.OriginalName}': exact numeric enums are not supported.");

        foreach (var operation in document.Document.Clients.SelectMany(client => client.Operations))
        {
            if (operation.RequestBody is { IsJson: false } body && HasExact(body.Type, document, []))
                throw Unsupported(operation, "exact numeric request body fields require application/json");

            foreach (var parameter in operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters))
            {
                if (HasExact(parameter.Type, document, [])
                    && ExactPrimitive(parameter.Type) == null
                    && (Unwrap(parameter.Type) is not IrArrayType array || ExactPrimitive(array.ElementType) == null))
                    throw Unsupported(operation, $"parameter '{parameter.OriginalName}' requires a scalar or array of exact numbers");
            }

            if (transports.Contains("axios")
                && operation.Responses.Any(Axios.Ast.Builders.AxiosAstBuilder.IsBinarySuccessResponse)
                && operation.Responses.Any(response => response.Type != null && HasExact(response.Type, document, [])))
                throw Unsupported(operation, "Axios cannot decode exact JSON and binary on the same operation");
        }
    }

    /// <summary>Finds exact numeric fields below a body, composed type, or referenced model.</summary>
    /// <param name="type">The type to search.</param>
    /// <param name="document">The model declarations shared by the current SDK.</param>
    /// <param name="visited">The model names already expanded during this search.</param>
    /// <returns>Whether the type contains an exact numeric scalar.</returns>
    private static bool HasExact(IrType type, IrDocumentContext document, HashSet<string> visited) => Unwrap(type) switch
    {
        IrPrimitiveType primitive => IsExact(primitive),
        IrArrayType array => HasExact(array.ElementType, document, visited),
        IrDictionaryType dictionary => HasExact(dictionary.ValueType, document, visited),
        IrUnionType union => union.Types.Any(variant => HasExact(variant, document, visited)),
        IrIntersectionType intersection => intersection.Types.Any(variant => HasExact(variant, document, visited)),
        IrModelReference reference when visited.Add(reference.ModelName)
            && document.ModelsByName.TryGetValue(reference.ModelName, out var model) =>
            model.Properties.Any(property => HasExact(property.Type, document, visited))
            || model.Composition?.Types.Any(variant => HasExact(variant, document, visited)) == true,
        _ => false,
    };

    /// <summary>Resolves the value type after removing nullability and presence wrappers.</summary>
    /// <param name="type">The wrapped value type.</param>
    /// <returns>The underlying type.</returns>
    private static IrType Unwrap(IrType type) => type switch
    {
        IrOptionalType optional => Unwrap(optional.InnerType),
        IrNullableType nullable => Unwrap(nullable.InnerType),
        _ => type,
    };

    /// <summary>Creates an actionable error referencing the responsible OpenAPI operation.</summary>
    /// <param name="operation">The operation that exceeds the exact codec's scope.</param>
    /// <param name="reason">The unsupported wire shape.</param>
    /// <returns>The generation error.</returns>
    private static InvalidOperationException Unsupported(IrOperation operation, string reason) =>
        new($"Option 'exactJsonNumbers' cannot generate '{operation.OriginalMethodName}': {reason}.");

    /// <summary>Imports the codec and predicates used by generated runtime code.</summary>
    /// <returns>The package namespace import.</returns>
    public static ImportNode Import() => new([new IdentifierNode(RuntimeName)], PackageName, ImportKind.Namespace);

    /// <summary>Builds a reference to a codec method.</summary>
    /// <param name="name">The exported package member.</param>
    /// <returns>The qualified member access.</returns>
    public static ExpressionNode Runtime(string name) => Member(Id(RuntimeName), name);

    /// <summary>Decodes original JSON text before native fetch can round its numeric tokens.</summary>
    /// <param name="response">The native fetch response.</param>
    /// <returns>The exact JSON decoder for the response body.</returns>
    public static ExpressionNode DecodeFetchResponse(ExpressionNode response) =>
        Call(Runtime("parse"), new AwaitExpressionNode(Call(Member(response, "text"))));

    /// <summary>Replaces Axios's JSON writer and supplies the JSON content type when absent.</summary>
    /// <returns>The Axios request transformer.</returns>
    public static ExpressionNode AxiosRequestTransform() =>
        new ArrowFunctionExpressionNode(AxiosTransformParameters(), new BlockNode([
            new ExpressionStatementNode(Call(Member(Id("headers"), "setContentType"), String("application/json"),
                new LiteralExpressionNode(false, LiteralKind.Boolean))),
            new ReturnStatementNode(Call(Runtime("stringify"), Id("data"))),
        ]));

    /// <summary>Decodes JSON from Axios's text response and preserves non-JSON error bodies.</summary>
    /// <returns>The Axios response transformer.</returns>
    public static ExpressionNode AxiosResponseTransform()
    {
        var data = Id("data");
        var contentType = new BinaryExpressionNode(Call(Member(Id("headers"), "getContentType")),
            BinaryOperator.NullishCoalescing, String("application/json"));
        var mediaType = Call(Member(Call(Id("String"), contentType), "toLowerCase"));
        var isJson = Call(Member(mediaType, "includes"), String("json"));
        return new ArrowFunctionExpressionNode(AxiosTransformParameters(), new ConditionalExpressionNode(
            And(data, isJson), Call(Runtime("parse"), data), data));
    }

    /// <summary>Declares contextually typed parameters shared by Axios transformers.</summary>
    /// <returns>The native data and headers transformer arguments.</returns>
    private static IReadOnlyList<ParameterDeclarationNode> AxiosTransformParameters() =>
        [new(new IdentifierNode("data"), type: null, isRequired: true),
         new(new IdentifierNode("headers"), type: null, isRequired: true)];

    /// <summary>Builds the exact numeric schema, preserving bounds as textual operands.</summary>
    /// <param name="type">The scalar OpenAPI format and primitive kind.</param>
    /// <param name="constraints">The inclusive and exclusive numeric bounds, if present.</param>
    /// <returns>A Zod schema that accepts validated numeric tokens.</returns>
    public static ExpressionNode ExactSchema(IrPrimitiveType type, IrPropertyConstraints? constraints)
    {
        var value = Id("value");
        var text = Member(value, "value");
        var isInt64 = type.PrimitiveKind == IrPrimitiveKind.Long;
        var schema = new CallExpressionNode(Member(Id("z"), "custom"), [Runtime("isLosslessNumber"),
            String("Expected an exact JSON number.")], genericTypeName: LosslessNumberType);
        ExpressionNode valid = Call(Runtime("isNumber"), text);
        if (isInt64)
        {
            var unsigned = type.Format == "uint64";
            valid = And(valid, Call(Runtime("isInteger"), text));
            valid = And(valid, Compare(text, unsigned ? "0" : "-9223372036854775808", BinaryOperator.GreaterThanOrEqual));
            valid = And(valid, Compare(text, unsigned ? "18446744073709551615" : "9223372036854775807", BinaryOperator.LessThanOrEqual));
        }

        if (constraints?.Min is { } min)
            valid = And(valid, Compare(text, Format(min), BinaryOperator.GreaterThanOrEqual));
        if (constraints?.Max is { } max)
            valid = And(valid, Compare(text, Format(max), BinaryOperator.LessThanOrEqual));
        if (constraints?.ExclusiveMin is { } exclusiveMin)
            valid = And(valid, Compare(text, Format(exclusiveMin), BinaryOperator.GreaterThan));
        if (constraints?.ExclusiveMax is { } exclusiveMax)
            valid = And(valid, Compare(text, Format(exclusiveMax), BinaryOperator.LessThan));

        return Call(Member(schema, "refine"), Lambda(valid), String("Number is outside the declared format or bounds."));
    }

    /// <summary>Unboxes parsed numeric tokens only at ordinary number schemas.</summary>
    /// <param name="schema">The ordinary Zod number or numeric enum schema.</param>
    /// <param name="integer">Whether fractional tokens that round to an integer must be rejected.</param>
    /// <returns>The schema composed with a numeric token preprocessor.</returns>
    public static ExpressionNode OrdinaryNumberSchema(ExpressionNode schema, bool integer)
    {
        var value = Id("value");
        var text = Member(value, "value");
        ExpressionNode converted = Call(Id("Number"), text);
        if (integer)
        {
            var isInteger = Call(Member(Id("Number"), "isSafeInteger"), converted);
            var unchanged = new BinaryExpressionNode(Call(Runtime("compareNumber"), text, Call(Id("String"), converted)),
                BinaryOperator.Equal, new LiteralExpressionNode(0, LiteralKind.Number));
            converted = new ConditionalExpressionNode(And(isInteger, unchanged), converted, value);
        }

        return Call(Member(Id("z"), "preprocess"), Lambda(new ConditionalExpressionNode(
            Call(Runtime("isLosslessNumber"), value), converted, value)), schema);
    }

    /// <summary>Builds a contextual one-argument function for a Zod predicate or preprocessor.</summary>
    /// <param name="expression">The function result.</param>
    /// <returns>The arrow function.</returns>
    private static ArrowFunctionExpressionNode Lambda(ExpressionNode expression) =>
        new([new ParameterDeclarationNode(new IdentifierNode("value"), type: null, isRequired: true)], expression);

    /// <summary>Builds a comparison without converting either operand to a JavaScript number.</summary>
    /// <param name="value">The token under validation.</param>
    /// <param name="bound">The exact OpenAPI bound.</param>
    /// <param name="comparison">The inclusive or exclusive comparison.</param>
    /// <returns>The comparison of the package comparator result against zero.</returns>
    private static ExpressionNode Compare(ExpressionNode value, string bound, BinaryOperator comparison) =>
        new BinaryExpressionNode(Call(Runtime("compareNumber"), value, String(bound)), comparison,
            new LiteralExpressionNode(0, LiteralKind.Number));

    /// <summary>Combines predicates with short-circuit evaluation.</summary>
    /// <param name="left">The prerequisite predicate.</param>
    /// <param name="right">The next predicate.</param>
    /// <returns>The combined predicate.</returns>
    private static ExpressionNode And(ExpressionNode left, ExpressionNode right) =>
        new BinaryExpressionNode(left, BinaryOperator.LogicalAnd, right);

    /// <summary>Builds a string literal.</summary>
    /// <param name="value">The exact string content.</param>
    /// <returns>The literal AST node.</returns>
    private static LiteralExpressionNode String(string value) => new(value, LiteralKind.String);

    /// <summary>Formats an IR numeric bound without a floating-point conversion.</summary>
    /// <param name="value">The decimal parsed from the spec.</param>
    /// <returns>The invariant decimal token.</returns>
    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
