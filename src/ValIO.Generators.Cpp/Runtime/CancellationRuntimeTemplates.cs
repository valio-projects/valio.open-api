namespace ValIO.Generators.Cpp.Runtime;

/// <summary>Emits the C++17 cancellation contract independently of HTTP and third-party headers.</summary>
internal static class CancellationRuntimeTemplates
{
    /// <summary>Loads the packaged C++ cancellation header without modifying its content.</summary>
    /// <returns>The stable header source shared by all standalone clients.</returns>
    /// <exception cref="InvalidOperationException">The generator package is missing its cancellation header.</exception>
    public static string Header()
    {
        using var stream = typeof(CancellationRuntimeTemplates).Assembly.GetManifestResourceStream("ValIO.Runtime.ValIOCancellation.h");
        if (stream is null)
            throw new InvalidOperationException("The C++ generator package is missing ValIOCancellation.h.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Emits lifetime-safe cancellation subscriptions inside the sole HTTP translation unit.</summary>
    /// <returns>The cancellation implementation fragment.</returns>
    public static string Source() => """
namespace detail {

struct CancellationCallback {
    std::mutex Mutex;
    bool Active = true;
    std::function<void()> Invoke;
};

struct CancellationState {
    std::atomic<bool> Cancelled{false};
    std::mutex Mutex;
    std::size_t NextId = 0;
    std::map<std::size_t, std::shared_ptr<CancellationCallback>> Callbacks;
};

class CancellationRegistration {
public:
    CancellationRegistration(const CancellationToken& token, std::function<void()> callback)
        : _state(token._state) {
        if (!_state) {
            return;
        }
        _callback = std::make_shared<CancellationCallback>();
        _callback->Invoke = std::move(callback);
        bool invokeImmediately;
        {
            const std::lock_guard<std::mutex> guard(_state->Mutex);
            invokeImmediately = _state->Cancelled.load(std::memory_order_acquire);
            if (!invokeImmediately) {
                _id = ++_state->NextId;
                _state->Callbacks.emplace(_id, _callback);
            }
        }
        if (invokeImmediately) {
            _callback->Invoke();
        }
    }

    ~CancellationRegistration() {
        if (!_callback) {
            return;
        }
        {
            const std::lock_guard<std::mutex> guard(_callback->Mutex);
            _callback->Active = false;
        }
        if (_state) {
            const std::lock_guard<std::mutex> guard(_state->Mutex);
            _state->Callbacks.erase(_id);
        }
    }

    CancellationRegistration(const CancellationRegistration&) = delete;
    CancellationRegistration& operator=(const CancellationRegistration&) = delete;

private:
    std::shared_ptr<CancellationState> _state;
    std::shared_ptr<CancellationCallback> _callback;
    std::size_t _id = 0;
};

} // namespace detail

bool CancellationToken::IsCancellationRequested() const noexcept {
    return _state && _state->Cancelled.load(std::memory_order_acquire);
}

CancellationToken::CancellationToken(std::shared_ptr<detail::CancellationState> state) noexcept : _state(std::move(state)) {}

CancellationSource::CancellationSource() : _state(std::make_shared<detail::CancellationState>()) {}

CancellationToken CancellationSource::GetToken() const noexcept {
    return CancellationToken(_state);
}

bool CancellationSource::Cancel() const {
    if (!_state) {
        return false;
    }
    if (_state->Cancelled.exchange(true, std::memory_order_acq_rel)) {
        return false;
    }
    std::vector<std::shared_ptr<detail::CancellationCallback>> callbacks;
    {
        const std::lock_guard<std::mutex> guard(_state->Mutex);
        for (const auto& entry : _state->Callbacks) {
            callbacks.push_back(entry.second);
        }
    }
    for (const auto& callback : callbacks) {
        const std::lock_guard<std::mutex> guard(callback->Mutex);
        if (callback->Active) {
            callback->Invoke();
        }
    }
    return true;
}
""";
}
