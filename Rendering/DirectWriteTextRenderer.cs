using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using DwFontStyle = Vortice.DirectWrite.FontStyle;

namespace CS2Browser.Rendering;

/// <summary>
/// DirectWrite 를 이용한 폰트/텍스트 렌더링과 텍스트 레이아웃(줄바꿈)을 담당한다.
/// 한글/영문 혼용을 고려해 시스템 폰트(Malgun Gothic 등)를 사용한다.
/// </summary>
public sealed class DirectWriteTextRenderer : IDisposable
{
    private readonly IDWriteFactory _factory;
    private readonly Dictionary<FormatKey, IDWriteTextFormat> _formats = new();

    public DirectWriteTextRenderer(IDWriteFactory factory) => _factory = factory;

    public IDWriteTextFormat GetFormat(string fontFamily, float fontSize, bool bold, bool italic)
    {
        var key = new FormatKey(fontFamily, fontSize, bold, italic);
        if (_formats.TryGetValue(key, out var format))
        {
            return format;
        }

        format = _factory.CreateTextFormat(
            string.IsNullOrWhiteSpace(fontFamily) ? "Malgun Gothic" : fontFamily,
            bold ? FontWeight.Bold : FontWeight.Normal,
            italic ? DwFontStyle.Italic : DwFontStyle.Normal,
            FontStretch.Normal,
            MathF.Max(1f, fontSize));

        format.TextAlignment = TextAlignment.Leading;
        format.ParagraphAlignment = ParagraphAlignment.Near;
        format.WordWrapping = WordWrapping.NoWrap;

        _formats[key] = format;
        return format;
    }

    public void DrawText(
        ID2D1RenderTarget target,
        string text,
        float x,
        float y,
        float width,
        float height,
        ID2D1Brush brush,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic)
    {
        if (string.IsNullOrEmpty(text) || brush is null)
        {
            return;
        }

        var format = GetFormat(fontFamily, fontSize, bold, italic);
        using var layout = _factory.CreateTextLayout(text, format, MathF.Max(1f, width), MathF.Max(1f, height));
        target.DrawTextLayout(new Vector2(x, y), layout, brush);
    }

    public void Dispose()
    {
        foreach (var format in _formats.Values)
        {
            format.Dispose();
        }

        _formats.Clear();
    }

    private readonly record struct FormatKey(string FontFamily, float FontSize, bool Bold, bool Italic);
}
