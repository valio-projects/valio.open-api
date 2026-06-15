using System.Reflection;
using System.Text;

namespace ValIO.Core.Tests.TestHelpers;

/// <summary>
/// Сборщики OpenAPI JSON-фрагментов для тестов: единый конверт документа
/// и загрузка встроенных фикстур.
/// </summary>
internal static class OpenApiFragments
{
    /// <summary>
    /// Оборачивает набор component-схем в минимальный валидный OpenAPI документ.
    /// </summary>
    public static string DocWithSchemas(string openapiVersion, params (string name, string body)[] schemas)
    {
        var sb = new StringBuilder();
        sb.Append('{');

        if (openapiVersion.StartsWith("2.", StringComparison.Ordinal))
        {
            // Swagger 2.0: definitions вместо components/schemas
            sb.Append("\"swagger\":\"2.0\",");
            sb.Append("\"info\":{\"title\":\"Test\",\"version\":\"1.0.0\"},");
            sb.Append("\"paths\":{},");
            sb.Append("\"definitions\":{");
            AppendSchemas(sb, schemas);
            sb.Append('}');
        }
        else
        {
            sb.Append("\"openapi\":\"").Append(openapiVersion).Append("\",");
            sb.Append("\"info\":{\"title\":\"Test\",\"version\":\"1.0.0\"},");
            sb.Append("\"paths\":{},");
            sb.Append("\"components\":{\"schemas\":{");
            AppendSchemas(sb, schemas);
            sb.Append("}}");
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendSchemas(StringBuilder sb, (string name, string body)[] schemas)
    {
        for (var i = 0; i < schemas.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append('"').Append(schemas[i].name).Append("\":").Append(schemas[i].body);
        }
    }

    /// <summary>
    /// Загружает встроенную фикстуру по относительному пути внутри TestData.
    /// </summary>
    public static string LoadEmbedded(string relativeName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = "ValIO.Core.Tests.TestData." + relativeName.Replace('/', '.').Replace('\\', '.');
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resource}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
