using System.Globalization;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;

namespace CS2Browser.Layout;

/// <summary>
/// &lt;img&gt; 대체 요소(replaced element)의 레이아웃과 관련된 부분.
/// 크기 결정(CSS → 속성 → 고유 크기), URL 해석, 여백/테두리 처리를 담당한다.
/// </summary>
public sealed partial class LayoutTreeBuilder
{
    /// <summary>대체 요소(replaced element)인 &lt;img&gt; 를 레이아웃한다.</summary>
    private float LayoutImage(IElement element, ICssStyleDeclaration? style, LayoutBox parent, float left, float top, float availableWidth, LinkTarget? link = null)
    {
        var marginTop = ParseLength(style?.GetPropertyValue("margin-top"));
        var marginRight = ParseLength(style?.GetPropertyValue("margin-right"));
        var marginBottom = ParseLength(style?.GetPropertyValue("margin-bottom"));
        var marginLeft = ParseLength(style?.GetPropertyValue("margin-left"));
        var borderLeft = ParseLength(style?.GetPropertyValue("border-left-width"));
        var borderRight = ParseLength(style?.GetPropertyValue("border-right-width"));
        var paddingLeft = ParseLength(style?.GetPropertyValue("padding-left"));
        var paddingRight = ParseLength(style?.GetPropertyValue("padding-right"));

        var source = ResolveUrl(_baseUrl, element.GetAttribute("src"));

        var naturalWidth = 0;
        var naturalHeight = 0;
        var localPath = TryGetLocalPath(source);
        if (localPath is not null)
        {
            ImageInfo.TryGetSizeFromFile(localPath, out naturalWidth, out naturalHeight);
        }

        var cssWidth = ParseLength(style?.GetPropertyValue("width"));
        var cssHeight = ParseLength(style?.GetPropertyValue("height"));
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
            Parent = parent,
            ImageSource = string.IsNullOrEmpty(source) ? null : source,
            Link = link,
            MarginTop = marginTop,
            MarginRight = marginRight,
            MarginBottom = marginBottom,
            MarginLeft = marginLeft,
            BorderLeft = borderLeft,
            BorderRight = borderRight,
            PaddingLeft = paddingLeft,
            PaddingRight = paddingRight,
            Width = contentWidth,
            Height = contentHeight
        };
        box.X = left + marginLeft + borderLeft + paddingLeft;
        box.Y = top + marginTop;
        parent.Children.Add(box);

        return top + marginTop + contentHeight + marginBottom;
    }

    private static bool IsImageElement(IElement element)
        => string.Equals(element.LocalName, "img", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 요소가 &lt;a href&gt; 이면 기준 URL로 해석한 <see cref="LinkTarget"/> 을 반환한다. 아니면 null.
    /// 앵커마다 새 인스턴스를 만들어 같은 href 의 다른 링크와 구분한다.
    /// </summary>
    private LinkTarget? GetLinkTarget(IElement element)
    {
        if (!string.Equals(element.LocalName, "a", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var href = element.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var resolved = ResolveUrl(_baseUrl, href);
        return string.IsNullOrWhiteSpace(resolved) ? null : new LinkTarget(resolved);
    }

    /// <summary>이 요소의 자손 중 대체 요소(&lt;img&gt;)가 있는지 확인한다.</summary>
    private static bool ContainsImage(IElement element)
    {
        foreach (var child in element.Children)
        {
            if (IsImageElement(child) || ContainsImage(child))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveUrl(string baseUrl, string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            && Uri.TryCreate(baseUri, source, out var combined))
        {
            return combined.ToString();
        }

        return source;
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
