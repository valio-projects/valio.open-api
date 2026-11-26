using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

/// <summary>Formats scalar HTTP values consistently across URL, header, and form transports.</summary>
internal static class HttpWireValueFormatter
{
    /// <summary>Formats one scalar using its invariant wire representation.</summary>
    /// <param name="value">The scalar value, or null.</param>
    /// <returns>The invariant text before transport-specific escaping.</returns>
    /// <exception cref="NotSupportedException">The value is not a supported HTTP scalar.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is outside its declared members.</exception>
    internal static string Format(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        bool boolean => boolean ? "true" : "false",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        Enum enumValue => FormatEnum(enumValue),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => throw new NotSupportedException($"The HTTP scalar type {value.GetType().Name} is not supported."),
    };

    /// <summary>Formats an HTTP collection with separators outside the optional per-item escaping.</summary>
    /// <typeparam name="T">The generated scalar item type.</typeparam>
    /// <param name="values">The collection to format.</param>
    /// <param name="separator">The delimiter between adjacent items.</param>
    /// <param name="escape">Whether to percent-encode the individual items.</param>
    /// <returns>The formatted values, separated by the declared delimiter.</returns>
    internal static string JoinValues<T>(IEnumerable<T> values, string separator, bool escape) =>
        string.Join(separator, values.Select(value =>
        {
            var text = Format(value);
            return escape ? Uri.EscapeDataString(text) : text;
        }));

    /// <summary>Uses declared string wire names and underlying numeric values for enum fields.</summary>
    /// <param name="value">The declared enum member.</param>
    /// <returns>The original schema value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The enum value has no declared member.</exception>
    private static string FormatEnum(Enum value)
    {
        var type = value.GetType();
        var memberName = Enum.GetName(type, value)
            ?? throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is not declared by the schema.");
        var wireName = type.GetField(memberName)?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name;
        return wireName ?? Convert.ToString(
            Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
