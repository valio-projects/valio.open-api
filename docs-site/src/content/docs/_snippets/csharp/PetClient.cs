// Client/PetsClient.cs
using System.Net.Http.Json;
using Acme.PetStore.Models;

namespace Acme.PetStore.Client;

public sealed class PetsClient
{
    private readonly HttpClient _http;

    public PetsClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<Pet>> ListPetsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/pets", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync(PetstoreJsonContext.Default.PetArray, ct)
            .ConfigureAwait(false)
            ?? Array.Empty<Pet>();
    }

    public async Task<Pet> CreatePetAsync(Pet body, CancellationToken ct = default)
    {
        var response = await _http
            .PostAsJsonAsync("/pets", body, PetstoreJsonContext.Default.Pet, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync(PetstoreJsonContext.Default.Pet, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty response body");
    }
}

// DependencyInjection.cs
public static class PetstoreServiceCollectionExtensions
{
    public static IServiceCollection AddPetstoreClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        services.AddHttpClient<PetsClient>(c => c.BaseAddress = baseAddress);
        return services;
    }
}
