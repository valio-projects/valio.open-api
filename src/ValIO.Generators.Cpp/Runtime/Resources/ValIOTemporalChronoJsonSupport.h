#pragma once
#include "ValIOTemporalJsonSupport.h"

namespace ValIO::TemporalDetail {

inline NativeTime MakeTime(const int hour, const int minute, const int second, const std::string& fraction) {
    if (second == 60) Fail("native chrono clock cannot preserve an RFC3339 leap-second marker; use string or valio");
    return NativeTime(std::chrono::hours(hour) + std::chrono::minutes(minute) + std::chrono::seconds(second) + std::chrono::nanoseconds(static_cast<std::int64_t>(Nanoseconds(fraction))));
}
inline void TimeFields(const NativeTime& value, int& hour, int& minute, int& second, std::string& fraction) {
    if (value.is_negative() || value.hours().count() >= 24) Fail("invalid native chrono clock time");
    hour = static_cast<int>(value.hours().count()); minute = static_cast<int>(value.minutes().count()); second = static_cast<int>(value.seconds().count());
    fraction = TrimFraction(Pad(static_cast<std::uint64_t>(value.subseconds().count()), 9));
}

inline std::uint64_t TicksPerSecond() { return 1000000000ULL; }
inline NativeDuration FromTicks(const std::int64_t ticks) { return NativeDuration(ticks); }
inline std::int64_t DurationTicks(const NativeDuration& value) { return value.count(); }

} // namespace ValIO::TemporalDetail
