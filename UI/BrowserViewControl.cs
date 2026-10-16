using CS2Browser.Engine;
using CS2Browser.Layout;
using CS2Browser.Rendering;
using DColor = Vortice.Mathematics.Color4;
using DMatrix3x2 = System.Numerics.Matrix3x2;

namespace CS2Browser.UI;

/// <summary>
/// 렌더링 결과가 그려질 커스텀 UserControl.
/// 이 컨트롤의 HWND 핸들을 Direct2D HwndRenderTarget 과 연결한다.
/// </summary>
public sealed class BrowserViewControl : UserControl
{
    private readonly BrowserEngine _engine = new();

    private Direct2DContext? _direct2D;
    private DirectWriteTextRenderer? _textRenderer;
    private PaintEngine? _paintEngine;
    private Exception? _initializationError;

    private float _scrollY;
    private bool _linkCursorActive;
    private LinkTarget? _hoveredLink;

    public event EventHandler<string>? TitleChanged;

    /// <summary>문서 높이/뷰포트가 바뀌어 스크롤 가능 범위가 달라졌을 때 발생한다.</summary>
    public event EventHandler? ScrollMetricsChanged;

    /// <summary>스크롤 오프셋이 바뀌었을 때 발생한다(휠/스크롤바 등).</summary>
    public event EventHandler? ScrollOffsetChanged;

    /// <summary>링크를 클릭했을 때 대상 절대 URL과 함께 발생한다.</summary>
    public event EventHandler<string>? LinkClicked;

    /// <summary>마우스가 링크 위로 올라가면 URL, 벗어나면 빈 문자열과 함께 발생한다.</summary>
    public event EventHandler<string>? LinkHovered;

    public string? CurrentAddress => _engine.CurrentAddress;

    /// <summary>현재 세로 스크롤 오프셋(px).</summary>
    public float ScrollOffsetY => _scrollY;

    /// <summary>문서 전체 높이(px).</summary>
    public float DocumentHeight => _engine.LayoutRoot?.Height ?? 0f;

    /// <summary>스크롤 가능한 최대 오프셋(px).</summary>
    public float MaxScroll => MathF.Max(0f, DocumentHeight - Math.Max(0, ClientSize.Height));

    public BrowserViewControl()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.Opaque
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.White;

        _engine.LayoutUpdated += OnLayoutUpdated;
        _engine.TitleChanged += OnTitleChanged;
    }

    /// <summary>주소를 로드하고 다시 그린다.</summary>
    public async Task NavigateAsync(string address)
    {
        try
        {
            await _engine.NavigateAsync(address, Math.Max(1, ClientSize.Width));
        }
        finally
        {
            _scrollY = 0f;
            _hoveredLink = null;
            InvalidateFromAnyThread();
            RaiseScrollMetricsChanged();
        }
    }

    /// <summary>네트워크 없이 HTML 문자열을 파싱해 화면에 그린다.</summary>
    public async Task LoadHtmlAsync(string html)
    {
        try
        {
            await _engine.LoadHtmlAsync(html, Math.Max(1, ClientSize.Width));
        }
        finally
        {
            _scrollY = 0f;
            _hoveredLink = null;
            InvalidateFromAnyThread();
            RaiseScrollMetricsChanged();
        }
    }

    /// <summary>세로 스크롤 오프셋을 설정한다(범위로 클램프).</summary>
    public void SetScrollOffset(float offset)
    {
        var clamped = Math.Clamp(offset, 0f, MaxScroll);
        if (MathF.Abs(clamped - _scrollY) < 0.5f)
        {
            return;
        }

        _scrollY = clamped;
        if (IsHandleCreated && !IsDisposed)
        {
            Invalidate();
        }

        ScrollOffsetChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>델타만큼 스크롤한다.</summary>
    public void ScrollBy(float delta) => SetScrollOffset(_scrollY + delta);

    /// <summary>
    /// 주어진 클라이언트 좌표(컨트롤 기준)에 있는 링크를 찾는다.
    /// 스크롤 오프셋을 반영해 문서 좌표로 변환한 뒤 레이아웃 트리를 검사한다.
    /// </summary>
    public bool TryGetLinkAt(Point clientPoint, out LinkTarget? link)
    {
        var documentY = clientPoint.Y + _scrollY;
        return HitTestLink(_engine.LayoutRoot, clientPoint.X, documentY, out link);
    }

    private static bool HitTestLink(LayoutBox? box, float x, float y, out LinkTarget? link)
    {
        link = null;
        if (box is null)
        {
            return false;
        }

        // 텍스트 조각(가장 정밀한 히트 영역)을 먼저 검사한다.
        foreach (var line in box.Lines)
        {
            foreach (var run in line.Runs)
            {
                if (run.Link is not null
                    && x >= run.X && x <= run.X + run.Width
                    && y >= line.Y && y <= line.Y + line.Height)
                {
                    link = run.Link;
                    return true;
                }
            }
        }

        // 블록 앵커/이미지처럼 박스 자체가 링크인 경우.
        if (box.Link is not null
            && x >= box.X && x <= box.X + box.Width
            && y >= box.Y && y <= box.Y + box.Height)
        {
            link = box.Link;
            return true;
        }

        // 자식(더 안쪽)이 우선하도록 재귀한다.
        foreach (var child in box.Children)
        {
            if (HitTestLink(child, x, y, out link))
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        InitializeGraphics();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DisposeGraphics();
        base.OnHandleDestroyed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_direct2D is { IsInitialized: true })
        {
            _direct2D.Resize(ClientSize.Width, ClientSize.Height);
        }

        ClampScroll();
        RaiseScrollMetricsChanged();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (MaxScroll > 0f)
        {
            ScrollBy(-e.Delta);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var overLink = TryGetLinkAt(e.Location, out var link);

        if (overLink != _linkCursorActive)
        {
            _linkCursorActive = overLink;
            Cursor = overLink ? Cursors.Hand : Cursors.Default;
        }

        if (!ReferenceEquals(link, _hoveredLink))
        {
            _hoveredLink = overLink ? link : null;
            Invalidate();
        }

        LinkHovered?.Invoke(this, overLink && link is not null ? link.Url : string.Empty);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_linkCursorActive)
        {
            _linkCursorActive = false;
            Cursor = Cursors.Default;
        }

        if (_hoveredLink is not null)
        {
            _hoveredLink = null;
            Invalidate();
        }

        LinkHovered?.Invoke(this, string.Empty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left
            && TryGetLinkAt(e.Location, out var link)
            && link is not null)
        {
            LinkClicked?.Invoke(this, link.Url);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_direct2D is { IsInitialized: true } && _paintEngine is not null)
        {
            Render();
        }
        else
        {
            RenderFallback(e.Graphics);
        }
    }

    private void InitializeGraphics()
    {
        try
        {
            _direct2D = new Direct2DContext();
            _direct2D.Initialize(Handle, Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
            _textRenderer = new DirectWriteTextRenderer(_direct2D.WriteFactory);
            _paintEngine = new PaintEngine(_textRenderer);
            // 레이아웃이 렌더링과 동일한 DirectWrite 메트릭으로 폭을 측정하도록 연결한다.
            _engine.LayoutBuilder.TextMeasurer = _textRenderer;
            _initializationError = null;
        }
        catch (Exception ex)
        {
            _initializationError = ex;
            DisposeGraphics();
        }
    }

    private void Render()
    {
        if (_direct2D is null || _paintEngine is null)
        {
            return;
        }

        _direct2D.Resize(ClientSize.Width, ClientSize.Height);
        _direct2D.BeginDraw();
        _direct2D.Clear(new DColor(1f, 1f, 1f, 1f));
        _direct2D.SetTransform(DMatrix3x2.CreateTranslation(0f, -_scrollY));

        var layoutRoot = _engine.LayoutRoot;
        if (layoutRoot is not null)
        {
            _paintEngine.Paint(_direct2D.RenderTarget, _direct2D, layoutRoot);

            if (_hoveredLink is not null)
            {
                _paintEngine.PaintLinkUnderlines(_direct2D.RenderTarget, _direct2D, layoutRoot, _hoveredLink);
            }
        }

        _direct2D.ResetTransform();
        _direct2D.EndDraw();
    }

    private void RenderFallback(Graphics graphics)
    {
        graphics.Clear(Color.White);
        using var brush = new SolidBrush(Color.DimGray);
        using var font = new Font("Malgun Gothic", 10f);
        var message = _initializationError is null
            ? "페이지를 불러오는 중입니다..."
            : "Direct2D 초기화 실패: " + _initializationError.Message;
        graphics.DrawString(message, font, brush, new PointF(12f, 12f));
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        ClampScroll();
        InvalidateFromAnyThread();
        RaiseScrollMetricsChanged();
    }

    private void OnTitleChanged(object? sender, EventArgs e)
        => TitleChanged?.Invoke(this, _engine.Title ?? string.Empty);

    private void ClampScroll()
    {
        var max = MaxScroll;
        if (_scrollY > max)
        {
            _scrollY = max;
        }

        if (_scrollY < 0f)
        {
            _scrollY = 0f;
        }
    }

    private void RaiseScrollMetricsChanged()
    {
        if (IsHandleCreated && !IsDisposed)
        {
            ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>호출 스레드와 무관하게 UI 스레드에서 다시 그리도록 요청한다.</summary>
    private void InvalidateFromAnyThread()
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(Invalidate));
            }
            catch (ObjectDisposedException)
            {
                // 핸들이 이미 파괴됨
            }
        }
        else
        {
            Invalidate();
        }
    }

    private void DisposeGraphics()
    {
        if (ReferenceEquals(_engine.LayoutBuilder.TextMeasurer, _textRenderer))
        {
            _engine.LayoutBuilder.TextMeasurer = null;
        }

        _paintEngine?.Dispose();
        _paintEngine = null;
        _textRenderer?.Dispose();
        _textRenderer = null;
        _direct2D?.Dispose();
        _direct2D = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeGraphics();
            _engine.Dispose();
        }

        base.Dispose(disposing);
    }
}
