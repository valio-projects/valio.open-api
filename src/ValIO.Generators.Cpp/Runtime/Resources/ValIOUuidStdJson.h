#pragma once
#include "ValIOUuidTypes.h"
#include "ValIOJsonValidation.h"

namespace ValIO::JsonDetail {

inline ValIO::Uuid ReadNativeUuid(const nlohmann::json& source, const std::string& text) {
    const auto parsed = uuids::uuid::from_string(std::string_view(text));
    if (!parsed) Reject(source, "UUID provider rejected the canonical UUID value");
    return *parsed;
}

inline std::string WriteNativeUuid(const ValIO::Uuid& value) {
    return uuids::to_string(value);
}

} // namespace ValIO::JsonDetail
