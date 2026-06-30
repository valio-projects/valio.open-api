using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.TypeBox.Ast.Builders;
using static ValIO.Generators.TypeScript.Validation.ValidationAst;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>Валидация через TypeBox: <c>Value.Parse(Schema, input)</c>. Схемы строит <see cref="TypeBoxAstBuilder"/>.</summary>
internal sealed class TypeBoxValidationStrategy : IClientValidationStrategy
{
    private readonly TypeBoxAstBuilder _builder;

    public TypeBoxValidationStrategy(TypeBoxAstBuilder builder)
    {
        _builder = builder;
    }

    public bool IsEnabled => true;

    public string? UuidConstName => Options.TypeScriptOptionIds.UuidFormat.PatternConstName;

    public ExpressionNode Validate(IrType type, ExpressionNode input) =>
        Call(Member(Id("Value"), "Parse"), _builder.BuildSchemaFromIrType(type), input);

    public ExpressionNode BuildSchema(IrType type) => _builder.BuildSchemaFromIrType(type);

    public ExpressionNode BuildParamsSchema(IReadOnlyList<IrParameter> parameters) => _builder.BuildObjectFromParameters(parameters);

    public ExpressionNode ValidateNamed(string schemaName, ExpressionNode input) =>
        Call(Member(Id("Value"), "Parse"), Id(schemaName), input);

    public IReadOnlyList<ImportNode> RuntimeImports(IReadOnlyCollection<IrType> inlineConstructedTypes, bool hasParams, bool hasNamedSchemaParse)
    {
        var imports = ImmutableList.CreateBuilder<ImportNode>();

        // `Type` нужен только для inline-построения схем тела (Type.Union/Array/...); схемы параметров
        // вынесены в {op}.params.schema.ts, поэтому клиенту `Type` для них не нужен.
        if (inlineConstructedTypes.Any(ValidationTypes.NeedsConstructedSchema))
            imports.Add(TypeImport());

        // `Value.Parse(...)` нужен при любой валидации (тело/параметры/именованные result-схемы).
        if (inlineConstructedTypes.Count > 0 || hasParams || hasNamedSchemaParse)
            imports.Add(ValueImport());

        return imports.ToImmutable();
    }

    public IReadOnlyList<ImportNode> SchemaDefinitionImports(IReadOnlyCollection<IrType> definedTypes) =>
        // Файл объявляет `Type.Union([...])` и т.п. → нужен только `Type` (без `Value`).
        definedTypes.Count > 0 ? [TypeImport()] : [];

    /// <inheritdoc/>
    /// <remarks>TypeBox derives the static type via <c>Static&lt;typeof Schema&gt;</c>, so the const is not annotated.</remarks>
    public TypeNode? SchemaTypeAnnotation(string validatedTypeName) => null;

    private static ImportNode TypeImport() =>
        new(ImmutableList.Create(new IdentifierNode("Type")), "@sinclair/typebox", ImportKind.Named);

    private static ImportNode ValueImport() =>
        new(ImmutableList.Create(new IdentifierNode("Value")), "@sinclair/typebox/value", ImportKind.Named);
}
