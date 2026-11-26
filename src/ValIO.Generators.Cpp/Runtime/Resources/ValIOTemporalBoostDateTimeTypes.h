#pragma once
#include <chrono>
#include <cstdint>
#include <string>
#include <boost/date_time/gregorian/gregorian.hpp>
#include <boost/date_time/posix_time/posix_time.hpp>

namespace ValIO {

using Date = boost::gregorian::date;
using NativeTime = boost::posix_time::time_duration;
using NativeDuration = boost::posix_time::time_duration;

namespace TemporalDetail {
inline Date DefaultDate() { return Date{1970, 1, 1}; }
}

} // namespace ValIO
