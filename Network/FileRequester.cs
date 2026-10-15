using System.Net;
using AngleSharp.Dom;
using AngleSharp.Dom.Events;
using AngleSharp.Io;

namespace CS2Browser.Network;

/// <summary>
/// file:// 스킴을 읽어 로컬 파일(HTML/CSS/JS/이미지 등)을 AngleSharp 리소스로 제공하는 요청자.
/// 기본 AngleSharp 로더는 http/https/data 만 지원하므로, 로컬 파일 페이지와
/// 그 상대 경로 리소스(예: style.css)를 불러오기 위해 추가한다.
/// </summary>
public sealed class FileRequester : IRequester
{
#pragma warning disable CS0067 // 인터페이스 구현용 이벤트(현재 사용되지 않음)
    public event DomEventHandler? Requesting;
    public event DomEventHandler? Requested;
#pragma warning restore CS0067

    public bool SupportsProtocol(string protocol)
        => string.Equals(protocol, "file", StringComparison.OrdinalIgnoreCase);

    public Task<IResponse?> RequestAsync(Request request, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();

        var address = request.Address ?? new Url("file:///");

        if (!TryGetLocalPath(address, out var path))
        {
            return Task.FromResult<IResponse?>(
                new FileResponse(address, Array.Empty<byte>(), "application/octet-stream", HttpStatusCode.BadRequest));
        }

        if (!File.Exists(path))
        {
            return Task.FromResult<IResponse?>(
                new FileResponse(address, Array.Empty<byte>(), "application/octet-stream", HttpStatusCode.NotFound));
        }

        var bytes = File.ReadAllBytes(path);
        var response = new FileResponse(address, bytes, GetMimeType(path), HttpStatusCode.OK);
        return Task.FromResult<IResponse?>(response);
    }

    private static bool TryGetLocalPath(Url address, out string path)
    {
        try
        {
            path = new Uri(address.Href).LocalPath;
            return path.Length > 0;
        }
        catch (UriFormatException)
        {
            path = string.Empty;
            return false;
        }
    }

    private static string GetMimeType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" or ".mjs" => "application/javascript",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };

    public void AddEventListener(string type, DomEventHandler? handler = null, bool capture = false)
    {
    }

    public void RemoveEventListener(string type, DomEventHandler? handler = null, bool capture = false)
    {
    }

    public void InvokeEventListener(Event ev)
    {
    }

    public bool Dispatch(Event ev) => true;
}

/// <summary>메모리에 담긴 파일 응답.</summary>
internal sealed class FileResponse : IResponse
{
    private readonly MemoryStream _content;

    public FileResponse(Url address, byte[] data, string mimeType, HttpStatusCode statusCode)
    {
        Address = address;
        StatusCode = statusCode;
        _content = new MemoryStream(data, writable: false);
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = mimeType
        };
    }

    public HttpStatusCode StatusCode { get; }

    public Url Address { get; }

    public IDictionary<string, string> Headers { get; }

    public Stream Content => _content;

    public void Dispose() => _content.Dispose();
}
