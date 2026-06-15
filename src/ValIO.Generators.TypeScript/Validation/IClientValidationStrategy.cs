using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>
/// Стратегия валидации тел запросов/ответов и параметров клиента для конкретной библиотеки
/// (zod/valibot/typebox/none). Инкапсулирует синтаксис parse-вызова, построение inline-схем
/// из IR и необходимые runtime-импорты. Реализация выбирается фабрикой по
/// <see cref="Options.TypeScriptGenerationOptions.ValidationLibrary"/> — клиентский код не зависит
/// от конкретной библиотеки.
/// </summary>
internal interface IClientValidationStrategy
{
    /// <summary>Включена ли валидация (выбрана непустая библиотека).</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Валидирует ли стратегия route/query параметры по предварительно сгенерированной импортируемой
    /// именованной схеме (<c>{op}RouteParamsSchema</c>). Если <c>false</c> — схема параметров строится
    /// inline из самих параметров. Используется логикой импортов клиента.
    /// </summary>
    bool ImportsNamedParamsSchema { get; }

    /// <summary>
    /// Валидирует <paramref name="input"/> по схеме, построенной из <paramref name="type"/>
    /// (inline-схема: ссылка на модель, union, array и т.д.). Для None возвращает вход без изменений.
    /// </summary>
    ExpressionNode Validate(IrType type, ExpressionNode input);

    /// <summary>
    /// Валидирует <paramref name="input"/> по импортируемой именованной схеме
    /// (например модели-ответа). Для None возвращает вход без изменений.
    /// </summary>
    ExpressionNode ValidateNamed(string schemaName, ExpressionNode input);

    /// <summary>
    /// Валидирует объект параметров <paramref name="input"/> для набора route/query
    /// <paramref name="parameters"/>. Если <see cref="ImportsNamedParamsSchema"/> — использует
    /// импортируемую схему <paramref name="importedSchemaName"/>; иначе строит схему параметров inline.
    /// </summary>
    ExpressionNode ValidateParams(string importedSchemaName, IReadOnlyList<IrParameter> parameters, ExpressionNode input);

    /// <summary>
    /// Runtime-импорты библиотеки для сгенерированных выражений валидации, с учётом inline-валидируемых
    /// типов (тела/ответы) и наличия валидации параметров (<paramref name="hasParams"/>).
    /// </summary>
    IReadOnlyList<ImportNode> RuntimeImports(IReadOnlyCollection<IrType> inlineValidatedTypes, bool hasParams);
}
