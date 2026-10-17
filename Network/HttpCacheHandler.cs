using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CS2Browser.Network;

/// <summary>
/// 캐시된 리소스의 메타데이터와 바이트 데이터를 담는 엔티티.
/// </summary>
public sealed class CacheEntry
{
    public required string Url { get; init; }
    public required byte[] Data { get; init; }
    public required string ContentType { get; init; }
    public string? ETag { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool NoStore { get; init; }
    public bool NoCache { get; init; }

    /// <summary>만료 시간 내에 있고 no-cache가 아니면 신선(Fresh)한 상태로 판단한다.</summary>
    public bool IsFresh => DateTimeOffset.UtcNow < ExpiresAt && !NoCache;
}

/// <summary>
/// 메모리 및 디스크 캐시를 관리하는 매니저.
/// </summary>
public sealed class HttpCacheManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly ConcurrentDictionary<string, CacheEntry> _memoryCache = new(StringComparer.Ordinal);
    private readonly string? _diskCachePath;

    public HttpCacheManager(string? diskCachePath = null)
    {
        _diskCachePath = string.IsNullOrWhiteSpace(diskCachePath) ? null : diskCachePath;
        if (_diskCachePath is not null)
        {
            Directory.CreateDirectory(_diskCachePath);
        }
    }

    /// <summary>사용자 로컬 앱 데이터 폴더를 사용하는 디스크 캐시 매니저를 생성한다.</summary>
    public static HttpCacheManager CreateDefault()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CS2Browser",
            "Cache");
        return new HttpCacheManager(path);
    }

    /// <summary>
    /// 요청 URL에 대한 캐시 상태를 조회한다. 만료된 항목이면 재검증용 헤더를 요청에 추가한다.
    /// </summary>
    public CacheResult Get(string url, HttpRequestMessage request)
    {
        var entry = _memoryCache.TryGetValue(url, out var memory) ? memory : TryLoadFromDisk(url);
        if (entry is null)
        {
            return CacheResult.Miss();
        }

        _memoryCache[url] = entry;

        if (entry.IsFresh)
        {
            return CacheResult.Hit(entry);
        }

        if (!string.IsNullOrEmpty(entry.ETag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", entry.ETag);
        }

        if (entry.LastModified.HasValue)
        {
            request.Headers.IfModifiedSince = entry.LastModified;
        }

        return CacheResult.RevalidateNeeded(entry);
    }

    public async Task PutAsync(string url, HttpResponseMessage response, byte[] content, CancellationToken cancellationToken = default)
    {
        var headers = response.Headers;
        var contentHeaders = response.Content.Headers;

        var cacheControl = headers.CacheControl;
        if (cacheControl?.NoStore == true)
        {
            return; // 저장 금지된 리소스는 캐시하지 않음
        }

        // max-age가 없으면 기본 30분 캐시 적용
        var maxAge = cacheControl?.MaxAge ?? TimeSpan.FromMinutes(30);
        var entry = new CacheEntry
        {
            Url = url,
            Data = content,
            ContentType = contentHeaders.ContentType?.ToString() ?? "text/html; charset=utf-8",
            ETag = headers.ETag?.Tag,
            LastModified = contentHeaders.LastModified,
            ExpiresAt = DateTimeOffset.UtcNow.Add(maxAge),
            NoCache = cacheControl?.NoCache ?? false,
            NoStore = cacheControl?.NoStore ?? false
        };

        _memoryCache[url] = entry;
        await SaveToDiskAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>304 응답을 받았을 때 캐시 항목의 만료 시간을 갱신한다.</summary>
    public void Touch(string url, HttpResponseMessage response)
    {
        if (!_memoryCache.TryGetValue(url, out var entry))
        {
            return;
        }

        var maxAge = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromMinutes(30);
        entry.ExpiresAt = DateTimeOffset.UtcNow.Add(maxAge);
    }

    private async Task SaveToDiskAsync(CacheEntry entry, CancellationToken cancellationToken)
    {
        if (_diskCachePath is null)
        {
            return;
        }

        try
        {
            var key = ComputeKey(entry.Url);
            var dataPath = Path.Combine(_diskCachePath, key + ".bin");
            var metaPath = Path.Combine(_diskCachePath, key + ".json");

            var meta = new CacheMetadata
            {
                Url = entry.Url,
                ContentType = entry.ContentType,
                ETag = entry.ETag,
                LastModified = entry.LastModified,
                ExpiresAt = entry.ExpiresAt,
                NoCache = entry.NoCache,
                NoStore = entry.NoStore
            };

            await File.WriteAllBytesAsync(dataPath, entry.Data, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta, JsonOptions), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 디스크 캐시 실패는 치명적이지 않으므로 무시한다.
        }
    }

    private CacheEntry? TryLoadFromDisk(string url)
    {
        if (_diskCachePath is null)
        {
            return null;
        }

        try
        {
            var key = ComputeKey(url);
            var dataPath = Path.Combine(_diskCachePath, key + ".bin");
            var metaPath = Path.Combine(_diskCachePath, key + ".json");

            if (!File.Exists(dataPath) || !File.Exists(metaPath))
            {
                return null;
            }

            var meta = JsonSerializer.Deserialize<CacheMetadata>(File.ReadAllText(metaPath), JsonOptions);
            if (meta is null || !string.Equals(meta.Url, url, StringComparison.Ordinal))
            {
                return null;
            }

            return new CacheEntry
            {
                Url = meta.Url,
                Data = File.ReadAllBytes(dataPath),
                ContentType = meta.ContentType,
                ETag = meta.ETag,
                LastModified = meta.LastModified,
                ExpiresAt = meta.ExpiresAt,
                NoCache = meta.NoCache,
                NoStore = meta.NoStore
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string ComputeKey(string url)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hash);
    }

    private sealed class CacheMetadata
    {
        public string Url { get; set; } = string.Empty;
        public string ContentType { get; set; } = "text/html; charset=utf-8";
        public string? ETag { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public bool NoStore { get; set; }
        public bool NoCache { get; set; }
    }
}

/// <summary>캐시 조회 결과.</summary>
public sealed record CacheResult(bool IsHit, CacheEntry? Entry, bool RequiresRevalidation)
{
    public static CacheResult Hit(CacheEntry entry) => new(true, entry, false);
    public static CacheResult RevalidateNeeded(CacheEntry entry) => new(false, entry, true);
    public static CacheResult Miss() => new(false, null, false);
}

/// <summary>
/// HttpClient 파이프라인에서 HTTP 캐시를 가로채어 처리하는 DelegatingHandler.
/// </summary>
public sealed class CachingHttpMessageHandler : DelegatingHandler
{
    private readonly HttpCacheManager _cacheManager;

    public CachingHttpMessageHandler(HttpMessageHandler innerHandler, HttpCacheManager cacheManager)
        : base(innerHandler)
    {
        _cacheManager = cacheManager;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // GET 요청이 아니면 캐시를 타지 않고 바로 네트워크로 전달한다.
        if (request.Method != HttpMethod.Get || request.RequestUri is null)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var url = request.RequestUri.ToString();
        var cacheResult = _cacheManager.Get(url, request);

        // 1. 캐시가 신선(Fresh)하면 네트워크 요청을 생략하고 반환한다.
        if (cacheResult.IsHit && cacheResult.Entry is not null)
        {
            return CreateCachedResponseMessage(cacheResult.Entry, request);
        }

        // 2. 네트워크 요청 수행 (신규 요청 또는 조건부 재검증 요청)
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // 3. 서버가 304 Not Modified를 반환하면 기존 캐시를 재사용한다.
        if (response.StatusCode == HttpStatusCode.NotModified && cacheResult.Entry is not null)
        {
            _cacheManager.Touch(url, response);
            response.Dispose();
            return CreateCachedResponseMessage(cacheResult.Entry, request);
        }

        // 4. 200 OK 응답은 캐시에 저장한 뒤 반환한다.
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            await _cacheManager.PutAsync(url, response, bytes, cancellationToken).ConfigureAwait(false);

            var cloned = CreateClonedResponse(response, bytes, request);
            response.Dispose();
            return cloned;
        }

        return response;
    }

    private static HttpResponseMessage CreateClonedResponse(HttpResponseMessage source, byte[] bytes, HttpRequestMessage request)
    {
        var clone = new HttpResponseMessage(source.StatusCode)
        {
            ReasonPhrase = source.ReasonPhrase,
            Version = source.Version,
            RequestMessage = request,
            Content = new ByteArrayContent(bytes)
        };

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in source.Content.Headers)
        {
            // ByteArrayContent가 Content-Length를 다시 계산하므로 중복 추가하지 않는다.
            if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private static HttpResponseMessage CreateCachedResponseMessage(CacheEntry entry, HttpRequestMessage request)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(entry.Data)
        };

        response.Content.Headers.TryAddWithoutValidation("Content-Type", entry.ContentType);

        if (!string.IsNullOrEmpty(entry.ETag))
        {
            response.Headers.TryAddWithoutValidation("ETag", entry.ETag);
        }

        if (entry.LastModified.HasValue)
        {
            response.Content.Headers.LastModified = entry.LastModified;
        }

        return response;
    }
}
