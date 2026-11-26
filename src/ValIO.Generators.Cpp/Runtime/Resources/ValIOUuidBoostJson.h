#pragma once
#include "ValIOUuidTypes.h"
#include "ValIOJsonValidation.h"
#include <boost/uuid/string_generator.hpp>
#include <boost/uuid/uuid_io.hpp>
#include <stdexcept>

namespace ValIO::JsonDetail {

inline ValIO::Uuid ReadNativeUuid(const nlohmann::json& source, const std::string& text) {
    try {
        return boost::uuids::string_generator{}(text);
    } catch (const std::runtime_error&) {
        Reject(source, "UUID provider rejected the canonical UUID value");
    }
}

inline std::string WriteNativeUuid(const ValIO::Uuid& value) {
    return boost::uuids::to_string(value);
}

} // namespace ValIO::JsonDetail
