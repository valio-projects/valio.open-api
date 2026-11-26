#pragma once
#include "ValIOTemporalJsonSupport.h"

namespace ValIO::TemporalDetail {

inline Date MakeDate(const int year, const int month, const int day) { return Date{year, static_cast<unsigned int>(month), static_cast<unsigned int>(day)}; }
inline void DateFields(const Date& value, int& year, int& month, int& day) { year = value.Year; month = static_cast<int>(value.Month); day = static_cast<int>(value.Day); }

inline Duration MakeDuration(const JsonDetail::IntervalParts& parts) {
    Duration result;
    result.Value = NativeDuration{parts.Negative, Component(parts.Years), Component(parts.Months), Component(parts.Weeks), Component(parts.Days), Component(parts.Hours), Component(parts.Minutes), Component(parts.Seconds), parts.Fraction};
    result.FractionDigits = parts.Fraction.size();
    return result;
}
inline JsonDetail::IntervalParts DurationParts(const Duration& value) {
    JsonDetail::IntervalParts result;
    result.Negative = value.Value.Negative;
    result.Years = std::to_string(value.Value.Years); result.Months = std::to_string(value.Value.Months); result.Weeks = std::to_string(value.Value.Weeks); result.Days = std::to_string(value.Value.Days);
    result.Hours = std::to_string(value.Value.Hours); result.Minutes = std::to_string(value.Value.Minutes); result.Seconds = std::to_string(value.Value.Seconds); result.Fraction = value.Value.Fraction;
    return result;
}

inline NativeTime MakeTime(const int hour, const int minute, const int second, const std::string& fraction) { return NativeTime{hour, minute, second, fraction}; }
inline void TimeFields(const NativeTime& value, int& hour, int& minute, int& second, std::string& fraction) { hour = value.Hour; minute = value.Minute; second = value.Second; fraction = value.Fraction; }

} // namespace ValIO::TemporalDetail
