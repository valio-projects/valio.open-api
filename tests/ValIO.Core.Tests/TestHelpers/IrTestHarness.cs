using System.Text;
using Microsoft.OpenApi;
using ValIO.Core.IR;
using ValIO.Core.IR.Builders;
using ValIO.Core.Services;

namespace ValIO.Core.Tests.TestHelpers;

/// <summary>
/// Хелпер для построения IR из встроенного/инлайн OpenAPI JSON.
/// Сознательно использует реальный ридер <see cref="OpenApiDocument.LoadAsync(Stream, string, OpenApiReaderSettings?, System.Threading.CancellationToken)"/>,
/// чтобы прогонять нормализацию nullable (3.0 nullable / 3.1 type:[T,"null"] / 2.0 x-nullable),
/// а не конструировать OpenApiSchema программно.
/// </summary>
internal static class IrTestHarness
{
    public static async Task<OpenApiDocument> LoadAsync(string json)
    {
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await OpenApiDocument.LoadAsync(ms, "json");
        return result.Document
            ?? throw new InvalidOperationException(
                "Failed to parse OpenAPI document: " +
                (result.Diagnostic?.Errors.FirstOrDefault()?.Message ?? "unknown error"));
    }

    public static async Task<IrDocument> BuildIrAsync(string json)
    {
        var doc = await LoadAsync(json);
        var resolver = new SchemaResolver(doc);
        var converter = new NamingConverter();
        return new IrBuilder(resolver, converter).BuildFromOpenApi(doc);
    }

    /// <summary>
    /// Строит IR с применением affix-преобразования имён (prefix/suffix).
    /// </summary>
    public static async Task<IrDocument> BuildIrAsync(string json, NameAffixOptions affixOptions)
    {
        var doc = await LoadAsync(json);
        var resolver = new SchemaResolver(doc);
        var converter = new NamingConverter();
        return new IrBuilder(resolver, converter).BuildFromOpenApi(doc, warnings: null, affixOptions: affixOptions);
    }

    /// <summary>
    /// Строит документ с заданными component-схемами и возвращает построенный IR.
    /// </summary>
    public static Task<IrDocument> BuildIrFromSchemasAsync(string openapiVersion, params (string name, string body)[] schemas)
        => BuildIrAsync(OpenApiFragments.DocWithSchemas(openapiVersion, schemas));

    /// <summary>
    /// Возвращает единственную модель с заданным именем из построенного IR.
    /// </summary>
    public static async Task<IrModel> BuildSingleModelAsync(
        string schemaName,
        string schemaBody,
        string openapiVersion = "3.0.3",
        params (string name, string body)[] extraSchemas)
    {
        var all = new List<(string, string)> { (schemaName, schemaBody) };
        all.AddRange(extraSchemas);
        var ir = await BuildIrFromSchemasAsync(openapiVersion, all.ToArray());
        return ir.Models.Single(m => m.Name == schemaName);
    }
}
