// Smoke для std-профиля ValIO: локальный httplib::Server + сгенерированный клиент.
// Проверяет: wire-имена (order_id/status), optional-семантику (отсутствующий note),
// enum-кодек ("placed"), не-2xx → Error{Status}, void-ответ (204), async-обёртку.
#include <httplib.h>

#include <cstdio>
#ifndef VALIO_SMOKE_ASYNC
#define VALIO_SMOKE_ASYNC 1
#endif

#include <future>
#include <string>
#include <thread>

#include "Clients/StoreClient.h"
#include "Models/Order.h"

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
    const auto fetched = client.GetOrderById(42, "extra");
    Check(fetched.has_value(), "GetOrderById(42) succeeds");
    if (fetched) {
        Check(fetched->OrderId == 42, "wire name order_id decoded");
        Check(!fetched->Note.has_value(), "absent optional note -> nullopt");
        Check(fetched->Status == Petstore::OrderStatus::Placed, "enum wire value placed decoded");
    }

    // 2. Не-2xx → Error{Status, 404}.
    const auto missing = client.GetOrderById(999);
    Check(!missing.has_value(), "404 yields error");
    if (!missing) {
        Check(missing.error().What == ValIO::Error::Code::Status, "error code is Status");
        Check(missing.error().StatusCode == 404, "status code preserved");
    }

    // 3. Тело запроса сериализуется с wire-именами (сервер проверяет и эхает).
    Petstore::Order order;
    order.OrderId = 7;
    order.Status = Petstore::OrderStatus::Approved;
    const auto placed = client.PlaceOrder(order);
    Check(placed.has_value(), "PlaceOrder round-trip succeeds");
    if (placed) {
        Check(placed->OrderId == 7, "request body encoded with wire names and decoded back");
        Check(placed->Status == Petstore::OrderStatus::Approved, "enum round-trips through request");
    }

    // 4. void-ответ (204).
    const auto deleted = client.DeleteOrder(42);
    Check(deleted.has_value(), "void response (204) is a success");

#if VALIO_SMOKE_ASYNC
    // 5. Async-обёртка (std::future).
    std::future<ValIO::Expected<Petstore::Order, ValIO::Error>> asyncResult = client.GetOrderByIdAsync(42);
    const auto asyncFetched = asyncResult.get();
    Check(asyncFetched.has_value() && asyncFetched->OrderId == 42, "async wrapper resolves");
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
