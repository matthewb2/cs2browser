using AngleSharp;
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

    public IBrowsingContext Context { get; }

    public IDocument? Document { get; private set; }

    public event EventHandler<IDocument>? DocumentLoaded;

    public DomManager(ResourceLoader resourceLoader)
    {
        _resourceLoader = resourceLoader;

        var configuration = Configuration.Default
            .WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true })
            .WithCss()
            .WithJs()
            .WithEventLoop();

        Context = BrowsingContext.New(configuration);
    }

    /// <summary>주소(URL)를 열고 파싱된 문서를 반환한다.</summary>
    public async Task<IDocument> OpenAsync(string address, CancellationToken cancellationToken = default)
    {
        var document = await Context.OpenAsync(address, cancellationToken).ConfigureAwait(true);
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
