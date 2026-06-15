using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>Валидация выключена: тела/параметры передаются без проверки.</summary>
internal sealed class NoneValidationStrategy : IClientValidationStrategy
{
    public static readonly NoneValidationStrategy Instance = new();

    private NoneValidationStrategy() { }

    public bool IsEnabled => false;

    public bool ImportsNamedParamsSchema => false;

    public ExpressionNode Validate(IrType type, ExpressionNode input) => input;

    public ExpressionNode ValidateNamed(string schemaName, ExpressionNode input) => input;

    public ExpressionNode ValidateParams(string importedSchemaName, IReadOnlyList<IrParameter> parameters, ExpressionNode input) => input;

    public IReadOnlyList<ImportNode> RuntimeImports(IReadOnlyCollection<IrType> inlineValidatedTypes, bool hasParams) =>
        [];
}
