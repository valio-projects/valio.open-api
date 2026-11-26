#pragma once
#include "ValIOTemporalJsonSupport.h"

namespace ValIO::TemporalDetail {

inline Duration MakeDuration(const JsonDetail::IntervalParts& parts) {
    if (Component(parts.Years) != 0 || Component(parts.Months) != 0) Fail("calendar years/months cannot be represented by a fixed native duration; use string or valio");
    const std::uint64_t rate = TicksPerSecond();
    const std::uint64_t limit = static_cast<std::uint64_t>((std::numeric_limits<std::int64_t>::max)()) + (parts.Negative ? 1ULL : 0ULL);
    std::uint64_t seconds = 0;
    const auto add = [&seconds, limit, rate](const std::string& digits, const std::uint64_t scale) {
        const auto component = Component(digits);
        if (component > (limit / rate - seconds) / scale) Fail("interval exceeds native duration range");
        seconds += component * scale;
    };
    add(parts.Weeks, 604800); add(parts.Days, 86400); add(parts.Hours, 3600); add(parts.Minutes, 60); add(parts.Seconds, 1);
    const auto nanos = Nanoseconds(parts.Fraction);
    const auto tickNanos = 1000000000ULL / rate;
    if (nanos % tickNanos != 0) Fail("interval fraction cannot be represented exactly by native ticks");
    const auto fractionalTicks = nanos / tickNanos;
    if (fractionalTicks > limit - seconds * rate) Fail("interval exceeds native duration range");
    const auto magnitude = seconds * rate + fractionalTicks;
    const auto signedTicks = parts.Negative
        ? (magnitude == static_cast<std::uint64_t>((std::numeric_limits<std::int64_t>::max)()) + 1ULL ? (std::numeric_limits<std::int64_t>::min)() : -static_cast<std::int64_t>(magnitude))
        : static_cast<std::int64_t>(magnitude);
    Duration result;
    result.Value = FromTicks(signedTicks);
    result.FractionDigits = parts.Fraction.size();
    return result;
}
inline JsonDetail::IntervalParts DurationParts(const Duration& value) {
    const auto ticks = DurationTicks(value.Value);
    const std::uint64_t rate = TicksPerSecond();
    const auto magnitude = ticks < 0 ? static_cast<std::uint64_t>(-(ticks + 1)) + 1ULL : static_cast<std::uint64_t>(ticks);
    auto seconds = magnitude / rate;
    JsonDetail::IntervalParts result;
    result.Negative = ticks < 0;
    result.Days = std::to_string(seconds / 86400); seconds %= 86400;
    result.Hours = std::to_string(seconds / 3600); seconds %= 3600;
    result.Minutes = std::to_string(seconds / 60); result.Seconds = std::to_string(seconds % 60);
    const auto nanos = (magnitude % rate) * (1000000000ULL / rate);
    result.Fraction = TrimFraction(Pad(nanos, 9));
    if (result.Fraction.size() < value.FractionDigits) result.Fraction.append(value.FractionDigits - result.Fraction.size(), '0');
    return result;
}

} // namespace ValIO::TemporalDetail
