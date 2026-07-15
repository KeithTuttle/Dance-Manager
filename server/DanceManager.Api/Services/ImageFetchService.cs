using System.Net;
using System.Net.Sockets;

namespace DanceManager.Api.Services;

/// <summary>
/// Fetches an external image URL into bytes for embedding in a PDF, with
/// guardrails against the fact that the URL is arbitrary user input:
/// only http/https, no private/loopback/link-local targets (SSRF), a short
/// timeout, an image-only content type, a size cap, and a decode probe so a
/// non-image response can never break PDF generation. Returns null on any
/// failure — the caller falls back to printing the link as text.
/// </summary>
public class ImageFetchService
{
    /// <summary>Named HttpClient configured (in Program.cs) with redirects disabled.</summary>
    public const string HttpClientName = "imagefetch";

    private readonly IHttpClientFactory _httpFactory;
    private const int MaxBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public ImageFetchService(IHttpClientFactory httpFactory) => _httpFactory = httpFactory;

    public async Task<byte[]?> TryFetchAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

        try
        {
            // Resolve the host and refuse any non-public address (basic SSRF guard).
            IPAddress[] addresses = IPAddress.TryParse(uri.Host, out var literal)
                ? new[] { literal }
                : await Dns.GetHostAddressesAsync(uri.Host, ct);
            if (addresses.Length == 0 || addresses.Any(IsDisallowed)) return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);

            var client = _httpFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", "DanceManager/1.0");
            using var resp = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            // Redirects are disabled: a 3xx (or any non-success) is treated as a miss.
            if (!resp.IsSuccessStatusCode) return null;

            var contentType = resp.Content.Headers.ContentType?.MediaType;
            if (contentType is null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return null;

            if (resp.Content.Headers.ContentLength is > MaxBytes) return null;

            var bytes = await ReadCappedAsync(resp, cts.Token);
            if (bytes is null) return null;

            // Probe-decode so an invalid image can't throw during GeneratePdf.
            try { _ = QuestPDF.Infrastructure.Image.FromBinaryData(bytes); }
            catch { return null; }

            return bytes;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadCappedAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    /// <summary>True for loopback/private/link-local/unique-local/other non-public addresses.</summary>
    private static bool IsDisallowed(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            // 10/8, 172.16/12, 192.168/16, 169.254/16 (link-local), 127/8, 100.64/10 (CGNAT)
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254)
                || b[0] == 127
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
            // Unique local fc00::/7
            var b = ip.GetAddressBytes();
            return (b[0] & 0xFE) == 0xFC;
        }

        return true; // unknown family — refuse
    }
}
