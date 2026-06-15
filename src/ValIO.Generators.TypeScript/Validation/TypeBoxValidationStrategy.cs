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

    // Схема параметров строится inline (Type.Object) из самих параметров.
    public bool ImportsNamedParamsSchema => false;

    public ExpressionNode Validate(IrType type, ExpressionNode input) =>
        Call(Member(Id("Value"), "Parse"), _builder.BuildSchemaFromIrType(type), input);

    public ExpressionNode BuildSchema(IrType type) => _builder.BuildSchemaFromIrType(type);

    public ExpressionNode ValidateNamed(string schemaName, ExpressionNode input) =>
        Call(Member(Id("Value"), "Parse"), Id(schemaName), input);

    public ExpressionNode ValidateParams(string importedSchemaName, IReadOnlyList<IrParameter> parameters, ExpressionNode input) =>
        Call(Member(Id("Value"), "Parse"), _builder.BuildObjectFromParameters(parameters), input);

    public IReadOnlyList<ImportNode> RuntimeImports(IReadOnlyCollection<IrType> inlineConstructedTypes, bool hasParams, bool hasNamedSchemaParse)
    {
        var imports = ImmutableList.CreateBuilder<ImportNode>();

        // `Type` нужен для inline-построения схем: Type.Object для параметров, Type.Union/Array/... для тела.
        if (hasParams || inlineConstructedTypes.Any(ZodSchemaExpressionBuilder.RequiresNamespace))
            imports.Add(TypeImport());

        // `Value.Parse(...)` нужен при любой валидации (тело/параметры/именованные result-схемы).
        if (inlineConstructedTypes.Count > 0 || hasParams || hasNamedSchemaParse)
            imports.Add(ValueImport());

        return imports.ToImmutable();
    }

    public IReadOnlyList<ImportNode> SchemaDefinitionImports(IReadOnlyCollection<IrType> definedTypes) =>
        // Файл объявляет `Type.Union([...])` и т.п. → нужен только `Type` (без `Value`).
        definedTypes.Count > 0 ? [TypeImport()] : [];

    private static ImportNode TypeImport() =>
        new(ImmutableList.Create(new IdentifierNode("Type")), "@sinclair/typebox", ImportKind.Named);

    private static ImportNode ValueImport() =>
        new(ImmutableList.Create(new IdentifierNode("Value")), "@sinclair/typebox/value", ImportKind.Named);
}
