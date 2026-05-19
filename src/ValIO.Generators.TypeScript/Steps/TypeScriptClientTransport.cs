using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Steps;

internal static class TypeScriptClientTransport
{
    private static readonly IReadOnlySet<string> GeneratedClientSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        TypeScriptOptionIds.Transport.Ky,
        TypeScriptOptionIds.Transport.Axios,
        TypeScriptOptionIds.Transport.Fetch,
    };

    public static string GetPrimaryGeneratedClientSuffix(TypeScriptGenerationOptions? options)
    {
        return options?.Transports.FirstOrDefault(GeneratedClientSuffixes.Contains)
            ?? TypeScriptOptionIds.Transport.Fetch;
    }
}
