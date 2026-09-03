# Compile-fixture для std-профиля ValIO.
# Пины совпадают с conan-графом gta-sa-rw: cpp-httplib v0.18.3 + nlohmann_json v3.12.0 (EXACT).
# GEN_DIR — корень сгенерированного вывода (передаётся -D из теста).
cmake_minimum_required(VERSION 3.16)
project(ValIO_Cpp_CompileFixture LANGUAGES CXX)

if(NOT DEFINED GEN_DIR)
    message(FATAL_ERROR "GEN_DIR must point at the generated output root")
endif()

set(CMAKE_CXX_STANDARD_REQUIRED ON)

# VALIO_SMOKE_ASYNC передаётся из теста (-DVALIO_SMOKE_ASYNC=0|1):
# smoke-файл использует async-методы только когда генерация их создала.
if(NOT DEFINED VALIO_SMOKE_ASYNC)
    set(VALIO_SMOKE_ASYNC 1)
endif()
add_compile_definitions(VALIO_SMOKE_ASYNC=${VALIO_SMOKE_ASYNC})

include(FetchContent)

FetchContent_Declare(httplib
    GIT_REPOSITORY https://github.com/yhirose/cpp-httplib.git
    GIT_TAG        v0.18.3)
FetchContent_MakeAvailable(httplib)

FetchContent_Declare(json
    GIT_REPOSITORY https://github.com/nlohmann/json.git
    GIT_TAG        v3.12.0)
FetchContent_MakeAvailable(json)

file(GLOB_RECURSE VALIO_SOURCES CONFIGURE_DEPENDS "${GEN_DIR}/*.cpp")
if(NOT VALIO_SOURCES)
    message(FATAL_ERROR "no generated sources found under ${GEN_DIR}")
endif()

add_library(valio_generated STATIC ${VALIO_SOURCES})
target_include_directories(valio_generated PUBLIC "${GEN_DIR}")
target_link_libraries(valio_generated PUBLIC httplib nlohmann_json::nlohmann_json)

add_executable(smoke_main smoke_main.cpp)
target_link_libraries(smoke_main PRIVATE valio_generated)

if(MSVC)
    target_compile_options(valio_generated PRIVATE /W4 /WX)
    target_compile_options(smoke_main PRIVATE /W4 /WX)
else()
    target_compile_options(valio_generated PRIVATE -Wall -Wextra -Werror)
    target_compile_options(smoke_main PRIVATE -Wall -Wextra -Werror)
endif()
