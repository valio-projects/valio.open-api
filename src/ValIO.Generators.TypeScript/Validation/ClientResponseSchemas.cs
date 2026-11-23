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

    /// <summary>Enumerates typed responses whose bodies receive runtime validation.</summary>
    /// <param name="operation">The operation whose response schemas are needed.</param>
    /// <param name="includeAlternatives">Whether the transport selects a schema by media type.</param>
    /// <returns>The validated responses or their media-specific schema inputs.</returns>
    public static IEnumerable<IrResponse> ValidatedResponses(IrOperation operation, bool includeAlternatives = false)
        => TypeScriptResponseMedia.SchemaInputs(operation, includeAlternatives)
            .Where(r => r.Type != null && ValidationTypes.IsValidatable(r.Type));

    /// <summary>Finds the generated schema constants required by the client.</summary>
    /// <param name="naming">The TypeScript identifier naming policy.</param>
    /// <param name="operation">The operation supplying the response schemas.</param>
    /// <param name="includeAlternatives">Whether the client needs media-specific validators.</param>
    /// <param name="skipBinaryStreams">Whether successful binary bodies bypass decoding and validation.</param>
    /// <returns>Names of the exported schema constants to import.</returns>
    public static List<string> ConstructedResultSchemaNames(INamingConverter naming, IrOperation operation,
        bool includeAlternatives = false, bool skipBinaryStreams = false)
        => ValidatedResponses(operation, includeAlternatives)
            .Where(r => ValidationTypes.NeedsConstructedSchema(r.Type!))
            .Where(response => !skipBinaryStreams || !TypeScriptBinaryResponse.IsBinarySuccess(operation, response))
            .Select(r => ResultSchemaName(naming, operation.MethodName, r.StatusCode))
            .ToList();

    /// <summary>Finds model schemas used directly by the client rather than by a generated result schema.</summary>
    /// <param name="operation">The operation supplying request and response shapes.</param>
    /// <param name="includeAlternatives">Whether the transport uses alternate response representations.</param>
    /// <returns>The set of model schema names required by the client.</returns>
    public static HashSet<string> ModelSchemaNames(IrOperation operation, bool includeAlternatives = false)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (operation.RequestBody != null && ValidationTypes.IsValidatable(operation.RequestBody.Type))
            ValidationTypes.CollectModelSchemaNames(operation.RequestBody.Type, names);

        foreach (var response in ValidatedResponses(operation, includeAlternatives))
            if (!ValidationTypes.NeedsConstructedSchema(response.Type!))
                ValidationTypes.CollectModelSchemaNames(response.Type!, names);

        return names;
    }
}
