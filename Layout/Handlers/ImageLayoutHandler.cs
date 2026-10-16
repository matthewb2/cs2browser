using System.Globalization;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;

namespace CS2Browser.Layout;

/// <summary>
/// &lt;img&gt; 대체 요소(replaced element) 레이아웃 핸들러.
/// 크기 결정(CSS → 속성 → 고유 크기), URL 해석, 여백/테두리 처리를 담당한다.
/// </summary>
internal sealed class ImageLayoutHandler : ILayoutHandler
{
    public static readonly ImageLayoutHandler Instance = new();

    public float Layout(
        IElement element,
        ICssStyleDeclaration? style,
        LayoutBox parent,
        float left,
        float top,
        float availableWidth,
        LinkTarget? link,
        LayoutTreeBuilder builder)
    {
        var edges = LayoutTreeBuilder.ReadEdges(style);

        var source = LayoutTreeBuilder.ResolveUrl(builder.BaseUrl, element.GetAttribute("src"));

        var naturalWidth = 0;
        var naturalHeight = 0;
        var localPath = TryGetLocalPath(source);
        if (localPath is not null)
        {
            ImageInfo.TryGetSizeFromFile(localPath, out naturalWidth, out naturalHeight);
        }

        var cssWidth = LayoutTreeBuilder.ParseLength(style?.GetPropertyValue("width"));
        var cssHeight = LayoutTreeBuilder.ParseLength(style?.GetPropertyValue("height"));
        var attrWidth = ParseInt(element.GetAttribute("width"));
        var attrHeight = ParseInt(element.GetAttribute("height"));

        var contentWidth = cssWidth > 0f ? cssWidth : attrWidth > 0 ? attrWidth : naturalWidth;
        var contentHeight = cssHeight > 0f ? cssHeight : attrHeight > 0 ? attrHeight : 0f;

        if (contentHeight <= 0f)
        {
            contentHeight = contentWidth > 0f && naturalWidth > 0 && naturalHeight > 0
                ? contentWidth * naturalHeight / naturalWidth
                : naturalHeight;
        }

        if (contentWidth <= 0f)
        {
            contentWidth = naturalWidth > 0 ? naturalWidth : availableWidth;
        }

        if (contentHeight <= 0f)
        {
            contentHeight = contentWidth;
        }

        contentWidth = MathF.Min(contentWidth, MathF.Max(1f, availableWidth));

        var box = new LayoutBox
        {
            TagName = "img",
            ImageSource = string.IsNullOrEmpty(source) ? null : source,
            Link = link,
            MarginTop = edges.MarginTop,
            MarginRight = edges.MarginRight,
            MarginBottom = edges.MarginBottom,
            MarginLeft = edges.MarginLeft,
            BorderLeft = edges.BorderLeft,
            BorderRight = edges.BorderRight,
            PaddingLeft = edges.PaddingLeft,
            PaddingRight = edges.PaddingRight,
            Width = contentWidth,
            Height = contentHeight
        };
        box.X = left + edges.MarginLeft + edges.BorderLeft + edges.PaddingLeft;
        box.Y = top + edges.MarginTop;
        parent.Children.Add(box);

        return top + edges.MarginTop + contentHeight + edges.MarginBottom;
    }

    private static string? TryGetLocalPath(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                return uri.LocalPath;
            }
        }
        catch (UriFormatException)
        {
            return null;
        }

        return File.Exists(url) ? url : null;
    }

    private static int ParseInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
