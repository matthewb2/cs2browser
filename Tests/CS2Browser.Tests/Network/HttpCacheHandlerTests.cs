using System.Net;
using System.Net.Sockets;
using System.Text;
using CS2Browser.Network;

namespace CS2Browser.Tests;

[TestClass]
public sealed class HttpCacheHandlerTests
{
    [TestMethod]
    public async Task FreshEntry_SecondRequest_IsServedFromCache()
    {
        using var server = new TestHttpServer(_ => TestResponse.Ok("cached-body", maxAgeSeconds: 300));
        using var loader = new ResourceLoader(new HttpCacheManager());

        var first = await loader.GetTextAsync(server.Url);
        var second = await loader.GetTextAsync(server.Url);

        Assert.AreEqual("cached-body", first);
        Assert.AreEqual("cached-body", second);
        Assert.AreEqual(1, server.RequestCount, "신선한 캐시는 두 번째 요청을 네트워크로 보내지 않아야 한다.");
    }

    [TestMethod]
    public async Task StaleEntry_WithETag_RevalidatesAndReusesCacheOn304()
    {
        using var server = new TestHttpServer(request =>
            request.Headers.TryGetValue("If-None-Match", out var inm) && inm == "\"v1\""
                ? TestResponse.NotModified("\"v1\"")
                : TestResponse.Ok("etag-body", maxAgeSeconds: 0, etag: "\"v1\""));
        using var loader = new ResourceLoader(new HttpCacheManager());

        var first = await loader.GetTextAsync(server.Url);
        var second = await loader.GetTextAsync(server.Url);

        Assert.AreEqual("etag-body", first);
        Assert.AreEqual("etag-body", second);
        Assert.AreEqual(2, server.RequestCount, "만료된 캐시는 조건부 재검증 요청을 보내야 한다.");
        Assert.AreEqual("\"v1\"", server.LastRequest.Headers["If-None-Match"]);
    }

    [TestMethod]
    public async Task NoStoreResponse_IsNotCached()
    {
        using var server = new TestHttpServer(_ => TestResponse.Ok("no-store-body", cacheControl: "no-store"));
        using var loader = new ResourceLoader(new HttpCacheManager());

        await loader.GetTextAsync(server.Url);
        await loader.GetTextAsync(server.Url);

        Assert.AreEqual(2, server.RequestCount, "no-store 응답은 캐시에 저장되면 안 된다.");
    }
}

internal sealed class TestRequest
{
    public required string Method { get; init; }
    public required string Path { get; init; }
    public required Dictionary<string, string> Headers { get; init; }
}

internal sealed class TestResponse
{
    public int StatusCode { get; init; } = 200;
    public string ReasonPhrase { get; init; } = "OK";
    public string Body { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; } = new();

    public static TestResponse Ok(string body, int? maxAgeSeconds = null, string? etag = null, string? cacheControl = null)
    {
        var response = new TestResponse { StatusCode = 200, ReasonPhrase = "OK", Body = body };
        if (cacheControl is not null)
        {
            response.Headers["Cache-Control"] = cacheControl;
        }
        else if (maxAgeSeconds.HasValue)
        {
            response.Headers["Cache-Control"] = $"max-age={maxAgeSeconds.Value}";
        }

        if (etag is not null)
        {
            response.Headers["ETag"] = etag;
        }

        return response;
    }

    public static TestResponse NotModified(string? etag = null)
    {
        var response = new TestResponse { StatusCode = 304, ReasonPhrase = "Not Modified", Body = string.Empty };
        if (etag is not null)
        {
            response.Headers["ETag"] = etag;
        }

        return response;
    }
}

/// <summary>여러 요청을 순차적으로 처리하는 최소 HTTP 서버로, 요청 횟수를 기록한다.</summary>
internal sealed class TestHttpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly Func<TestRequest, TestResponse> _handler;
    private int _requestCount;
    private TestRequest _lastRequest = new() { Method = string.Empty, Path = string.Empty, Headers = new() };

    public string Url { get; }
    public int RequestCount => Volatile.Read(ref _requestCount);
    public TestRequest LastRequest => _lastRequest;

    public TestHttpServer(Func<TestRequest, TestResponse> handler)
    {
        _handler = handler;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Url = $"http://127.0.0.1:{port}/";
        _loop = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                await HandleClientAsync(client).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 종료 시 정상적으로 빠져나온다.
        }
        catch (SocketException)
        {
            // 리스너 종료 시 발생할 수 있다.
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var stream = client.GetStream();
        var request = await ReadRequestAsync(stream).ConfigureAwait(false);
        Interlocked.Increment(ref _requestCount);
        _lastRequest = request;

        var response = _handler(request);
        var bodyBytes = Encoding.UTF8.GetBytes(response.Body);

        var builder = new StringBuilder();
        builder.Append($"HTTP/1.1 {response.StatusCode} {response.ReasonPhrase}\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        foreach (var header in response.Headers)
        {
            builder.Append($"{header.Key}: {header.Value}\r\n");
        }
        builder.Append($"Content-Length: {bodyBytes.Length}\r\n");
        builder.Append("Connection: close\r\n\r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes(builder.ToString())).ConfigureAwait(false);
        if (bodyBytes.Length > 0)
        {
            await stream.WriteAsync(bodyBytes).ConfigureAwait(false);
        }
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static async Task<TestRequest> ReadRequestAsync(NetworkStream stream)
    {
        var buffer = new byte[1024];
        var received = new List<byte>();

        while (true)
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            received.AddRange(buffer.Take(read));
            var text = Encoding.ASCII.GetString(received.ToArray());
            if (text.Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                break;
            }
        }

        var headerText = Encoding.ASCII.GetString(received.ToArray());
        var lines = headerText.Split("\r\n");
        var requestLine = lines[0].Split(' ');
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var separator = lines[i].IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = lines[i][..separator].Trim();
            var value = lines[i][(separator + 1)..].Trim();
            headers[name] = value;
        }

        return new TestRequest
        {
            Method = requestLine.ElementAtOrDefault(0) ?? string.Empty,
            Path = requestLine.ElementAtOrDefault(1) ?? string.Empty,
            Headers = headers
        };
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 정리 실패는 무시한다.
        }

        _cts.Dispose();
    }
}
