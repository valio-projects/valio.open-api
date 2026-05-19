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
    AngularRxjs = 4,
}

/// <summary>
/// Один сгенерированный endpoint-файл (axios или fetch) или per-client сервис (angular-rxjs).
/// </summary>
public enum TypeScriptEndpointTransport
{
    Axios,
    Fetch,
    AngularRxjs,
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
/// Вспомогательные правила выбора транспорта для TanStack Query.
/// </summary>
public static class TypeScriptHttpTransportsTanStack
{
    /// <summary>
    /// Транспорт для TanStack Query: при наличии Axios — только он, иначе Fetch.
    /// </summary>
    public static TypeScriptEndpointTransport PreferredHookTransport(TypeScriptHttpTransports transports)
    {
        if (transports.HasFlag(TypeScriptHttpTransports.Axios))
            return TypeScriptEndpointTransport.Axios;
        return TypeScriptEndpointTransport.Fetch;
    }
}
