#pragma once
#include "ValIOUuidTypes.h"
#include "ValIOJsonValidation.h"

namespace ValIO::JsonDetail {

inline ValIO::Uuid ReadNativeUuid(const nlohmann::json& source, const std::string& text) {
    const auto parsed = muuid::uuid::from_chars(text);
    if (!parsed) Reject(source, "UUID provider rejected the canonical UUID value");
    return *parsed;
}

inline std::string WriteNativeUuid(const ValIO::Uuid& value) {
    return value.to_string();
}

} // namespace ValIO::JsonDetail
