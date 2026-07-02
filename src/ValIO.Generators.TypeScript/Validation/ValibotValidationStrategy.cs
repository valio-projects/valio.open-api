using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Valibot.Ast.Builders;
using static ValIO.Generators.TypeScript.Validation.ValidationAst;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>Валидация через Valibot: <c>v.parse(Schema, input)</c>. Схемы строит <see cref="ValibotAstBuilder"/>.</summary>
internal sealed class ValibotValidationStrategy : IClientValidationStrategy
{
    private readonly ValibotAstBuilder _builder;

    public ValibotValidationStrategy(ValibotAstBuilder builder)
    {
        _builder = builder;
    }

    public bool IsEnabled => true;

    /// <summary>Valibot inlines literal rule values (like its model schemas), so no constants object is imported.</summary>
    public bool UsesConstraintConstants => false;

    public string? UuidConstName => Options.TypeScriptOptionIds.UuidFormat.RegexConstName;

    public ExpressionNode Validate(IrType type, ExpressionNode input) =>
        Call(Member(Id("v"), "parse"), _builder.BuildSchemaFromIrType(type), input);

    public ExpressionNode BuildSchema(IrType type) => _builder.BuildSchemaFromIrType(type);

    public ExpressionNode BuildParamsSchema(IReadOnlyList<IrParameter> parameters, string? constraintsRefPrefix = null) =>
        _builder.BuildObjectFromParameters(parameters);

    public ExpressionNode ValidateNamed(string schemaName, ExpressionNode input) =>
        Call(Member(Id("v"), "parse"), Id(schemaName), input);

    public IReadOnlyList<ImportNode> RuntimeImports(IReadOnlyCollection<IrType> inlineConstructedTypes, bool hasParams, bool hasNamedSchemaParse) =>
        // Любой `v.parse(...)` / `v.object(...)` обращается к пространству `v`.
        inlineConstructedTypes.Count > 0 || hasParams || hasNamedSchemaParse
            ? [ValibotImport()]
            : [];

    public IReadOnlyList<ImportNode> SchemaDefinitionImports(IReadOnlyCollection<IrType> definedTypes) =>
        definedTypes.Count > 0 ? [ValibotImport()] : [];

    /// <inheritdoc/>
    public TypeNode? SchemaTypeAnnotation(string validatedTypeName) =>
        new RawTypeNode($"v.GenericSchema<{validatedTypeName}>");

    private static ImportNode ValibotImport() =>
        new(ImmutableList.Create(new IdentifierNode("v")), "valibot", ImportKind.Namespace);
}
