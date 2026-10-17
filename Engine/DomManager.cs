using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Dom;
using AngleSharp.Io;
using CS2Browser.Network;

namespace CS2Browser.Engine;

/// <summary>
/// AngleSharp BrowsingContext 와 문서 초기화를 관리한다.
/// WithDefaultLoader / WithCss / WithJs 설정을 담당한다.
/// </summary>
public sealed class DomManager
{
    private readonly ResourceLoader _resourceLoader;
    private readonly DefaultRenderDevice _renderDevice = new()
    {
        ViewPortWidth = 1024,
        ViewPortHeight = 768,
        FontSize = 16
    };

    public IBrowsingContext Context { get; }

    public IDocument? Document { get; private set; }

    public event EventHandler<IDocument>? DocumentLoaded;

    public DomManager(ResourceLoader resourceLoader)
    {
        _resourceLoader = resourceLoader;

        var configuration = Configuration.Default
            .WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true })
            .With(new FileRequester())
            .WithRenderDevice(_renderDevice)
            .WithCss()
            .WithJs()
            .WithEventLoop();

        Context = BrowsingContext.New(configuration);
    }

    /// <summary>em/rem/vh 등 상대 단위 계산에 쓰이는 뷰포트 크기를 갱신한다.</summary>
    public void SetViewport(int width, int height)
    {
        _renderDevice.ViewPortWidth = Math.Max(1, width);
        _renderDevice.ViewPortHeight = Math.Max(1, height);
    }

    /// <summary>주소(URL)를 열고 파싱된 문서를 반환한다.</summary>
    public async Task<IDocument> OpenAsync(string address, CancellationToken cancellationToken = default)
    {
        // http(s) 문서는 캐시/로깅이 적용되는 ResourceLoader 로 직접 받아온 뒤 파싱한다.
        // (AngleSharp 기본 로더는 자체 HttpClient 를 사용하므로 우리 파이프라인을 거치지 않는다.)
        IDocument document;
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var html = await _resourceLoader.GetTextAsync(address, cancellationToken).ConfigureAwait(true);
            document = await Context
                .OpenAsync(request => request.Address(address).Content(html), cancellationToken)
                .ConfigureAwait(true);
        }
        else
        {
            document = await Context.OpenAsync(address, cancellationToken).ConfigureAwait(true);
        }

        SetDocument(document);
        return document;
    }

    /// <summary>HTML 문자열을 직접 열어 파싱한다.</summary>
    public async Task<IDocument> OpenHtmlAsync(string html, CancellationToken cancellationToken = default)
    {
        var document = await Context.OpenAsync(request => request.Content(html), cancellationToken).ConfigureAwait(true);
        SetDocument(document);
        return document;
    }

    private void SetDocument(IDocument document)
    {
        Document = document;
        DocumentLoaded?.Invoke(this, document);
    }
}
