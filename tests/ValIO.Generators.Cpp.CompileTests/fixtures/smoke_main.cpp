// Smoke для std-профиля ValIO: локальный httplib::Server + сгенерированный клиент.
// Проверяет: wire-имена (order_id/status), optional-семантику (отсутствующий note),
// enum-кодек ("placed"), не-2xx → Error{Status}, void-ответ (204), async-обёртку.
#include <httplib.h>

#include <cstdio>
#ifndef VALIO_SMOKE_ASYNC
#define VALIO_SMOKE_ASYNC 1
#endif

#include <future>
#include <iterator>
#include <string>
#include <thread>

#include "Clients/StoreClient.h"
#include "Models/Order.h"
#include "Models/CodecProbe.h"
#include "Models/Empty.h"
#include "Models/Upload.h"
#include "Runtime/ValIOJson.h"
#include "Serializers/CodecProbeJson.h"
#include "Serializers/ColorJson.h"
#include "Serializers/EmptyJson.h"
#include "Serializers/OrderJson.h"
#include "Serializers/SingleBaseJson.h"
#include "Serializers/CombinedJson.h"
#include "Serializers/WideJson.h"

#if VALIO_ASYNC_ONLY
#define VALIO_CALL(method, ...) client.method##Async(__VA_ARGS__).get()
#else
#define VALIO_CALL(method, ...) client.method(__VA_ARGS__)
#endif

namespace {

int g_failures = 0;

void Check(bool condition, const char* what)
{
    if (!condition) {
        std::printf("FAIL: %s\n", what);
        ++g_failures;
    } else {
        std::printf("ok:   %s\n", what);
    }
}

const char* kCanaryBody = R"({"order_id": 42, "status": "placed"})";

} // namespace

int main()
{
    const ValIO::Expected<int, ValIO::Error> defaultResult;
    Check(defaultResult.has_value() && *defaultResult == 0, "default Expected value agrees with std::expected semantics");
    const auto wide = ValIO::Parse<Petstore::Wide>("-9223372036854775808");
    Check(wide.has_value() && *wide == Petstore::Wide::Minimum, "signed 64-bit minimum enum wire value parses");
    Check(ValIO::Dump(Petstore::Wide::Maximum) == "9223372036854775807", "signed 64-bit maximum enum wire value round-trips");
    httplib::Server server;
    server.Get("/store/order/42", [](const httplib::Request&, httplib::Response& res) {
        res.status = 200;
        res.set_content(kCanaryBody, "application/json");
    });
    server.Get("/store/order/999", [](const httplib::Request&, httplib::Response& res) {
        res.status = 404;
        res.set_content(R"({"title": "not found"})", "application/json");
    });
    server.Post("/store/order", [](const httplib::Request& request, httplib::Response& res) {
        // Сервер валидирует wire-имена запроса и эхом возвращает тело клиенту.
        if (request.body.find("\"order_id\"") == std::string::npos
            || request.body.find("\"status\"") == std::string::npos) {
            res.status = 400;
            res.set_content("wire name check failed", "text/plain");
            return;
        }
        res.status = 200;
        res.set_content(request.body, "application/json");
    });
    server.Delete("/store/order/42", [](const httplib::Request&, httplib::Response& res) {
        res.status = 204;
    });
    server.Get("/store/active", [](const httplib::Request& request, httplib::Response& res) {
        res.set_content(request.get_param_value("filter[level]") == "2" ? "true" : "false", "application/json");
    });
    const auto uploadHandler = [](const httplib::Request& request, httplib::Response& res) {
        const auto documents = request.files.equal_range("documents");
        const auto count = std::distance(documents.first, documents.second);
        const bool valid = request.is_multipart_form_data()
            && request.has_file("document.title")
            && request.get_file_value("document.title").content == "----ValIOBoundary"
            && count == 2;
        res.set_content(valid ? "true" : "false", "application/json");
    };
    server.Post("/store/upload", uploadHandler);
    server.Put("/store/upload", uploadHandler);
    server.Patch("/store/upload", uploadHandler);

    const int port = server.bind_to_any_port("127.0.0.1");
    if (port <= 0) {
        std::printf("FAIL: cannot bind smoke server\n");
        return 1;
    }
    std::thread serverThread([&] { server.listen_after_bind(); });

    ValIO::HttpConfig config;
    config.BaseUrl = "http://127.0.0.1:" + std::to_string(port);
    Petstore::StoreClient client(config);

    // 1. Happy-path: wire-имена + optional-отсутствие + enum-кодек.
    const auto fetched = VALIO_CALL(GetOrderById, 42, "extra");
    Check(fetched.has_value(), "GetOrderById(42) succeeds");
    if (fetched) {
        Check(fetched->OrderId == 42, "wire name order_id decoded");
        Check(!fetched->Note.has_value(), "absent optional note -> nullopt");
        Check(fetched->Status == Petstore::OrderStatus::Placed, "enum wire value placed decoded");
    }

    // 2. Не-2xx → Error{Status, 404}.
    const auto missing = VALIO_CALL(GetOrderById, 999);
    Check(!missing.has_value(), "404 yields error");
    if (!missing) {
        Check(missing.error().What == ValIO::Error::Code::Status, "error code is Status");
        Check(missing.error().StatusCode == 404, "status code preserved");
    }

    // 3. Тело запроса сериализуется с wire-именами (сервер проверяет и эхает).
    Petstore::Order order;
    order.OrderId = 7;
    order.Status = Petstore::OrderStatus::Approved;
    const auto placed = VALIO_CALL(PlaceOrder, order);
    Check(placed.has_value(), "PlaceOrder round-trip succeeds");
    if (placed) {
        Check(placed->OrderId == 7, "request body encoded with wire names and decoded back");
        Check(placed->Status == Petstore::OrderStatus::Approved, "enum round-trips through request");
    }

    // 4. void-ответ (204).
    const auto deleted = VALIO_CALL(DeleteOrder, 42);
    Check(deleted.has_value(), "void response (204) is a success");

#if VALIO_SMOKE_ASYNC
    // 5. Async-обёртка (std::future).
    std::future<ValIO::Expected<Petstore::Order, ValIO::Error>> asyncResult = client.GetOrderByIdAsync(42);
    const auto asyncFetched = asyncResult.get();
    Check(asyncFetched.has_value() && asyncFetched->OrderId == 42, "async wrapper resolves");
#endif

    const auto active = VALIO_CALL(GetActive, Petstore::Level::High);
    Check(active.has_value() && *active, "primitive response and integer enum query wire value");

    Petstore::Upload upload;
    upload.Title = "----ValIOBoundary";
    upload.Files = {{0, 1, 2}, {3, 4, 5}};
    const auto uploadPost = VALIO_CALL(UploadPost, upload);
    const auto uploadPut = VALIO_CALL(UploadPut, upload);
    const auto uploadPatch = VALIO_CALL(UploadPatch, upload);
    Check(uploadPost.has_value() && *uploadPost, "POST multipart retains optional text and repeated binary parts");
    Check(uploadPut.has_value() && *uploadPut, "PUT multipart retains text and repeated binary parts");
    Check(uploadPatch.has_value() && *uploadPatch, "PATCH multipart retains text and repeated binary parts");

    const auto nullable = ValIO::Parse<Petstore::CodecProbe>(
        R"({"required_null":null,"optional_null":null,"nullable_items":[null,"text"],"color":"red"})");
    Check(nullable.has_value(), "nullable model with nested nullable items parses");
    if (nullable) {
        Check(!nullable->RequiredNullable, "required nullable key retains explicit null");
        Check(nullable->OptionalNullable.has_value() && !*nullable->OptionalNullable,
            "optional nullable key distinguishes present null from missing");
        Check(nullable->NullableItems.size() == 2 && !nullable->NullableItems[0]
            && nullable->NullableItems[1] == "text", "nullable array elements retain values");
        Check(nullable->Color == Petstore::Color::Red, "second string enum has a distinct parser");
        const auto json = nlohmann::json::parse(ValIO::Dump(*nullable));
        Check(json.at("required_null").is_null() && json.at("optional_null").is_null(), "explicit null keys round-trip");

        auto reused = *nullable;
        nlohmann::json::parse(R"({"required_null":"value","nullable_items":[]})").get_to(reused);
        Check(!reused.OptionalNullable && !reused.Color, "deserialization resets missing optional fields on reused objects");
    }
    const auto absentRequired = ValIO::Parse<Petstore::CodecProbe>(R"({"nullable_items":[]})");
    Check(!absentRequired && absentRequired.error().What == ValIO::Error::Code::Parse, "missing required nullable key is rejected");
    const auto invalidOptional = ValIO::Parse<Petstore::Order>(R"({"order_id":1,"status":"placed","note":null})");
    Check(!invalidOptional, "optional non-nullable field rejects explicit null");
    Check(ValIO::Dump(Petstore::Empty{}) == "{}", "empty model serializes as an object");
    const auto invalidEmpty = ValIO::Parse<Petstore::Empty>("[]");
    Check(!invalidEmpty, "empty model rejects a non-object JSON value");
    const auto inherited = ValIO::Parse<Petstore::SingleBase>(R"({"name":"base"})");
    Check(inherited.has_value() && inherited->Name == "base", "single-base allOf codec delegates to base");
    const auto combined = ValIO::Parse<Petstore::Combined>(R"({"name":"base","id":12})");
    Check(combined.has_value() && combined->Name == "base" && combined->Id == 12, "multiple-base allOf fields flatten without losing base properties");
    if (combined) {
        const auto flattened = nlohmann::json::parse(ValIO::Dump(*combined));
        Check(flattened.at("name") == "base" && flattened.at("id") == 12, "multiple-base allOf wire properties round-trip");
    }

#if VALIO_SMOKE_ASYNC
    std::future<ValIO::Expected<Petstore::Order, ValIO::Error>> detached;
    {
        Petstore::StoreClient temporary(config);
        detached = temporary.GetOrderByIdAsync(42);
    }
    const auto detachedResult = detached.get();
    Check(detachedResult.has_value(), "async operation owns transport beyond client destruction");
#endif

    server.stop();
    serverThread.join();

    if (g_failures == 0) {
        std::printf("ALL SMOKE CHECKS PASSED\n");
    } else {
        std::printf("%d SMOKE CHECK(S) FAILED\n", g_failures);
    }
    return g_failures;
}
