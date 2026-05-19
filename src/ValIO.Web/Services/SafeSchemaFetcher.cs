using System.Net;
using System.Net.Sockets;

namespace ValIO.Web.Services;

/// <summary>
/// Default <see cref="ISafeSchemaFetcher"/>:
///   - URL must be http or https
///   - Resolved IP must not be loopback, link-local, or RFC1918 private
///   - Saves uploaded bytes to %TEMP%/valio-uploads/&lt;guid&gt;.&lt;ext&gt;
///   - Size cap: 10 MB
/// </summary>
public sealed class SafeSchemaFetcher : ISafeSchemaFetcher
{
    private const int MaxSchemaBytes = 10 * 1024 * 1024;

    public async Task<string> PrepareUrlAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL");
        if (uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Only http(s) URLs are allowed");

        var addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
        foreach (var ip in addresses)
        {
            if (IsBlocked(ip))
                throw new ArgumentException($"URL host resolves to a blocked address ({ip})");
        }

        // The actual fetch happens inside OpenApiDocumentParser. We only validated the URL —
        // returning it as the orchestrator input.
        return url;
    }

    public async Task<string> PrepareUploadAsync(string fileName, byte[] content, CancellationToken ct = default)
    {
        if (content.Length == 0)
            throw new ArgumentException("Uploaded schema is empty");
        if (content.Length > MaxSchemaBytes)
            throw new ArgumentException($"Uploaded schema exceeds {MaxSchemaBytes / 1024 / 1024} MB limit");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || (ext is not ".json" and not ".yaml" and not ".yml"))
            ext = DetectExtension(content);

        var dir = Path.Combine(Path.GetTempPath(), "valio-uploads");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ext);
        await File.WriteAllBytesAsync(path, content, ct);
        return path;
    }

    private static string DetectExtension(byte[] content)
    {
        var head = System.Text.Encoding.UTF8.GetString(content, 0, Math.Min(content.Length, 256)).TrimStart();
        return head.StartsWith('{') || head.StartsWith('[') ? ".json" : ".yaml";
    }

    private static bool IsBlocked(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 10) return true;
            if (bytes[0] == 127) return true;
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
        }
        return false;
    }
}
