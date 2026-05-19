using System.Collections.Immutable;
using System.Globalization;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.TypeBox.Ast.Builders;

/// <summary>
/// Builds TypeBox schema expression AST from IR types.
/// </summary>
public sealed class TypeBoxAstBuilder
{
    public ExpressionNode BuildSchemaFromIrModel(IrModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.Composition != null && model.Composition.Types.Count > 0)
        {
            var expressions = model.Composition.Types
                .Select(type => BuildSchemaFromIrType(type, model.Name))
                .ToList();

            if (model.Properties.Count > 0)
                expressions.Add(BuildObjectSchema(model));

            return model.Composition.Kind switch
            {
                IrCompositionKind.AllOf => BuildIntersection(expressions),
                IrCompositionKind.OneOf or IrCompositionKind.AnyOf => BuildUnion(expressions),
                _ => BuildObjectSchema(model)
            };
        }

        return BuildObjectSchema(model);
    }

    public ExpressionNode BuildEnumSchema(IrEnum enumModel)
    {
        ArgumentNullException.ThrowIfNull(enumModel);

        var literals = enumModel.Values
            .Select(value => Call("Literal", BuildLiteral(value.Value)))
            .ToImmutableList<ExpressionNode>();

        return BuildUnion(literals);
    }

    public ExpressionNode BuildSchemaFromIrType(
        IrType irType,
        string? currentModelName = null,
        IrPropertyConstraints? constraints = null)
    {
        return irType switch
        {
            IrPrimitiveType primitive => BuildPrimitive(primitive.PrimitiveKind, constraints),
            IrModelReference reference => Id($"{reference.ModelName}Schema"),
            IrArrayType array => Call("Array", BuildSchemaFromIrType(array.ElementType, currentModelName)),
            IrDictionaryType dictionary => Call(
                "Record",
                BuildSchemaFromIrType(dictionary.KeyType, currentModelName),
                BuildSchemaFromIrType(dictionary.ValueType, currentModelName)),
            IrNullableType nullable => BuildUnion([
                BuildSchemaFromIrType(nullable.InnerType, currentModelName, constraints),
                Call("Null")
            ]),
            IrOptionalType optional => Call("Optional", BuildSchemaFromIrType(optional.InnerType, currentModelName, constraints)),
            IrUnionType union => BuildUnion(union.Types.Select(type => BuildSchemaFromIrType(type, currentModelName, constraints))),
            IrIntersectionType intersection => BuildIntersection(intersection.Types.Select(type => BuildSchemaFromIrType(type, currentModelName, constraints))),
            _ => Call("Unknown")
        };
    }

    private ExpressionNode BuildObjectSchema(IrModel model)
    {
        var properties = model.Properties
            .Select(property => new PropertyAssignmentNode(
                name: new IdentifierNode(property.Name),
                value: BuildPropertySchema(property, model.Name)))
            .ToImmutableList();

        return Call("Object", new ObjectLiteralExpressionNode(properties, preferMultilineFormat: true));
    }

    private ExpressionNode BuildPropertySchema(IrProperty property, string currentModelName)
    {
        var expression = BuildSchemaFromIrType(property.Type, currentModelName, property.Constraints);
        return property.IsRequired ? expression : Call("Optional", expression);
    }

    private static ExpressionNode BuildPrimitive(IrPrimitiveKind kind, IrPropertyConstraints? constraints)
    {
        return kind switch
        {
            IrPrimitiveKind.String => CallOptional("String", BuildStringOptions(null, constraints)),
            IrPrimitiveKind.Uuid => CallOptional("String", BuildStringOptions("uuid", constraints)),
            IrPrimitiveKind.Date => CallOptional("String", BuildStringOptions("date", constraints)),
            IrPrimitiveKind.DateTime => CallOptional("String", BuildStringOptions("date-time", constraints)),
            IrPrimitiveKind.Time => CallOptional("String", BuildStringOptions("time", constraints)),
            IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File => Call(
                "String",
                Object(("contentEncoding", String("base64")))),
            IrPrimitiveKind.Integer or IrPrimitiveKind.Long => CallOptional("Integer", BuildNumberOptions(constraints)),
            IrPrimitiveKind.Number => CallOptional("Number", BuildNumberOptions(constraints)),
            IrPrimitiveKind.Boolean => Call("Boolean"),
            IrPrimitiveKind.Void => Call("Void"),
            IrPrimitiveKind.Any => Call("Unknown"),
            _ => Call("Unknown")
        };
    }

    private static ExpressionNode BuildUnion(IEnumerable<ExpressionNode> expressions)
    {
        var schemas = expressions.ToImmutableList();

        return schemas.Count switch
        {
            0 => Call("Unknown"),
            1 => schemas[0],
            _ => Call("Union", new ArrayLiteralExpressionNode(schemas))
        };
    }

    private static ExpressionNode BuildIntersection(IEnumerable<ExpressionNode> expressions)
    {
        var schemas = expressions.ToImmutableList();

        return schemas.Count switch
        {
            0 => Call("Unknown"),
            1 => schemas[0],
            _ => Call("Intersect", new ArrayLiteralExpressionNode(schemas))
        };
    }

    private static ObjectLiteralExpressionNode? BuildStringOptions(string? format, IrPropertyConstraints? constraints)
    {
        var properties = ImmutableList.CreateBuilder<PropertyAssignmentNode>();

        if (!string.IsNullOrWhiteSpace(format))
            properties.Add(Property("format", String(format)));
        if (constraints?.MinLength is { } minLength)
            properties.Add(Property("minLength", Number(minLength)));
        if (constraints?.MaxLength is { } maxLength)
            properties.Add(Property("maxLength", Number(maxLength)));
        if (!string.IsNullOrWhiteSpace(constraints?.Pattern))
            properties.Add(Property("pattern", String(constraints.Pattern)));

        return properties.Count == 0
            ? null
            : new ObjectLiteralExpressionNode(properties.ToImmutableList());
    }

    private static ObjectLiteralExpressionNode? BuildNumberOptions(IrPropertyConstraints? constraints)
    {
        var properties = ImmutableList.CreateBuilder<PropertyAssignmentNode>();

        if (constraints?.Min is { } min)
            properties.Add(Property("minimum", Number(min)));
        if (constraints?.Max is { } max)
            properties.Add(Property("maximum", Number(max)));
        if (constraints?.ExclusiveMin is { } exclusiveMin)
            properties.Add(Property("exclusiveMinimum", Number(exclusiveMin)));
        if (constraints?.ExclusiveMax is { } exclusiveMax)
            properties.Add(Property("exclusiveMaximum", Number(exclusiveMax)));

        return properties.Count == 0
            ? null
            : new ObjectLiteralExpressionNode(properties.ToImmutableList());
    }

    private static ExpressionNode CallOptional(string methodName, ExpressionNode? options)
    {
        return options == null ? Call(methodName) : Call(methodName, options);
    }

    private static CallExpressionNode Call(string methodName, params ExpressionNode[] arguments)
    {
        return new CallExpressionNode(
            new MemberAccessExpressionNode(Id("Type"), new IdentifierNode(methodName)),
            arguments.ToImmutableList());
    }

    private static ObjectLiteralExpressionNode Object(params (string Name, ExpressionNode Value)[] properties)
    {
        return new ObjectLiteralExpressionNode(properties
            .Select(property => Property(property.Name, property.Value))
            .ToImmutableList());
    }

    private static PropertyAssignmentNode Property(string name, ExpressionNode value)
    {
        return new PropertyAssignmentNode(new IdentifierNode(name), value);
    }

    private static IdentifierExpressionNode Id(string name)
        => new(new IdentifierNode(name));

    private static LiteralExpressionNode BuildLiteral(object? value)
    {
        return value switch
        {
            null => new LiteralExpressionNode(null, LiteralKind.Null),
            string text => String(text),
            bool boolean => new LiteralExpressionNode(boolean, LiteralKind.Boolean),
            int or long or short or byte or decimal or double or float => Number(value),
            _ => String(value.ToString() ?? string.Empty)
        };
    }

    private static LiteralExpressionNode String(string value)
        => new(value, LiteralKind.String);

    private static LiteralExpressionNode Number(object value)
        => new(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0", LiteralKind.Number);
}
