using System.Globalization;
using System.Text.Json;

namespace ValIO.Core.Utilities;

/// <summary>Reads invariant JSON numbers without accepting decimal rounding or underflow.</summary>
public static class ExactDecimal
{
    /// <summary>Parses a number only if its exact value fits the CLR decimal representation.</summary>
    /// <param name="text">The original JSON numeric token.</param>
    /// <param name="value">The exact decimal value when parsing succeeds.</param>
    /// <returns>Whether the token is a JSON number representable without arithmetic loss.</returns>
    public static bool TryParse(string? text, out decimal value)
    {
        value = default;
        if (text is null || !decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        try
        {
            using var source = JsonDocument.Parse(text);
            using var roundTrip = JsonDocument.Parse(parsed.ToString(CultureInfo.InvariantCulture));
            if (source.RootElement.ValueKind != JsonValueKind.Number ||
                !JsonElement.DeepEquals(source.RootElement, roundTrip.RootElement))
            {
                return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        value = parsed;
        return true;
    }
}
