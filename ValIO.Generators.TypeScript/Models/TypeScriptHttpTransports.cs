namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// Какие HTTP-транспорты генерировать для TypeScript (можно комбинировать).
/// </summary>
[Flags]
public enum TypeScriptHttpTransports
{
    None = 0,
    Fetch = 1,
    Axios = 2,
}

/// <summary>
/// Один сгенерированный endpoint-файл (axios или fetch).
/// </summary>
public enum TypeScriptEndpointTransport
{
    Axios,
    Fetch,
}

/// <summary>
/// Настройки для fetch-клиента (аналог <see cref="AxiosOptions"/>).
/// </summary>
public sealed class FetchOptions
{
    /// <summary>
    /// Путь до модуля с кастомной функцией fetch (например, '../lib/server-fetch').
    /// </summary>
    public string? CustomFetcherPath { get; set; }

    /// <summary>
    /// Имя экспорта: <c>(input, init?) => Promise&lt;Response&gt;</c>.
    /// </summary>
    public string? CustomFetcherName { get; set; }
}

/// <summary>
/// Нормализация флагов транспортов и согласование с legacy <see cref="ValIO.Core.Models.HttpClientLibrary"/>.
/// </summary>
public static class TypeScriptHttpTransportResolver
{
    /// <summary>
    /// Возвращает ненулевой набор транспортов: явные флаги или вывод из <see cref="TypeScriptGenerationOptions.HttpClientLibrary"/>.
    /// </summary>
    public static TypeScriptHttpTransports Resolve(TypeScriptGenerationOptions options)
    {
        if (options.HttpTransports != TypeScriptHttpTransports.None)
            return options.HttpTransports;

        return options.HttpClientLibrary == ValIO.Core.Models.HttpClientLibrary.Axios
            ? TypeScriptHttpTransports.Axios
            : TypeScriptHttpTransports.Fetch;
    }

    /// <summary>
    /// Транспорт для TanStack Query: при наличии Axios — только он, иначе Fetch.
    /// </summary>
    public static TypeScriptEndpointTransport ResolveTanStackHookTransport(TypeScriptHttpTransports transports)
    {
        if (transports.HasFlag(TypeScriptHttpTransports.Axios))
            return TypeScriptEndpointTransport.Axios;
        return TypeScriptEndpointTransport.Fetch;
    }
}
