#pragma once
#include <chrono>
#include <cstdint>
#include <string>

namespace ValIO {

using Date = std::chrono::year_month_day;
using NativeTime = std::chrono::hh_mm_ss<std::chrono::nanoseconds>;
using NativeDuration = std::chrono::nanoseconds;

namespace TemporalDetail {
inline Date DefaultDate() { return Date{std::chrono::year{1970}, std::chrono::month{1}, std::chrono::day{1}}; }
}

} // namespace ValIO
