using System.Diagnostics;
using System.Net;

namespace CS2Browser.Network;

/// <summary>
/// HTML, CSS, 이미지 등의 리소스를 비동기로 내려받는 로더.
/// HTTP 캐시(Cache-Control, ETag, Last-Modified, 304)를 처리하는 핸들러를 파이프라인에 연결한다.
/// </summary>
public sealed class ResourceLoader : IDisposable
{
    private readonly HttpClient _client;

    public ResourceLoader(HttpCacheManager? cacheManager = null)
    {
        var innerHandler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            UseProxy = false,
            Proxy = null,
            UseDefaultCredentials = false
        };

        _cacheManager = cacheManager ?? new HttpCacheManager();
        var cachingHandler = new CachingHttpMessageHandler(innerHandler, _cacheManager);

        _client = new HttpClient(cachingHandler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) CS2Browser/0.1");
    }

    private readonly HttpCacheManager _cacheManager;

    public Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default)
        => GetBytesCoreAsync(url, cancellationToken);

    public Task<string> GetTextAsync(string url, CancellationToken cancellationToken = default)
        => GetTextCoreAsync(url, cancellationToken);

    private async Task<byte[]> GetBytesCoreAsync(string url, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine($"[Network] {sw.ElapsedMilliseconds}ms GET(byte) {url} -> {response.StatusCode}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Network] {sw.ElapsedMilliseconds}ms GET(byte) {url} FAILED: {ex.Message}");
            throw;
        }
    }

    private async Task<string> GetTextCoreAsync(string url, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine($"[Network] {sw.ElapsedMilliseconds}ms GET {url} -> {response.StatusCode}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Network] {sw.ElapsedMilliseconds}ms GET {url} FAILED: {ex.Message}");
            throw;
        }
    }

    public void Dispose() => _client.Dispose();
}
