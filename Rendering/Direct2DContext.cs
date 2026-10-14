using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2DFactoryType = Vortice.Direct2D1.FactoryType;
using DColor = Vortice.Mathematics.Color4;
using DSizeI = Vortice.Mathematics.SizeI;
using DwFactoryType = Vortice.DirectWrite.FactoryType;

namespace CS2Browser.Rendering;

/// <summary>
/// Direct2D / DirectWrite 디바이스, 렌더 타겟, 브러시의 생명주기를 관리한다.
/// Vortice 를 사용하며, HWND 기반 렌더 타겟(HwndRenderTarget)을 사용한다.
/// </summary>
public sealed class Direct2DContext : IDisposable
{
    private readonly Dictionary<uint, ID2D1SolidColorBrush> _brushes = new();

    private ID2D1Factory1? _factory;
    private IDWriteFactory? _writeFactory;
    private ID2D1RenderTarget? _renderTarget;

    private IntPtr _hwnd;
    private int _width = 1;
    private int _height = 1;

    public ID2D1RenderTarget RenderTarget =>
        _renderTarget ?? throw new InvalidOperationException("Direct2D 렌더 타겟이 초기화되지 않았습니다.");

    public IDWriteFactory WriteFactory =>
        _writeFactory ?? throw new InvalidOperationException("DirectWrite 팩토리가 초기화되지 않았습니다.");

    public bool IsInitialized => _renderTarget is not null;

    /// <summary>지정한 HWND 표면에 렌더 타겟을 만든다.</summary>
    public void Initialize(IntPtr hwnd, int pixelWidth, int pixelHeight)
    {
        if (IsInitialized)
        {
            return;
        }

        _factory = D2D1.D2D1CreateFactory<ID2D1Factory1>(D2DFactoryType.SingleThreaded, DebugLevel.None);
        _writeFactory = DWrite.DWriteCreateFactory<IDWriteFactory>(DwFactoryType.Shared);
        _hwnd = hwnd;
        CreateTarget(pixelWidth, pixelHeight);
    }

    /// <summary>크기가 바뀌면 렌더 타겟을 다시 만든다.</summary>
    public void Resize(int pixelWidth, int pixelHeight)
    {
        if (!IsInitialized)
        {
            return;
        }

        var width = Math.Max(1, pixelWidth);
        var height = Math.Max(1, pixelHeight);
        if (width == _width && height == _height)
        {
            return;
        }

        CreateTarget(width, height);
    }

    public void BeginDraw() => RenderTarget.BeginDraw();

    public void Clear(DColor color) => RenderTarget.Clear(color);

    public bool EndDraw()
    {
        var result = RenderTarget.EndDraw();
        if (!result.Success)
        {
            // 디바이스 손실 등 → 타겟 재생성
            CreateTarget(_width, _height);
            return false;
        }

        return true;
    }

    /// <summary>색상별로 브러시를 캐시해 재사용한다.</summary>
    public ID2D1SolidColorBrush GetBrush(DColor color)
    {
        var key = color.ToBgra();
        if (!_brushes.TryGetValue(key, out var brush))
        {
            brush = RenderTarget.CreateSolidColorBrush(color, null);
            _brushes[key] = brush;
        }

        return brush;
    }

    private void CreateTarget(int width, int height)
    {
        ReleaseTarget();
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);

        var renderTargetProperties = new RenderTargetProperties(
            RenderTargetType.Default,
            new PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
            96f,
            96f,
            RenderTargetUsage.None,
            FeatureLevel.Default);

        var hwndProperties = new HwndRenderTargetProperties
        {
            Hwnd = _hwnd,
            PixelSize = new DSizeI(_width, _height),
            PresentOptions = PresentOptions.None
        };

        _renderTarget = _factory!.CreateHwndRenderTarget(renderTargetProperties, hwndProperties);
        _renderTarget.SetDpi(96f, 96f);
    }

    private void ReleaseTarget()
    {
        foreach (var brush in _brushes.Values)
        {
            brush.Dispose();
        }

        _brushes.Clear();
        _renderTarget?.Dispose();
        _renderTarget = null;
    }

    public void Dispose()
    {
        ReleaseTarget();
        _writeFactory?.Dispose();
        _writeFactory = null;
        _factory?.Dispose();
        _factory = null;
    }
}
