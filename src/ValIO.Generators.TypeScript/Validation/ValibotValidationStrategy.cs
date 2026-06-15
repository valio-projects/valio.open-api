using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
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

    // Схема параметров строится inline из самих параметров.
    public bool ImportsNamedParamsSchema => false;

    public ExpressionNode Validate(IrType type, ExpressionNode input) =>
        Call(Member(Id("v"), "parse"), _builder.BuildSchemaFromIrType(type), input);

    public ExpressionNode ValidateNamed(string schemaName, ExpressionNode input) =>
        Call(Member(Id("v"), "parse"), Id(schemaName), input);

    public ExpressionNode ValidateParams(string importedSchemaName, IReadOnlyList<IrParameter> parameters, ExpressionNode input) =>
        Call(Member(Id("v"), "parse"), _builder.BuildObjectFromParameters(parameters), input);

    public IReadOnlyList<ImportNode> RuntimeImports(IReadOnlyCollection<IrType> inlineValidatedTypes, bool hasParams) =>
        // `v.parse(...)` / `v.object(...)` всегда обращаются к пространству `v`.
        inlineValidatedTypes.Count > 0 || hasParams
            ? [new ImportNode(ImmutableList.Create(new IdentifierNode("v")), "valibot", ImportKind.Namespace)]
            : [];
}
