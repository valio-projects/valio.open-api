using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using static ValIO.Generators.TypeScript.Validation.ValidationAst;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>Валидация через Zod: <c>Schema.parse(input)</c> / <c>z.union([...]).parse(input)</c>.</summary>
internal sealed class ZodValidationStrategy : IClientValidationStrategy
{
    private readonly ZodSchemaExpressionBuilder _schema;
    private readonly string _importPath;

    public ZodValidationStrategy(ZodSchemaExpressionBuilder schema, string importPath)
    {
        _schema = schema;
        _importPath = importPath;
    }

    public bool IsEnabled => true;

    // Zod-схемы параметров генерируются отдельным шагом и импортируются по имени.
    public bool ImportsNamedParamsSchema => true;

    public ExpressionNode Validate(IrType type, ExpressionNode input) =>
        Call(Member(_schema.Build(type), "parse"), input);

    public ExpressionNode ValidateNamed(string schemaName, ExpressionNode input) =>
        Call(Member(Id(schemaName), "parse"), input);

    public ExpressionNode ValidateParams(string importedSchemaName, IReadOnlyList<IrParameter> parameters, ExpressionNode input) =>
        Call(Member(Id(importedSchemaName), "parse"), input);

    public IReadOnlyList<ImportNode> RuntimeImports(IReadOnlyCollection<IrType> inlineValidatedTypes, bool hasParams) =>
        // Импорт `z` нужен только когда строится inline `z.*` (union/array/...);
        // голые `{Model}Schema.parse` и импортируемые параметры в `z` не нуждаются.
        inlineValidatedTypes.Any(ZodSchemaExpressionBuilder.RequiresNamespace)
            ? [new ImportNode(ImmutableList.Create(new IdentifierNode("z")), _importPath, ImportKind.Named)]
            : [];
}
