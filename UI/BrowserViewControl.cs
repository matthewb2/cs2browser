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
    private Exception? _renderError;

    private float _scrollY;
    private bool _linkCursorActive;
    private bool _textCursorActive;
    private bool _inputCursorActive;
    private LinkTarget? _hoveredLink;

    private LayoutBox? _focusedInput;
    private System.Windows.Forms.Timer? _caretTimer;

    private TextSelection? _selection;
    private bool _selectionPending;
    private bool _selectionDragging;
    private int _selectionAnchorOffset;
    private Point _selectionStartPoint;

    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _copyMenuItem;

    private const int DragThreshold = 3;

    public event EventHandler<string>? TitleChanged;

    /// <summary>문서 높이/뷰포트가 바뀌어 스크롤 가능 범위가 달라졌을 때 발생한다.</summary>
    public event EventHandler? ScrollMetricsChanged;

    /// <summary>스크롤 오프셋이 바뀌었을 때 발생한다(휠/스크롤바 등).</summary>
    public event EventHandler? ScrollOffsetChanged;

    /// <summary>링크를 클릭했을 때 대상 절대 URL과 함께 발생한다.</summary>
    public event EventHandler<string>? LinkClicked;

    /// <summary>마우스가 링크 위로 올라가면 URL, 벗어나면 빈 문자열과 함께 발생한다.</summary>
    public event EventHandler<string>? LinkHovered;

    /// <summary>선택 범위가 바뀌었을 때 발생한다(빈 선택 포함).</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>현재 선택된 텍스트(선택이 없으면 빈 문자열).</summary>
    public string SelectedText => TextSelectionReader.GetSelectedText(_engine.LayoutRoot, _selection);

    /// <summary>선택 여부.</summary>
    public bool HasSelection => _selection is { IsEmpty: false };

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
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);
        TabStop = true;

        BackColor = Color.White;

        _copyMenuItem = new ToolStripMenuItem("복사", null, (_, _) => CopySelectionToClipboard())
        {
            Enabled = false
        };
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add(_copyMenuItem);
        _contextMenu.Opening += (_, _) => _copyMenuItem.Enabled = HasSelection;
        ContextMenuStrip = _contextMenu;

        _engine.LayoutUpdated += OnLayoutUpdated;
        _engine.TitleChanged += OnTitleChanged;

        // IME(한글 조합)를 받도록 한다.
        ImeMode = ImeMode.Alpha;

        // 캐럿 깜빡임 타이머. 시스템 표준에 가까운 주기(약 530ms)로 토글한다.
        _caretTimer = new System.Windows.Forms.Timer { Interval = 530 };
        _caretTimer.Tick += (_, _) => ToggleCaret();
    }

    protected override bool CanEnableIme => true;

    /// <summary>주소를 로드하고 다시 그린다.</summary>
    public async Task NavigateAsync(string address)
    {
        ClearSelection();
        ClearInputFocus();
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
        ClearSelection();
        ClearInputFocus();
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

    /// <summary>
    /// 주어진 클라이언트 좌표에 대응하는 텍스트 버퍼 오프셋을 찾는다.
    /// 스크롤 오프셋을 반영해 문서 좌표로 변환한 뒤 히트 테스트한다.
    /// </summary>
    public bool TryGetTextOffsetAt(Point clientPoint, out int offset)
    {
        var documentY = clientPoint.Y + _scrollY;
        return TextSelectionReader.TryHitTest(_engine.LayoutRoot, clientPoint.X, documentY, _textRenderer, out offset);
    }

    /// <summary>
    /// 주어진 클라이언트 좌표에 있는 텍스트 입력 상자(&lt;input&gt;/&lt;textarea&gt;)를 찾는다.
    /// 스크롤 오프셋을 반영해 문서 좌표로 변환한 뒤 히트 테스트한다.
    /// </summary>
    public bool TryGetInputAt(Point clientPoint, out LayoutBox? input)
    {
        var documentY = clientPoint.Y + _scrollY;
        return TextInputReader.TryHitTest(_engine.LayoutRoot, clientPoint.X, documentY, out input);
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

        _engine.SetViewportWidth(Math.Max(1, ClientSize.Width));
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // 입력 상자를 클릭하면 텍스트 선택 대신 캐럿을 옮긴다.
        if (TryGetInputAt(e.Location, out var input) && input is not null)
        {
            _selectionDragging = false;
            _selectionPending = false;
            ClearSelection();
            FocusInput(input, TextInputReader.CaretIndexFromX(input, e.Location.X, _textRenderer));
            return;
        }

        ClearInputFocus();

        _selectionStartPoint = e.Location;
        _selectionDragging = false;

        // 새 지점을 누르면 기존 선택을 해제하고 앵커 후보를 기록한다.
        ClearSelection();
        _selectionPending = TryGetTextOffsetAt(e.Location, out _selectionAnchorOffset);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // 왼쪽 버튼을 누른 채 일정 거리 이상 움직이면 텍스트 선택을 시작/갱신한다.
        if ((e.Button & MouseButtons.Left) != 0 && _selectionPending)
        {
            if (!_selectionDragging
                && Math.Abs(e.X - _selectionStartPoint.X) + Math.Abs(e.Y - _selectionStartPoint.Y) > DragThreshold)
            {
                _selectionDragging = true;
                _selection = new TextSelection(_selectionAnchorOffset, _selectionAnchorOffset);
            }

            if (_selectionDragging && TryGetTextOffsetAt(e.Location, out var focus))
            {
                _selection!.Focus = focus;
                SuppressLinkHover();
                Cursor = Cursors.IBeam;
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        var overLink = TryGetLinkAt(e.Location, out var link);
        var overText = overLink || TryGetTextOffsetAt(e.Location, out _);
        var overInput = TryGetInputAt(e.Location, out _);

        if (overLink != _linkCursorActive || overText != _textCursorActive || overInput != _inputCursorActive)
        {
            _linkCursorActive = overLink;
            _textCursorActive = overText;
            _inputCursorActive = overInput;
            Cursor = overLink ? Cursors.Hand : overInput || overText ? Cursors.IBeam : Cursors.Default;
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

        if (_linkCursorActive || _textCursorActive || _inputCursorActive)
        {
            _linkCursorActive = false;
            _textCursorActive = false;
            _inputCursorActive = false;
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

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_selectionDragging)
        {
            _selectionDragging = false;
            _selectionPending = false;

            if (_selection is { IsEmpty: true })
            {
                _selection = null;
            }

            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _selectionPending = false;

        // 드래그가 아니면 일반 클릭으로 보고 링크 이동을 처리한다.
        if (TryGetLinkAt(e.Location, out var link) && link is not null)
        {
            LinkClicked?.Invoke(this, link.Url);
        }
    }

    /// <summary>선택을 지운다(선택이 있었다면 SelectionChanged 발생).</summary>
    public void ClearSelection()
    {
        if (_selection is null)
        {
            return;
        }

        _selection = null;
        if (IsHandleCreated && !IsDisposed)
        {
            Invalidate();
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>선택된 텍스트를 클립보드에 복사한다. 복사했으면 true.</summary>
    public bool CopySelectionToClipboard()
    {
        var text = SelectedText;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (Exception)
        {
            // 다른 프로세스가 클립보드를 점유 중인 경우 등.
            return false;
        }
    }

    private void SuppressLinkHover()
    {
        if (_hoveredLink is null)
        {
            return;
        }

        _hoveredLink = null;
        LinkHovered?.Invoke(this, string.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_focusedInput is not null && HandleInputKeyDown(e))
        {
            return;
        }

        if (e.Control && e.KeyCode == Keys.C && CopySelectionToClipboard())
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);

        // 입력 상자에 포커스가 있고, IME 조합 중이 아닐 때만 직접 삽입한다.
        if (_focusedInput is null || !string.IsNullOrEmpty(_focusedInput.CompositionText))
        {
            return;
        }

        if (char.IsControl(e.KeyChar))
        {
            return;
        }

        var text = _focusedInput.InputText ?? string.Empty;
        var index = Math.Clamp(_focusedInput.CaretIndex, 0, text.Length);
        _focusedInput.InputText = text.Insert(index, e.KeyChar.ToString());
        _focusedInput.CaretIndex = index + 1;
        e.Handled = true;
        StartCaretBlink();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        ClearInputFocus();
    }

    /// <summary>포커스된 입력 상자의 캐럿 이동/삭제 키를 처리한다. 처리했으면 true.</summary>
    private bool HandleInputKeyDown(KeyEventArgs e)
    {
        var box = _focusedInput!;
        var text = box.InputText ?? string.Empty;
        var composing = !string.IsNullOrEmpty(box.CompositionText);

        switch (e.KeyCode)
        {
            case Keys.Left:
                if (!composing && box.CaretIndex > 0)
                {
                    box.CaretIndex--;
                }

                break;

            case Keys.Right:
                if (!composing && box.CaretIndex < text.Length)
                {
                    box.CaretIndex++;
                }

                break;

            case Keys.Home:
                if (!composing)
                {
                    box.CaretIndex = 0;
                }

                break;

            case Keys.End:
                if (!composing)
                {
                    box.CaretIndex = text.Length;
                }

                break;

            case Keys.Back:
                if (!composing && box.CaretIndex > 0)
                {
                    var index = box.CaretIndex;
                    box.InputText = text.Remove(index - 1, 1);
                    box.CaretIndex = index - 1;
                }

                break;

            case Keys.Delete:
                if (!composing && box.CaretIndex < text.Length)
                {
                    box.InputText = text.Remove(box.CaretIndex, 1);
                }

                break;

            default:
                return false;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        StartCaretBlink();
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (_focusedInput is not null
            && (m.Msg == InputMethod.WmImeStartComposition || m.Msg == InputMethod.WmImeComposition))
        {
            HandleImeMessage(ref m);
            return;
        }

        base.WndProc(ref m);
    }

    /// <summary>IME 조합 시작/갱신 메시지를 처리해 입력 상자에 반영한다.</summary>
    private void HandleImeMessage(ref Message m)
    {
        var box = _focusedInput!;

        if (m.Msg == InputMethod.WmImeStartComposition)
        {
            PositionCompositionWindow(box);
            base.WndProc(ref m);
            return;
        }

        var flags = m.LParam.ToInt32();
        if ((flags & InputMethod.GcsResultStr) != 0)
        {
            InputMethod.CommitResult(Handle, box);
        }
        else if ((flags & InputMethod.GcsCompStr) != 0)
        {
            InputMethod.UpdateComposition(Handle, box);
        }

        StartCaretBlink();
    }

    private void PositionCompositionWindow(LayoutBox box)
    {
        var fontSize = box.FontSize > 0f ? box.FontSize : 14f;
        var lineHeight = fontSize * 1.4f;
        var caretX = TextInputReader.CaretX(box, _textRenderer);
        var caretY = box.Y + MathF.Max(0f, (box.Height - lineHeight) * 0.5f);
        InputMethod.SetCompositionWindow(
            Handle,
            (int)MathF.Round(caretX),
            (int)MathF.Round(caretY - _scrollY));
    }

    /// <summary>입력 상자에 포커스하고 캐럿을 지정 위치로 옮긴다.</summary>
    private void FocusInput(LayoutBox box, int caretIndex)
    {
        if (!ReferenceEquals(_focusedInput, box))
        {
            if (_focusedInput is not null)
            {
                _focusedInput.IsFocused = false;
                _focusedInput.CompositionText = string.Empty;
            }

            _focusedInput = box;
            box.IsFocused = true;
        }

        box.CaretIndex = Math.Clamp(caretIndex, 0, (box.InputText ?? string.Empty).Length);
        StartCaretBlink();
    }

    /// <summary>입력 상자 포커스를 해제하고 캐럿을 숨긴다.</summary>
    private void ClearInputFocus()
    {
        if (_focusedInput is null)
        {
            return;
        }

        _focusedInput.IsFocused = false;
        _focusedInput.CompositionText = string.Empty;
        _focusedInput.IsCaretVisible = false;
        _focusedInput = null;
        _caretTimer?.Stop();
        Invalidate();
    }

    /// <summary>캐럿을 보이게 하고 깜빡임 타이머를 다시 시작한다.</summary>
    private void StartCaretBlink()
    {
        if (_focusedInput is null)
        {
            return;
        }

        _focusedInput.IsCaretVisible = true;
        _caretTimer?.Stop();
        _caretTimer?.Start();
        Invalidate();
    }

    private void ToggleCaret()
    {
        if (_focusedInput is null)
        {
            _caretTimer?.Stop();
            return;
        }

        _focusedInput.IsCaretVisible = !_focusedInput.IsCaretVisible;
        if (IsHandleCreated && !IsDisposed)
        {
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_direct2D is { IsInitialized: true } && _paintEngine is not null)
        {
            try
            {
                Render();
                return;
            }
            catch (Exception ex)
            {
                // 방어 코드: 렌더링 중 예외가 나도 앱이 죽지 않도록 그래픽 자원을 정리하고
                // 폴백 화면으로 대체한다.
                Diagnostics.CrashLogger.Log(ex, "render");
                _renderError = ex;
                try
                {
                    DisposeGraphics();
                }
                catch (Exception cleanupError)
                {
                    Diagnostics.CrashLogger.Log(cleanupError, "render-cleanup");
                }
            }
        }

        RenderFallback(e.Graphics);
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
            _paintEngine.Paint(_direct2D.RenderTarget, _direct2D, layoutRoot, _selection);

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
        var error = _renderError ?? _initializationError;
        var message = error is null
            ? "페이지를 불러오는 중입니다..."
            : "렌더링 실패: " + error.Message;
        graphics.DrawString(message, font, brush, new PointF(12f, 12f));
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // 이전 렌더링 실패로 그래픽 자원이 정리된 상태라면 다시 초기화를 시도한다.
        if (_direct2D is null && IsHandleCreated && !IsDisposed)
        {
            _renderError = null;
            InitializeGraphics();
        }

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
            _caretTimer?.Stop();
            _caretTimer?.Dispose();
            _caretTimer = null;
            DisposeGraphics();
            _contextMenu.Dispose();
            _engine.Dispose();
        }

        base.Dispose(disposing);
    }
}
