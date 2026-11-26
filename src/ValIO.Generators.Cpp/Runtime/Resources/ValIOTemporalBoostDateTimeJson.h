#pragma once
#include "ValIOTemporalJsonSupport.h"

namespace ValIO::TemporalDetail {

inline Date MakeDate(const int year, const int month, const int day) { return Date{static_cast<unsigned short>(year), static_cast<unsigned short>(month), static_cast<unsigned short>(day)}; }
inline void DateFields(const Date& value, int& year, int& month, int& day) { if (value.is_special()) Fail("special Boost date is not an RFC3339 calendar date"); year = static_cast<int>(value.year()); month = static_cast<int>(value.month()); day = static_cast<int>(value.day()); }

inline NativeTime MakeTime(const int hour, const int minute, const int second, const std::string& fraction) {
    if (second == 60) Fail("Boost native time cannot preserve an RFC3339 leap-second marker; use string or valio");
    const auto nanos = Nanoseconds(fraction);
    const auto tickNanos = 1000000000ULL / static_cast<std::uint64_t>(NativeTime::ticks_per_second());
    if (nanos % tickNanos != 0) Fail("fraction cannot be represented exactly by Boost.Date_Time tick precision");
    return NativeTime(hour, minute, second, static_cast<NativeTime::fractional_seconds_type>(nanos / tickNanos));
}
inline void TimeFields(const NativeTime& value, int& hour, int& minute, int& second, std::string& fraction) {
    if (value.is_special() || value.is_negative() || value.hours() >= 24) Fail("invalid Boost clock time");
    hour = static_cast<int>(value.hours()); minute = static_cast<int>(value.minutes()); second = static_cast<int>(value.seconds());
    fraction = TrimFraction(Pad(static_cast<std::uint64_t>(value.fractional_seconds()), NativeTime::num_fractional_digits()));
}

inline std::uint64_t TicksPerSecond() { return static_cast<std::uint64_t>(NativeDuration::ticks_per_second()); }
inline NativeDuration FromTicks(const std::int64_t ticks) {
    const auto value = NativeDuration(0, 0, 0, static_cast<NativeDuration::fractional_seconds_type>(ticks));
    if (value.is_special()) Fail("duration collides with a reserved Boost special value");
    return value;
}
inline std::int64_t DurationTicks(const NativeDuration& value) {
    if (value.is_special()) Fail("special Boost duration cannot be serialized");
    return static_cast<std::int64_t>(value.ticks());
}

} // namespace ValIO::TemporalDetail
