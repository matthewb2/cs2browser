using AngleSharp.Dom;
using CS2Browser.Layout;
using CS2Browser.Network;

namespace CS2Browser.Engine;

/// <summary>
/// 로드 → 파싱 → (스크립트) → 레이아웃 → 페인트 파이프라인을 총괄하는 오케스트레이터.
/// </summary>
public sealed class BrowserEngine : IDisposable
{
    public ResourceLoader ResourceLoader { get; }
    public DomManager DomManager { get; }
    public ScriptEngine ScriptEngine { get; }
    public LayoutTreeBuilder LayoutBuilder { get; }

    public LayoutBox? LayoutRoot { get; private set; }
    public string? CurrentAddress { get; private set; }
    public string? Title { get; private set; }

    public event EventHandler? LayoutUpdated;
    public event EventHandler? TitleChanged;

    public BrowserEngine()
    {
        ResourceLoader = new ResourceLoader();
        DomManager = new DomManager(ResourceLoader);
        ScriptEngine = new ScriptEngine();
        ScriptEngine.Attach(DomManager);
        LayoutBuilder = new LayoutTreeBuilder();

        DomManager.DocumentLoaded += OnDocumentLoaded;
    }

    /// <summary>주소를 로드하고 레이아웃까지 계산한다.</summary>
    public async Task NavigateAsync(string address, float viewportWidth, CancellationToken cancellationToken = default)
    {
        CurrentAddress = address;
        await DomManager.OpenAsync(address, cancellationToken).ConfigureAwait(true);
        BuildLayout(viewportWidth);
    }

    /// <summary>네트워크 없이 HTML 문자열을 직접 파싱하고 레이아웃까지 계산한다.</summary>
    public async Task LoadHtmlAsync(string html, float viewportWidth, CancellationToken cancellationToken = default)
    {
        CurrentAddress = "about:local";
        await DomManager.OpenHtmlAsync(html, cancellationToken).ConfigureAwait(true);
        BuildLayout(viewportWidth);
    }

    /// <summary>현재 문서를 기준으로 레이아웃 트리를 다시 만든다.</summary>
    public void BuildLayout(float viewportWidth)
    {
        var document = DomManager.Document;
        if (document is not null)
        {
            DomManager.SetViewport((int)MathF.Max(1f, viewportWidth), 768);
        }

        LayoutRoot = document is null
            ? null
            : LayoutBuilder.Build(document, Math.Max(1f, viewportWidth));

        LayoutUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void OnDocumentLoaded(object? sender, IDocument document)
    {
        Title = document.Title;
        TitleChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => ResourceLoader.Dispose();
}
