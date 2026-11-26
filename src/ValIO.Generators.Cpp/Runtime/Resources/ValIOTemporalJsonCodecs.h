#pragma once
#include "ValIOTemporalJsonSupport.h"

namespace ValIO {
namespace TemporalDetail {

/** @brief Parses a checked date through the selected provider's calendar constructor. */
inline Date ReadDate(const std::string_view text) {
    return MakeDate(JsonDetail::Digits(text, 0, 4), JsonDetail::Digits(text, 5, 2), JsonDetail::Digits(text, 8, 2));
}

/** @brief Formats a selected-provider date and verifies the complete Gregorian calendar contract. */
inline std::string DateText(const Date& value) {
    int year = 0, month = 0, day = 0;
    DateFields(value, year, month, day);
    if (year < 0 || year > 9999 || month < 1 || day < 1) Fail("date exceeds RFC3339 four-digit calendar representation");
    const auto text = Pad(static_cast<std::uint64_t>(year), 4) + "-" + Pad(static_cast<std::uint64_t>(month), 2) + "-" + Pad(static_cast<std::uint64_t>(day), 2);
    if (!JsonDetail::IsDate(text)) Fail("invalid provider calendar date");
    return text;
}

/** @brief Reads local civil time without discarding the original offset or fractional width. */
inline Time ReadTime(const std::string_view text) {
    std::size_t offset = 8;
    std::string fraction;
    if (text[offset] == '.') {
        const auto first = ++offset;
        while (offset < text.size() && text[offset] >= '0' && text[offset] <= '9') ++offset;
        fraction = std::string(text.substr(first, offset - first));
    }
    Time result;
    result.Value = MakeTime(JsonDetail::Digits(text, 0, 2), JsonDetail::Digits(text, 3, 2), JsonDetail::Digits(text, 6, 2), fraction);
    result.FractionDigits = fraction.size();
    result.Zulu = text[offset] == 'Z' || text[offset] == 'z';
    result.LowercaseZulu = text[offset] == 'z';
    if (!result.Zulu) {
        const auto minutes = JsonDetail::Digits(text, offset + 1, 2) * 60 + JsonDetail::Digits(text, offset + 4, 2);
        result.UtcOffset = std::chrono::minutes(text[offset] == '-' ? -minutes : minutes);
        result.UnknownOffset = text[offset] == '-' && minutes == 0;
    }
    return result;
}

/** @brief Formats native clock components and restores retained offset and fractional-width metadata. */
inline std::string TimeText(const Time& value) {
    int hour = 0, minute = 0, second = 0;
    std::string fraction;
    TimeFields(value.Value, hour, minute, second, fraction);
    if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 60) Fail("invalid provider clock value");
    if (fraction.size() < value.FractionDigits) fraction.append(value.FractionDigits - fraction.size(), '0');
    auto text = Pad(static_cast<std::uint64_t>(hour), 2) + ":" + Pad(static_cast<std::uint64_t>(minute), 2) + ":" + Pad(static_cast<std::uint64_t>(second), 2);
    if (!fraction.empty()) text += "." + fraction;
    const auto offset = value.UtcOffset.count();
    if (offset < -1439 || offset > 1439) Fail("UTC offset exceeds RFC3339 range");
    if (value.UnknownOffset) {
        if (offset != 0) Fail("unknown local offset must retain the -00:00 convention");
        text += "-00:00";
    } else if (value.Zulu && offset == 0) text += value.LowercaseZulu ? "z" : "Z";
    else {
        const auto magnitude = static_cast<std::uint64_t>(offset < 0 ? -offset : offset);
        text += offset < 0 ? "-" : "+";
        text += Pad(magnitude / 60, 2) + ":" + Pad(magnitude % 60, 2);
    }
    if (!JsonDetail::IsTime(text)) Fail("invalid provider time wire representation");
    return text;
}

/** @brief Converts an interval to the explicitly requested ISO duration or time-span wire convention. */
inline std::string IntervalText(const JsonDetail::IntervalParts& parts, const JsonStringFormat format) {
    if (format == JsonStringFormat::TimeSpan) {
        if (Component(parts.Years) != 0 || Component(parts.Months) != 0) Fail("calendar years/months cannot be represented as a fixed time-span");
        const auto weeks = Component(parts.Weeks);
        const auto days = Component(parts.Days);
        if (weeks > ((std::numeric_limits<std::uint64_t>::max)() - days) / 7) Fail("time-span days exceed provider range");
        const auto dayCount = days + weeks * 7;
        const auto hours = Component(parts.Hours);
        const auto minutes = Component(parts.Minutes);
        const auto seconds = Component(parts.Seconds);
        if (hours > 23 || minutes > 59 || seconds > 59) Fail("time-span clock components must be normalized before formatting");
        auto text = std::string(parts.Negative ? "-" : "");
        if (dayCount != 0) text += std::to_string(dayCount) + ".";
        text += Pad(hours, 2) + ":" + Pad(minutes, 2) + ":" + Pad(seconds, 2);
        if (!parts.Fraction.empty()) text += "." + parts.Fraction;
        return text;
    }
    if (parts.Negative) Fail("negative intervals require the explicit time-span wire format");
    std::string text = "P";
    const auto append = [&text](const std::string& value, const char unit) { if (Component(value) != 0) { text += value; text += unit; } };
    append(parts.Years, 'Y'); append(parts.Months, 'M'); append(parts.Weeks, 'W'); append(parts.Days, 'D');
    if (Component(parts.Hours) != 0 || Component(parts.Minutes) != 0 || Component(parts.Seconds) != 0 || !parts.Fraction.empty()) {
        text += 'T';
        append(parts.Hours, 'H'); append(parts.Minutes, 'M');
        if (Component(parts.Seconds) != 0 || !parts.Fraction.empty()) {
            text += parts.Seconds.empty() ? "0" : parts.Seconds;
            if (!parts.Fraction.empty()) text += "." + parts.Fraction;
            text += 'S';
        }
    }
    if (text == "P") text = "PT0S";
    if (!JsonDetail::IsInterval(text, JsonStringFormat::Duration)) Fail("invalid calendar interval wire representation");
    return text;
}

} // namespace TemporalDetail

/** @brief Formats a native date for a JSON, path, query, header, or form boundary. */
inline std::string ToWireText(const Date& value, const JsonStringFormat format = JsonStringFormat::Date) {
    (void)format;
    try { return TemporalDetail::DateText(value); }
    catch (const nlohmann::json::exception&) { throw; }
    catch (const std::exception& error) { TemporalDetail::Fail(error.what()); }
}
/** @brief Formats local native clock time with its explicit RFC3339 UTC offset. */
inline std::string ToWireText(const Time& value, const JsonStringFormat format = JsonStringFormat::Time) {
    (void)format;
    return TemporalDetail::TimeText(value);
}
/** @brief Formats a native date and clock while retaining the original RFC3339 offset and precision. */
inline std::string ToWireText(const DateTime& value, const JsonStringFormat format = JsonStringFormat::DateTime) {
    (void)format;
    return ToWireText(value.DateValue) + (value.LowercaseSeparator ? "t" : "T") + ToWireText(value.TimeValue);
}
/** @brief Formats a checked fixed or calendar interval without averaging calendar years or months. */
inline std::string ToWireText(const Duration& value, const JsonStringFormat format = JsonStringFormat::Duration) {
    return TemporalDetail::IntervalText(TemporalDetail::DurationParts(value), format);
}

namespace JsonDetail {

/** @brief Parses and formats the selected provider's actual calendar date. */
template<> struct Codec<Date> {
    static Date Read(const nlohmann::json& source, JsonStringFormat) {
        const auto text = ReadJson<std::string>(source, JsonStringFormat::Date);
        try { return TemporalDetail::ReadDate(text); }
        catch (const nlohmann::json::exception&) { throw; }
        catch (const std::exception& error) { Reject(source, error.what()); }
    }
    static nlohmann::json Write(const Date& value, JsonStringFormat) { return ToWireText(value); }
};
/** @brief Parses and formats a selected-provider clock value with explicit offset semantics. */
template<> struct Codec<Time> {
    static Time Read(const nlohmann::json& source, JsonStringFormat) {
        const auto text = ReadJson<std::string>(source, JsonStringFormat::Time);
        return TemporalDetail::ReadTime(text);
    }
    static nlohmann::json Write(const Time& value, JsonStringFormat) { return ToWireText(value); }
};
/** @brief Parses and formats provider calendar and clock components without narrowing the supported date range. */
template<> struct Codec<DateTime> {
    static DateTime Read(const nlohmann::json& source, JsonStringFormat) {
        const auto text = ReadJson<std::string>(source, JsonStringFormat::DateTime);
        DateTime result;
        try { result.DateValue = TemporalDetail::ReadDate(std::string_view(text).substr(0, 10)); }
        catch (const nlohmann::json::exception&) { throw; }
        catch (const std::exception& error) { Reject(source, error.what()); }
        result.TimeValue = TemporalDetail::ReadTime(std::string_view(text).substr(11));
        result.LowercaseSeparator = text[10] == 't';
        return result;
    }
    static nlohmann::json Write(const DateTime& value, JsonStringFormat) { return ToWireText(value); }
};
/** @brief Parses fixed native durations or explicit calendar intervals according to the schema's preserved format. */
template<> struct Codec<Duration> {
    static Duration Read(const nlohmann::json& source, const JsonStringFormat format) {
        const auto wireFormat = format == JsonStringFormat::TimeSpan ? JsonStringFormat::TimeSpan : JsonStringFormat::Duration;
        const auto text = ReadJson<std::string>(source, wireFormat);
        IntervalParts parts;
        if (wireFormat == JsonStringFormat::TimeSpan) (void)ParseTimeSpanParts(text, parts);
        else (void)ParseDurationParts(text, parts);
        return TemporalDetail::MakeDuration(parts);
    }
    static nlohmann::json Write(const Duration& value, const JsonStringFormat format) { return ToWireText(value, format); }
};

} // namespace JsonDetail
} // namespace ValIO
