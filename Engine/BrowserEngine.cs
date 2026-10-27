using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using CS2Browser.Layout;
using CS2Browser.Network;

namespace CS2Browser.Engine;

/// <summary>
/// 로드 → 파싱 → (스크립트) → 레이아웃 → 페인트 파이프라인을 총괄하는 오케스트레이터.
/// </summary>
public sealed class BrowserEngine : IDisposable
{
    private const int MaxMetaRedirects = 5;
    private readonly SynchronizationContext? _syncContext;
    private float _viewportWidth = 1f;
    private IDocument? _layoutDocument;
    private bool _reformatPending;
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
        ResourceLoader = new ResourceLoader(HttpCacheManager.CreateDefault());
        DomManager = new DomManager(ResourceLoader);
        ScriptEngine = new ScriptEngine();
        ScriptEngine.Attach(DomManager);
        LayoutBuilder = new LayoutTreeBuilder();

        DomManager.DocumentLoaded += OnDocumentLoaded;
        DomManager.DomCommitted += (_, _) => ScheduleReformat();
        _syncContext = SynchronizationContext.Current;
    }

    /// <summary>주소를 로드하고 레이아웃까지 계산한다.</summary>
    public async Task NavigateAsync(string address, float viewportWidth, CancellationToken cancellationToken = default)
    {
        var current = address;

        // 브라우저처럼 <meta http-equiv="refresh"> 리다이렉트를 따라간다.
        for (var redirect = 0; redirect <= MaxMetaRedirects; redirect++)
        {
            CurrentAddress = current;
            var document = await DomManager.OpenAsync(current, cancellationToken).ConfigureAwait(true);

            var next = GetMetaRefreshTarget(document, current);
            if (next is null || string.Equals(next, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = next;
        }

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
        _viewportWidth = Math.Max(1f, viewportWidth);
        _reformatPending = false;
        var document = DomManager.Document;
        _layoutDocument = document;
        if (document is not null)
        {
            DomManager.SetViewport((int)MathF.Max(1f, viewportWidth), 768);
        }

        LayoutRoot = document is null
            ? null
            : LayoutBuilder.Build(document, Math.Max(1f, viewportWidth));

        LayoutUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>뷰포트 폭이 바뀌면 레이아웃을 다시 계산하도록 예약한다.</summary>
    public void SetViewportWidth(float viewportWidth)
    {
        viewportWidth = Math.Max(1f, viewportWidth);
        if (MathF.Abs(viewportWidth - _viewportWidth) < 0.5f)
        {
            return;
        }

        _viewportWidth = viewportWidth;
        ScheduleReformat();
    }

    /// <summary>
    /// NetSurf 의 content reformat 처럼 레이아웃 재계산을 UI 스레드에 예약한다.
    /// 여러 번 커밋되어도 다음 턴에 한 번만 재계산한다. 동기화 컨텍스트가 없으면 즉시 실행한다.
    /// </summary>
    public void ScheduleReformat()
    {
        if (_reformatPending)
        {
            return;
        }

        _reformatPending = true;
        if (_syncContext is null)
        {
            Reformat();
        }
        else
        {
            _syncContext.Post(_ => Reformat(), null);
        }
    }

    private void Reformat()
    {
        if (!_reformatPending)
        {
            return;
        }

        // 로드 중인 문서는 Navigate/LoadHtml 이 끝날 때 레이아웃된다.
        if (DomManager.Document is null || !ReferenceEquals(DomManager.Document, _layoutDocument))
        {
            _reformatPending = false;
            return;
        }

        BuildLayout(_viewportWidth);
    }

    private void OnDocumentLoaded(object? sender, IDocument document)
    {
        Title = document.Title;
        TitleChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 문서에 &lt;meta http-equiv="refresh"&gt; 가 있으면 리다이렉트 대상 절대 URL 을 반환한다.
    /// 없으면 null. 상대 경로는 문서 URL 기준으로 해석한다.
    /// </summary>
    private static string? GetMetaRefreshTarget(IDocument document, string baseAddress)
    {
        foreach (var meta in document.QuerySelectorAll("meta[http-equiv]"))
        {
            var target = ResolveRefreshTarget(
                meta.GetAttribute("http-equiv"),
                meta.GetAttribute("content"),
                document,
                baseAddress);
            if (target is not null)
            {
                return target;
            }
        }

        // 스크립팅이 켜져 있으면 <noscript> 내용은 요소로 파싱되지 않고 텍스트로 남는다.
        // 자바스크립트 없는 브라우저를 위한 대체 리다이렉트를 직접 찾아본다.
        foreach (var noscript in document.QuerySelectorAll("noscript"))
        {
            var text = noscript.TextContent;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            foreach (Match metaTag in MetaRefreshTagRegex.Matches(text))
            {
                var content = ContentAttributeRegex.Match(metaTag.Value);

                // noscript 원시 텍스트는 엔티티가 디코딩되지 않은 채 유지되므로
                // URL 앞뒤의 &quot; 등 엔티티를 풀어준다.
                var rawContent = content.Success
                    ? WebUtility.HtmlDecode(content.Groups["value"].Value)
                    : null;

                var target = ResolveRefreshTarget(
                    "refresh",
                    rawContent,
                    document,
                    baseAddress);
                if (target is not null)
                {
                    return target;
                }
            }
        }

        return null;
    }

    /// <summary>http-equiv/content 속성에서 리다이렉트 대상 절대 URL 을 계산한다.</summary>
    private static string? ResolveRefreshTarget(string? httpEquiv, string? content, IDocument document, string baseAddress)
    {
        if (!string.Equals(httpEquiv, "refresh", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var index = content.IndexOf("url=", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var target = content[(index + 4)..].Trim().Trim('\'', '"');
        if (target.Length == 0)
        {
            return null;
        }

        var baseUrl = string.IsNullOrEmpty(document.Url) ? baseAddress : document.Url;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || !Uri.TryCreate(baseUri, target, out var resolved))
        {
            return null;
        }

        return resolved.AbsoluteUri;
    }

    private static readonly Regex MetaRefreshTagRegex = new(
        @"<meta\b[^>]*\bhttp-equiv\s*=\s*(?:""refresh""|'refresh'|refresh\b)[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ContentAttributeRegex = new(
        @"\bcontent\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public void Dispose() => ResourceLoader.Dispose();
}
