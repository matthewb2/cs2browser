using System.Net;
using System.Net.Sockets;
using System.Text;
using CS2Browser.Network;

namespace CS2Browser.Tests;

[TestClass]
public sealed class ResourceLoaderTests
{
    [TestMethod]
    public async Task GetTextAsync_ReturnsBody()
    {
        using var server = new SingleResponseServer("hello-resource");
        using var loader = new ResourceLoader();

        var text = await loader.GetTextAsync(server.Url);

        Assert.AreEqual("hello-resource", text);
    }

    [TestMethod]
    public async Task GetBytesAsync_ReturnsBodyBytes()
    {
        using var server = new SingleResponseServer("bytes-body");
        using var loader = new ResourceLoader();

        var bytes = await loader.GetBytesAsync(server.Url);

        Assert.AreEqual("bytes-body", Encoding.UTF8.GetString(bytes));
    }

    [TestMethod]
    public async Task EnsureSuccessStatusCode_ThrowsOnServerError()
    {
        using var server = new SingleResponseServer("boom", statusCode: 500);
        using var loader = new ResourceLoader();

        await Assert.ThrowsExceptionAsync<HttpRequestException>(
            () => loader.GetTextAsync(server.Url));
    }
}

/// <summary>TcpListener 기반의 최소 HTTP 서버로, 요청 한 건에 한 번만 응답한다.</summary>
internal sealed class SingleResponseServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Task _serveTask;

    public string Url { get; }

    public SingleResponseServer(string body, int statusCode = 200)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Url = $"http://127.0.0.1:{port}/";

        _serveTask = Task.Run(async () =>
        {
            using var client = await _listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();

            var buffer = new byte[8192];
            _ = await stream.ReadAsync(buffer, 0, buffer.Length);

            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var reason = statusCode == 200 ? "OK" : "Error";
            var header =
                $"HTTP/1.1 {statusCode} {reason}\r\n" +
                "Content-Type: text/plain; charset=utf-8\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n\r\n";

            await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
            await stream.WriteAsync(bodyBytes);
            await stream.FlushAsync();
        });
    }

    public void Dispose()
    {
        _listener.Stop();
        try
        {
            _serveTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 서버 태스크 정리 실패는 무시한다.
        }
    }
}
