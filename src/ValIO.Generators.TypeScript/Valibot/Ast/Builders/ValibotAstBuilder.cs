using System.Collections.Immutable;
using System.Globalization;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Valibot.Ast.Builders;

/// <summary>
/// Builds Valibot schema expression AST from IR types.
/// </summary>
public sealed class ValibotAstBuilder
{
    private readonly string _uuidFormat;

    public ValibotAstBuilder(string? uuidFormat = null)
    {
        _uuidFormat = TypeScriptOptionIds.UuidFormat.Resolve(uuidFormat);
    }

    public ExpressionNode BuildSchemaFromIrModel(IrModel model)
    {
        if (model.Composition != null && model.Composition.Types.Count > 0)
        {
            return model.Composition.Kind switch
            {
                IrCompositionKind.OneOf or IrCompositionKind.AnyOf => BuildUnion(model.Composition.Types, model.Name),
                IrCompositionKind.AllOf => BuildModelIntersection(model),
                _ => BuildObject(model)
            };
        }

        return BuildObject(model);
    }

    public ExpressionNode BuildEnumSchema(IrEnum enumModel)
    {
        return Call("picklist", Id($"{enumModel.Name}Values"));
    }

    public ExpressionNode BuildSchemaFromIrType(IrType irType, string? currentModelName = null)
    {
        return irType switch
        {
            IrPrimitiveType primitive => BuildPrimitive(primitive.PrimitiveKind),
            IrModelReference reference => BuildModelReference(reference, currentModelName),
            IrArrayType array => Call("array", BuildSchemaFromIrType(array.ElementType, currentModelName)),
            IrDictionaryType dictionary => Call(
                "record",
                BuildSchemaFromIrType(dictionary.KeyType, currentModelName),
                BuildSchemaFromIrType(dictionary.ValueType, currentModelName)),
            IrUnionType union => BuildUnion(union.Types, currentModelName),
            IrIntersectionType intersection => BuildIntersection(intersection.Types, currentModelName),
            IrNullableType nullable => Call("nullable", BuildSchemaFromIrType(nullable.InnerType, currentModelName)),
            IrOptionalType optional => Call("optional", BuildSchemaFromIrType(optional.InnerType, currentModelName)),
            _ => Call("unknown")
        };
    }

    private ExpressionNode BuildObject(IrModel model)
    {
        var properties = model.Properties
            .Select(property => new PropertyAssignmentNode(
                new IdentifierNode(property.Name),
                BuildPropertySchema(property, model.Name)))
            .ToImmutableList();

        return Call("object", new ObjectLiteralExpressionNode(properties, preferMultilineFormat: true));
    }

    private ExpressionNode BuildPropertySchema(IrProperty property, string currentModelName)
    {
        var type = property.Type;
        var isNullable = false;

        if (type is IrNullableType nullable)
        {
            type = nullable.InnerType;
            isNullable = true;
        }

        var schema = type is IrPrimitiveType primitive
            ? BuildPrimitive(primitive.PrimitiveKind, property.Constraints)
            : BuildSchemaFromIrType(type, currentModelName);

        return !property.IsRequired || isNullable
            ? Call("nullish", schema)
            : schema;
    }

    private ExpressionNode BuildModelIntersection(IrModel model)
    {
        var schemas = model.Composition!.Types
            .Select(type => BuildSchemaFromIrType(type, model.Name))
            .ToList();

        if (model.Properties.Count > 0)
            schemas.Add(BuildObject(model));

        return BuildIntersection(schemas);
    }

    private ExpressionNode BuildUnion(IReadOnlyList<IrType> types, string? currentModelName)
    {
        var schemas = types
            .Select(type => BuildSchemaFromIrType(type, currentModelName))
            .ToImmutableList();

        return schemas.Count == 1
            ? schemas[0]
            : Call("union", new ArrayLiteralExpressionNode(schemas));
    }

    private ExpressionNode BuildIntersection(IReadOnlyList<IrType> types, string? currentModelName)
    {
        var schemas = types
            .Select(type => BuildSchemaFromIrType(type, currentModelName))
            .ToList();

        return BuildIntersection(schemas);
    }

    private static ExpressionNode BuildIntersection(IReadOnlyList<ExpressionNode> schemas)
    {
        return schemas.Count switch
        {
            0 => Call("unknown"),
            1 => schemas[0],
            _ => Call("intersect", new ArrayLiteralExpressionNode(schemas.ToImmutableList()))
        };
    }

    private static ExpressionNode BuildModelReference(IrModelReference reference, string? currentModelName)
    {
        var schemaName = $"{reference.ModelName}Schema";
        var isSelfReference = !string.IsNullOrEmpty(currentModelName)
            && string.Equals(reference.ModelName, currentModelName, StringComparison.OrdinalIgnoreCase);

        return isSelfReference
            ? Call("lazy", new ArrowFunctionExpressionNode(ImmutableList<ParameterDeclarationNode>.Empty, Id(schemaName)))
            : Id(schemaName);
    }

    private ExpressionNode BuildPrimitive(IrPrimitiveKind kind, IrPropertyConstraints? constraints = null)
    {
        var baseSchema = kind switch
        {
            IrPrimitiveKind.String => Call("string"),
            IrPrimitiveKind.Number => Call("number"),
            IrPrimitiveKind.Integer => Pipe(Call("number"), Call("integer")),
            IrPrimitiveKind.Long => Pipe(Call("number"), Call("integer")),
            IrPrimitiveKind.Boolean => Call("boolean"),
            IrPrimitiveKind.DateTime => Pipe(Call("string"), Call("isoDateTime")),
            IrPrimitiveKind.Date => Pipe(Call("string"), Call("isoDate")),
            IrPrimitiveKind.Time => Pipe(Call("string"), Call("isoTime")),
            IrPrimitiveKind.Uuid => BuildUuidSchema(),
            IrPrimitiveKind.Any => Call("unknown"),
            IrPrimitiveKind.Void => Call("undefined"),
            IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File => Call("unknown"),
            _ => Call("unknown")
        };

        return ApplyConstraints(baseSchema, kind, constraints);
    }

    /// <summary>
    /// UUID schema per configured mode: guid → v.uuid(); string → v.string();
    /// regex → v.pipe(v.string(), v.regex(UUID_REGEX)).
    /// </summary>
    private ExpressionNode BuildUuidSchema()
    {
        return _uuidFormat switch
        {
            TypeScriptOptionIds.UuidFormat.String => Call("string"),
            TypeScriptOptionIds.UuidFormat.Regex => Pipe(Call("string"), Call("regex", Id(TypeScriptOptionIds.UuidFormat.RegexConstName))),
            _ => Pipe(Call("string"), Call("uuid")),
        };
    }

    private static ExpressionNode ApplyConstraints(ExpressionNode schema, IrPrimitiveKind kind, IrPropertyConstraints? constraints)
    {
        if (constraints == null)
            return schema;

        var actions = new List<ExpressionNode>();
        var isNumeric = kind is IrPrimitiveKind.Number or IrPrimitiveKind.Integer or IrPrimitiveKind.Long;
        var isString = kind is IrPrimitiveKind.String or IrPrimitiveKind.Uuid or IrPrimitiveKind.DateTime
            or IrPrimitiveKind.Date or IrPrimitiveKind.Time;

        if (isNumeric)
        {
            if (constraints.Min.HasValue)
                actions.Add(Call("minValue", Number(constraints.Min.Value)));
            if (constraints.Max.HasValue)
                actions.Add(Call("maxValue", Number(constraints.Max.Value)));
            if (constraints.ExclusiveMin.HasValue)
                actions.Add(Call("gtValue", Number(constraints.ExclusiveMin.Value)));
            if (constraints.ExclusiveMax.HasValue)
                actions.Add(Call("ltValue", Number(constraints.ExclusiveMax.Value)));
        }
        else if (isString)
        {
            if (constraints.MinLength.HasValue)
                actions.Add(Call("minLength", Number(constraints.MinLength.Value)));
            if (constraints.MaxLength.HasValue)
                actions.Add(Call("maxLength", Number(constraints.MaxLength.Value)));
            if (!string.IsNullOrEmpty(constraints.Pattern))
            {
                var regex = new CallExpressionNode(
                    Id("RegExp"),
                    new[] { StringLiteral(constraints.Pattern) }.ToImmutableList(),
                    isNewExpression: true);
                actions.Add(Call("regex", regex));
            }
        }

        return actions.Count == 0
            ? schema
            : Pipe([schema, .. actions]);
    }

    private static ExpressionNode Pipe(params ExpressionNode[] expressions)
    {
        return expressions.Length == 1
            ? expressions[0]
            : Call("pipe", expressions);
    }

    private static CallExpressionNode Call(string methodName, params ExpressionNode[] arguments)
    {
        return new CallExpressionNode(
            new MemberAccessExpressionNode(Id("v"), new IdentifierNode(methodName)),
            arguments.ToImmutableList());
    }

    private static IdentifierExpressionNode Id(string name)
        => new(new IdentifierNode(name));

    private static LiteralExpressionNode StringLiteral(string value)
        => new(value, LiteralKind.String);

    private static LiteralExpressionNode Number(int value)
        => new(value, LiteralKind.Number);

    private static LiteralExpressionNode Number(decimal value)
        => new(value.ToString(CultureInfo.InvariantCulture), LiteralKind.Number);
}
