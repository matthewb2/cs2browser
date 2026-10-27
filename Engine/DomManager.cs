using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Dom;
using AngleSharp.Io;
using CS2Browser.Network;

namespace CS2Browser.Engine;

/// <summary>
/// AngleSharp BrowsingContext 와 문서 초기화를 관리한다.
/// WithDefaultLoader / WithCss / V8ScriptingService 설정을 담당한다.
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
            .With(new V8ScriptingService())
            .WithEventLoop();

        Context = BrowsingContext.New(configuration);
    }

    /// <summary>em/rem/vh 등 상대 단위 계산에 쓰이는 뷰포트 크기를