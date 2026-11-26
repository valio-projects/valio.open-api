#pragma once
#include <chrono>
#include <cstdint>
#include <string>

namespace ValIO {

/** @brief A Gregorian civil date with no third-party application dependency. */
struct Date {
    int Year = 1970;
    unsigned int Month = 1;
    unsigned int Day = 1;
};
/** @brief A civil clock value retaining arbitrary decimal fractions and the RFC3339 leap-second marker. */
struct NativeTime {
    int Hour = 0;
    int Minute = 0;
    int Second = 0;
    std::string Fraction;
};
/** @brief A calendar interval that keeps years and months distinct from fixed elapsed durations. */
struct NativeDuration {
    bool Negative = false;
    std::uint64_t Years = 0, Months = 0, Weeks = 0, Days = 0, Hours = 0, Minutes = 0, Seconds = 0;
    std::string Fraction;
};

namespace TemporalDetail {
inline Date DefaultDate() { return Date{}; }
}

} // namespace ValIO
