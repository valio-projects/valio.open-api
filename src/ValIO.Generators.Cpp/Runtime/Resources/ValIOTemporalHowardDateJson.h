#pragma once
#include "ValIOTemporalChronoJsonSupport.h"

namespace ValIO::TemporalDetail {

inline Date MakeDate(const int year, const int month, const int day) { return Date{date::year{year}, date::month{static_cast<unsigned int>(month)}, date::day{static_cast<unsigned int>(day)}}; }
inline void DateFields(const Date& value, int& year, int& month, int& day) { year = static_cast<int>(value.year()); month = static_cast<int>(static_cast<unsigned int>(value.month())); day = static_cast<int>(static_cast<unsigned int>(value.day())); }

} // namespace ValIO::TemporalDetail
