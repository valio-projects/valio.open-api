using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Resolves which generated transport client the framework query hooks (TanStack Query / SWR) wrap.
/// </summary>
/// <remarks>
/// Only <c>ky</c>, <c>axios</c> and <c>fetch</c> produce a plain client function suitable for wrapping; the
/// server-only <c>nextjs</c> transport and the framework transports (angular/solid) are excluded. The chosen
/// suffix drives the hook file's <c>import { op } from "./{op}.{suffix}"</c>.
/// </remarks>
internal static class TypeScriptClientTransport
{
    /// <summary>The transports that emit a wrappable client function, in no particular preference order.</summary>
    private static readonly IReadOnlySet<string> GeneratedClientSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        TypeScriptOptionIds.Transport.Ky,
        TypeScriptOptionIds.Transport.Axios,
        TypeScriptOptionIds.Transport.Fetch,
    };

    /// <summary>Whether <paramref name="transport"/> is a client-capable (wrappable) transport.</summary>
    /// <param name="transport">The transport id, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is one of ky/axios/fetch.</returns>
    public static bool IsGeneratedClientSuffix(string? transport) =>
        transport != null && GeneratedClientSuffixes.Contains(transport);

    /// <summary>
    /// Picks the first client-capable transport present in <see cref="TypeScriptGenerationOptions.Transports"/>,
    /// falling back to <c>fetch</c> when none is selected.
    /// </summary>
    /// <param name="options">The TypeScript generation options, or <see langword="null"/>.</param>
    /// <returns>The client suffix (ky/axios/fetch).</returns>
    public static string GetPrimaryGeneratedClientSuffix(TypeScriptGenerationOptions? options)
    {
        return options?.Transports.FirstOrDefault(GeneratedClientSuffixes.Contains)
            ?? TypeScriptOptionIds.Transport.Fetch;
    }

    /// <summary>
    /// Resolves the transport the query hooks should wrap: the explicit
    /// <see cref="TypeScriptGenerationOptions.QueryTransport"/> when set to a client-capable transport, otherwise
    /// the primary transport from <see cref="GetPrimaryGeneratedClientSuffix"/>.
    /// </summary>
    /// <param name="options">The TypeScript generation options, or <see langword="null"/>.</param>
    /// <returns>The resolved client suffix (ky/axios/fetch).</returns>
    /// <seealso cref="GetPrimaryGeneratedClientSuffix"/>
    public static string ResolveHookClientSuffix(TypeScriptGenerationOptions? options)
    {
        if (IsGeneratedClientSuffix(options?.QueryTransport))
            return options!.QueryTransport!;
        return GetPrimaryGeneratedClientSuffix(options);
    }
}
