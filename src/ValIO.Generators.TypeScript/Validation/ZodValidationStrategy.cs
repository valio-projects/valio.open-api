using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Ast.Builders;
using static ValIO.Generators.TypeScript.Validation.ValidationAst;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>Валидация через Zod: <c>Schema.parse(input)</c> / <c>z.union([...]).parse(input)</c>.</summary>
internal sealed class ZodValidationStrategy : IClientValidationStrategy
{
    private readonly ZodSchemaExpressionBuilder _schema;
    private readonly string _importPath;

    /// <summary>Whether inline Zod schemas decode numeric tokens before validating them.</summary>
    private readonly bool _exactJsonNumbers;

    /// <summary>Creates the validation strategy for a generated client's selected Zod version.</summary>
    /// <param name="schema">The inline expression builder.</param>
    /// <param name="importPath">The package import path for the selected Zod major version.</param>
    /// <param name="exactJsonNumbers">Whether numeric schemas use the lossless JSON codec.</param>
    public ZodValidationStrategy(ZodSchemaExpressionBuilder schema, string importPath, bool exactJsonNumbers = false)
    {
        _schema = schema;
        _importPath = importPath;
        _exactJsonNumbers = exactJsonNumbers;
    }

    public bool IsEnabled => true;

    /// <summary>Zod references the exported <c>…Constraints</c> constants for rule values.</summary>
    public bool UsesConstraintConstants => true;

    public string? UuidConstName => Options.TypeScriptOptionIds.UuidFormat.RegexConstName;

    public ExpressionNode Validate(IrType type, ExpressionNode input) =>
        Call(Member(_schema.Build(type), "parse"), input);

    public ExpressionNode BuildSchema(IrType type) => _schema.Build(type);

    public ExpressionNode BuildParamsSchema(IReadOnlyList<IrParameter> parameters, string? constraintsRefPrefix = null) =>
        _schema.BuildObjectFromParameters(parameters, constraintsRefPrefix);

    public ExpressionNode ValidateNamed(string schemaName, ExpressionNode input) =>
        Call(Member(Id(schemaName), "parse"), input);

    /// <inheritdoc />
    public IReadOnlyList<ImportNode> RuntimeImports(IReadOnlyCollection<IrType> inlineConstructedTypes, bool hasParams, bool hasNamedSchemaParse) =>
        // Nullable named schemas use their own .nullable() method and need no Zod namespace.
        inlineConstructedTypes.Where(ValidationTypes.NeedsConstructedSchema)
            .Any(type => TypeScriptIdentifierUsage.References(_schema.Build(type), "z"))
            ? _exactJsonNumbers && inlineConstructedTypes.Any(TypeScriptExactJson.UsesNumberCodec)
                ? [ZodImport(), TypeScriptExactJson.Import()]
                : [ZodImport()]
            : [];

    public IReadOnlyList<ImportNode> SchemaDefinitionImports(IReadOnlyCollection<IrType> definedTypes) =>
        // A schema-declaring file uses `z` (value) to build the schema and `ZodSchema` (type) to annotate the
        // const type (`export const XSchema: ZodSchema<X> = ...`). `ZodSchema` — только тип, поэтому под
        // `verbatimModuleSyntax` его нужно импортировать type-only (иначе TS1485), отдельной строкой от `z`.
        definedTypes.Count > 0
            ? _exactJsonNumbers && definedTypes.Any(TypeScriptExactJson.UsesNumberCodec)
                ? [ZodImport(), ZodSchemaTypeImport(), TypeScriptExactJson.Import()]
                : [ZodImport(), ZodSchemaTypeImport()]
            : [];

    /// <inheritdoc/>
    public TypeNode? SchemaTypeAnnotation(string validatedTypeName) =>
        ZodSchemaTypes.Build(new SimpleTypeNode(new IdentifierNode(validatedTypeName)), _importPath);

    private ImportNode ZodImport() =>
        new(ImmutableList.Create(new IdentifierNode("z")), _importPath, ImportKind.Named);

    private ImportNode ZodSchemaTypeImport() =>
        new(ImmutableList.Create(new IdentifierNode("ZodSchema")), _importPath, ImportKind.Named, isTypeOnly: true);
}
