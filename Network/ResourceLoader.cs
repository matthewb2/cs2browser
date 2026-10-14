using System.Net;
using System.Net.Http;

namespace CS2Browser.Network;

/// <summary>
/// HTML, CSS, 이미지 등 외부 리소스를 비동기로 내려받는 로더.
/// AngleSharp 의 기본 로더(WithDefaultLoader)와 함께 사용된다.
/// </summary>
public sealed class ResourceLoader : IDisposable
{
    private readonly HttpClient _client;

    public ResourceLoader()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All
        };

        _client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) CS2Browser/0.1");
    }

    public Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default)
        => GetBytesCoreAsync(url, cancellationToken);

    public Task<string> GetTextAsync(string url, CancellationToken cancellationToken = default)
        => GetTextCoreAsync(url, cancellationToken);

    private async Task<byte[]> GetBytesCoreAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetTextCoreAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _client.Dispose();
}
