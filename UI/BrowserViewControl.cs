using CS2Browser.Engine;
using CS2Browser.Rendering;
using DColor = Vortice.Mathematics.Color4;

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

    public event EventHandler<string>? TitleChanged;

    public string? CurrentAddress => _engine.CurrentAddress;

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
            InvalidateFromAnyThread();
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
            InvalidateFromAnyThread();
        }
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

        var layoutRoot = _engine.LayoutRoot;
        if (layoutRoot is not null)
        {
            _paintEngine.Paint(_direct2D.RenderTarget, _direct2D, layoutRoot);
        }

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
        InvalidateFromAnyThread();
    }

    private void OnTitleChanged(object? sender, EventArgs e)
        => TitleChanged?.Invoke(this, _engine.Title ?? string.Empty);

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
