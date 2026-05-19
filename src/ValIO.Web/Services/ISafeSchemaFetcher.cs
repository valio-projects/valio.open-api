namespace ValIO.Web.Services;

/// <summary>
/// Prepares an OpenAPI schema for the orchestrator from untrusted input.
/// Implementations are responsible for:
///   - Validating URLs (http/https only, no private/loopback ranges) to mitigate SSRF.
///   - Persisting uploaded bytes to a temp file with a known JSON/YAML extension.
///   - Enforcing size and timeout limits.
/// Returns a local filesystem path the orchestrator can read.
/// </summary>
public interface ISafeSchemaFetcher
{
    Task<string> PrepareUrlAsync(string url, CancellationToken ct = default);
    Task<string> PrepareUploadAsync(string fileName, byte[] content, CancellationToken ct = default);
}
