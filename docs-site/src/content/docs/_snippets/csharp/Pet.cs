// Models/Pet.cs
using System.Text.Json.Serialization;

namespace Acme.PetStore.Models;

public sealed class Pet
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("tag")]
    public string? Tag { get; init; }
}

// Models/PetstoreJsonContext.cs
[JsonSerializable(typeof(Pet))]
[JsonSerializable(typeof(Pet[]))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class PetstoreJsonContext : JsonSerializerContext { }
