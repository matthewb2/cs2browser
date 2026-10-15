using System.IO;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2DFactoryType = Vortice.Direct2D1.FactoryType;
using DColor = Vortice.Mathematics.Color4;
using DSizeI = Vortice.Mathematics.SizeI;
using DwFactoryType = Vortice.DirectWrite.FactoryType;
using IWICImagingFactory = Vortice.WIC.IWICImagingFactory;
using DMatrix3x2 = System.Numerics.Matrix3x2;

namespace CS2Browser.Rendering;

/// <summary>
/// Direct2D / DirectWrite 디바이스, 렌더 타겟, 브러시의 생명주기를 관리한다.
/// Vortice 를 사용하며, HWND 기반 렌더 타겟(HwndRenderTarget)을 사용한다.
/// 이미지 디코딩을 위한 WIC 팩토리와 비트맵 캐시도 함께 관리한다.
/// </summary>
public sealed class Direct2DContext : IDisposable
{
    private readonly Dictionary<uint, ID2D1SolidColorBrush> _brushes = new();
    private readonly Dictionary<string, ID2D1Bitmap?> _images = new(StringComparer.Ordinal);

    private ID2D1Factory1? _factory;
    private IDWriteFactory? _writeFactory;
    private IWICImagingFactory? _wicFactory;
    private ID2D1RenderTarget? _renderTarget;

    private IntPtr _hwnd;
    private int _width = 1;
    private int _height = 1;

    public ID2D1RenderTarget RenderTarget =>
        _renderTarget ?? throw new InvalidOperationException("Direct2D 렌더 타겟이 초기화되지 않았습니다.");

    public IDWriteFactory WriteFactory =>
        _writeFactory ?? throw new InvalidOperationException("DirectWrite 팩토리가 초기화되지 않았습니다.");

    public IWICImagingFactory WicFactory =>
        _wicFactory ?? throw new InvalidOperationException("WIC 팩토리가 초기화되지 않았습니다.");

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
        _wicFactory = new IWICImagingFactory();
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

    /// <summary>렌더 타겟 변환(스크롤 등)을 설정한다.</summary>
    public void SetTransform(DMatrix3x2 transform) => RenderTarget.Transform = transform;

    /// <summary>렌더 타겟 변환을 항등으로 되돌린다.</summary>
    public void ResetTransform() => RenderTarget.Transform = DMatrix3x2.Identity;

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

    /// <summary>이미지 소스(절대 URL/로컬 경로)에 대한 Direct2D 비트맵을 로드/캐시한다.</summary>
    public ID2D1Bitmap? GetImage(string source)
    {
        if (string.IsNullOrEmpty(source) || _renderTarget is null || _wicFactory is null)
        {
            return null;
        }

        if (_images.TryGetValue(source, out var cached))
        {
            return cached;
        }

        ID2D1Bitmap? bitmap = null;
        var localPath = ToLocalPath(source);
        if (localPath is not null)
        {
            bitmap = ImageLoader.LoadBitmap(_renderTarget, _wicFactory, localPath);
        }

        _images[source] = bitmap;
        return bitmap;
    }

    private static string? ToLocalPath(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return uri.LocalPath;
        }

        return File.Exists(source) ? source : null;
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
        // 비트맵은 렌더 타겟에 종속되므로 타겟보다 먼저 해제한다.
        foreach (var image in _images.Values)
        {
            image?.Dispose();
        }

        _images.Clear();

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
        _wicFactory?.Dispose();
        _wicFactory = null;
        _writeFactory?.Dispose();
        _writeFactory = null;
        _factory?.Dispose();
        _factory = null;
    }
}
