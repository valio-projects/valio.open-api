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
    /// Whether this validator references the exported <c>…Constraints</c> constants object for validation-rule
    /// values (as Zod does, e.g. <c>.min(GetTreeQueryParamsConstraints.limit.min)</c>) rather than inlining
    /// literals. When <see langword="true"/>, a schema-declaring file must import the constants object; when
    /// <see langword="false"/> (Valibot/TypeBox/None) the rule values are inlined and no import is needed.
    /// </summary>
    bool UsesConstraintConstants { get; }

    /// <summary>
    /// Имя shared-константы UUID-формата для библиотеки (<c>UUID_REGEX</c> для zod/valibot,
    /// <c>UUID_PATTERN</c> для typebox), на которую ссылаются схемы в режиме <c>uuidFormat = regex</c>;
    /// <c>null</c> когда валидация выключена. Импортируется файлом, который ОБЪЯВЛЯЕТ схему с uuid.
    /// </summary>
    string? UuidConstName { get; }

    /// <summary>
    /// Валидирует <paramref name="input"/> по схеме, построенной из <paramref name="type"/>
    /// (inline-схема: ссылка на модель, union, array и т.д.). Для None возвращает вход без изменений.
    /// </summary>
    ExpressionNode Validate(IrType type, ExpressionNode input);

    /// <summary>
    /// Строит выражение схемы из <paramref name="type"/> БЕЗ parse-обёртки — для объявления
    /// именованной константы (например в <c>{op}.results.schemas.ts</c>), чтобы не пересоздавать
    /// union/array-схему в рантайме на каждый вызов. Для None — не вызывается.
    /// </summary>
    ExpressionNode BuildSchema(IrType type);

    /// <summary>
    /// Строит выражение схемы для объекта параметров (route/query) — для объявления именованной
    /// константы <c>{Op}RouteParamsSchema</c>/<c>{Op}QueryParamsSchema</c>. Для None — не вызывается.
    /// </summary>
    /// <param name="parameters">The route or query parameters.</param>
    /// <param name="constraintsRefPrefix">
    /// The exported constraints-object name (e.g. <c>GetTreeQueryParamsConstraints</c>) a validator that
    /// <see cref="UsesConstraintConstants"/> references for rule values (<c>{prefix}.{param}.{rule}</c>). Ignored by
    /// validators that inline literals; <see langword="null"/> when the group has no constrained parameters.
    /// </param>
    ExpressionNode BuildParamsSchema(IReadOnlyList<IrParameter> parameters, string? constraintsRefPrefix = null);

    /// <summary>
    /// Валидирует <paramref name="input"/> по импортируемой именованной схеме (модель-ответ,
    /// вынесенная result-схема или схема параметров). Для None возвращает вход без изменений.
    /// </summary>
    ExpressionNode ValidateNamed(string schemaName, ExpressionNode input);

    /// <summary>
    /// Runtime-импорты для КЛИЕНТСКОГО файла: учитывает типы, валидируемые inline (тело),
    /// валидацию параметров (<paramref name="hasParams"/> — parse по импортируемой схеме параметров)
    /// и наличие parse по именованным схемам (<paramref name="hasNamedSchemaParse"/> — модели-ответы
    /// и вынесенные result-схемы).
    /// </summary>
    IReadOnlyList<ImportNode> RuntimeImports(IReadOnlyCollection<IrType> inlineConstructedTypes, bool hasParams, bool hasNamedSchemaParse);

    /// <summary>
    /// Runtime-импорты для файла, который ОБЪЯВЛЯЕТ схемы (например <c>{op}.results.schemas.ts</c>):
    /// нужно только пространство построения схем (zod <c>z</c> / valibot <c>v</c> / typebox <c>Type</c>),
    /// без parse-рантайма.
    /// </summary>
    IReadOnlyList<ImportNode> SchemaDefinitionImports(IReadOnlyCollection<IrType> definedTypes);

    /// <summary>
    /// Returns the typed annotation for a declared schema const whose validated type is
    /// <paramref name="validatedTypeName"/>, so a params/results schema reads like a model schema
    /// (e.g. Zod <c>ZodSchema&lt;T&gt;</c>, Valibot <c>v.GenericSchema&lt;T&gt;</c>).
    /// </summary>
    /// <param name="validatedTypeName">The generated interface/type name the schema validates.</param>
    /// <returns>
    /// The annotation <see cref="TypeNode"/>, or <see langword="null"/> for validators that do not
    /// annotate the const (TypeBox relies on <c>Static&lt;typeof&gt;</c>; None when validation is off).
    /// The caller adds a type-only import for <paramref name="validatedTypeName"/> only when this is non-null.
    /// </returns>
    TypeNode? SchemaTypeAnnotation(string validatedTypeName);
}
