using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.TypeBox.Ast.Builders;
using ValIO.Generators.TypeScript.Valibot.Ast.Builders;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>
/// Создаёт стратегию валидации клиента по выбранной библиотеке. Единственное место,
/// где сопоставляется имя библиотеки с реализацией — остальной код работает через
/// <see cref="IClientValidationStrategy"/> и не зависит от конкретной библиотеки.
/// </summary>
internal static class ClientValidationStrategyFactory
{
    public static IClientValidationStrategy Create(TypeScriptGenerationOptions? options) =>
        options?.ValidationLibrary switch
        {
            TypeScriptOptionIds.Validation.Zod3 =>
                new ZodValidationStrategy(new ZodSchemaExpressionBuilder(options.UuidFormat), "zod"),
            TypeScriptOptionIds.Validation.Zod4 =>
                new ZodValidationStrategy(new ZodSchemaExpressionBuilder(options.UuidFormat), "zod/v4"),
            TypeScriptOptionIds.Validation.Valibot =>
                new ValibotValidationStrategy(new ValibotAstBuilder(options.UuidFormat)),
            TypeScriptOptionIds.Validation.TypeBox =>
                new TypeBoxValidationStrategy(new TypeBoxAstBuilder(options.UuidFormat)),
            _ => NoneValidationStrategy.Instance
        };
}
