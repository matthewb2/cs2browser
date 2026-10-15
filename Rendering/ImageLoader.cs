using System.IO;
using Vortice.Direct2D1;
using Vortice.WIC;

namespace CS2Browser.Rendering;

/// <summary>
/// WIC(Windows Imaging Component)를 사용해 PNG/JPEG 등 이미지 파일을
/// Direct2D 비트맵으로 디코딩하는 로더. WIC 바인딩은 Vortice.Direct2D1 에 포함되어 있다.
/// </summary>
public static class ImageLoader
{
    /// <summary>파일을 Direct2D 비트맵으로 디코딩한다. 실패하거나 파일이 없으면 null.</summary>
    public static ID2D1Bitmap? LoadBitmap(ID2D1RenderTarget renderTarget, IWICImagingFactory wicFactory, string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var decoder = wicFactory.CreateDecoderFromFileName(filePath, FileAccess.Read, DecodeOptions.CacheOnLoad);
            using var frame = decoder.GetFrame(0);

            using var converter = wicFactory.CreateFormatConverter();
            converter.Initialize(frame, PixelFormat.Format32bppPBGRA);

            return renderTarget.CreateBitmapFromWicBitmap(converter, null);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>이미지의 고유 픽셀 크기를 읽는다.</summary>
    public static bool TryGetPixelSize(IWICImagingFactory wicFactory, string filePath, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var decoder = wicFactory.CreateDecoderFromFileName(filePath, FileAccess.Read, DecodeOptions.CacheOnLoad);
            using var frame = decoder.GetFrame(0);
            frame.GetSize(out var w, out var h);
            width = (int)w;
            height = (int)h;
            return width > 0 && height > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
