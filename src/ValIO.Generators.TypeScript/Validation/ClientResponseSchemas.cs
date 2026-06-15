using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Ast.Builders;

namespace ValIO.Generators.TypeScript.Validation;

/// <summary>
/// Общая логика выбора схем для валидации тел ответов клиента — едина для ВСЕХ транспортов
/// (axios/fetch/got/ky/ofetch/undici), чтобы не расходиться: голая ссылка на модель валидируется
/// напрямую <c>{Model}Schema</c>; constructed-тип (union/array/...) — вынесенной именованной схемой
/// <c>{Op}Result{Status}Schema</c> из <c>{op}.results.schemas.ts</c>.
/// </summary>
internal static class ClientResponseSchemas
{
    /// <summary>Имя вынесенной result-схемы для status-варианта: <c>{Op}Result{Status}Schema</c>.</summary>
    public static string ResultSchemaName(INamingConverter naming, string operationName, string statusCode)
        => TypeScriptResultsAstBuilder.ResultVariantName(naming, operationName, statusCode) + "Schema";

    /// <summary>Числовые типизированные ответы, которые валидируются (исключая any/void).</summary>
    public static IEnumerable<IrResponse> ValidatedResponses(IrOperation operation)
        => operation.Responses.Where(r =>
            r.Type != null && int.TryParse(r.StatusCode, out _) && ValidationTypes.IsValidatable(r.Type!));

    /// <summary>Имена вынесенных result-схем (constructed-ответы) — для импорта и ссылки в клиенте.</summary>
    public static List<string> ConstructedResultSchemaNames(INamingConverter naming, IrOperation operation)
        => ValidatedResponses(operation)
            .Where(r => ValidationTypes.NeedsConstructedSchema(r.Type!))
            .Select(r => ResultSchemaName(naming, operation.MethodName, r.StatusCode))
            .ToList();

    /// <summary>
    /// Имена model-схем (<c>{Model}Schema</c>), импортируемых клиентом: тело запроса (рекурсивно) +
    /// ответы-голые-ссылки. Члены union/array constructed-ответов сюда НЕ входят — они импортируются
    /// файлом <c>{op}.results.schemas.ts</c>.
    /// </summary>
    public static HashSet<string> ModelSchemaNames(IrOperation operation)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (operation.RequestBody != null && ValidationTypes.IsValidatable(operation.RequestBody.Type))
            ValidationTypes.CollectModelSchemaNames(operation.RequestBody.Type, names);

        foreach (var response in ValidatedResponses(operation))
            if (!ValidationTypes.NeedsConstructedSchema(response.Type!))
                ValidationTypes.CollectModelSchemaNames(response.Type!, names);

        return names;
    }
}
