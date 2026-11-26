#pragma once
#include <chrono>
#include <cstdint>
#include <string>
#include <date/date.h>

namespace ValIO {

using Date = date::year_month_day;
using NativeTime = date::hh_mm_ss<std::chrono::nanoseconds>;
using NativeDuration = std::chrono::nanoseconds;

namespace TemporalDetail {
inline Date DefaultDate() { return Date{date::year{1970}, date::month{1}, date::day{1}}; }
}

} // namespace ValIO
