using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Reads and writes exactly the wire strings declared by the generated enum.</summary>
/// <typeparam name="TEnum">The enumeration declared as strings by the API.</typeparam>
internal sealed class ValioStrictStringEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Caches the exact bidirectional mapping for this generated type.</summary>
    private static readonly (Dictionary<string, TEnum> ByName, Dictionary<TEnum, string> ByValue) Members = GetMembers();

    /// <inheritdoc />
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? GetValue(reader.GetString())
            : throw new JsonException($"Expected a string value for {typeof(TEnum).Name}.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(GetName(value));

    /// <inheritdoc />
    public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        GetValue(reader.GetString());

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WritePropertyName(GetName(value));

    /// <summary>Resolves a declared wire string using ordinal matching.</summary>
    /// <param name="name">The decoded JSON string.</param>
    /// <returns>The matching enum member.</returns>
    /// <exception cref="JsonException">The string is outside the declared values.</exception>
    private static TEnum GetValue(string? name) =>
        name is not null && Members.ByName.TryGetValue(name, out var value)
            ? value
            : throw new JsonException($"The value is not declared for {typeof(TEnum).Name}.");

    /// <summary>Resolves the exact wire string for a declared member.</summary>
    /// <param name="value">The enum value being serialized.</param>
    /// <returns>The original OpenAPI string.</returns>
    /// <exception cref="JsonException">The value is outside the declared members.</exception>
    private static string GetName(TEnum value) =>
        Members.ByValue.TryGetValue(value, out var name)
            ? name
            : throw new JsonException($"The value is not declared for {typeof(TEnum).Name}.");

    /// <summary>Indexes the attributes emitted for the schema's enum members.</summary>
    /// <returns>The string-to-member and member-to-string mappings.</returns>
    private static (Dictionary<string, TEnum>, Dictionary<TEnum, string>) GetMembers()
    {
        var byName = new Dictionary<string, TEnum>(StringComparer.Ordinal);
        var byValue = new Dictionary<TEnum, string>();
        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var name = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name;
            var value = (TEnum)field.GetValue(null)!;
            byName.Add(name, value);
            byValue.Add(value, name);
        }

        return (byName, byValue);
    }
}
