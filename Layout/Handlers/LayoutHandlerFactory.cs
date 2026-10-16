using AngleSharp.Dom;

namespace CS2Browser.Layout;

/// <summary>
/// 요소의 display(block / inline / flex / img)에 따라 알맞은 <see cref="ILayoutHandler"/> 를 선택한다.
/// 박스 레이아웃 생성과, 인라인 흐름 참여 여부 판정을 모두 이 팩토리에서 담당한다.
/// </summary>
internal static class LayoutHandlerFactory
{
    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "body", "div", "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li",
        "dl", "dt", "dd", "section", "header", "footer", "nav", "article", "main", "aside",
        "form", "table", "thead", "tbody", "tfoot", "tr", "blockquote", "pre", "hr",
        "figure", "figcaption", "fieldset", "address", "details", "summary"
    };

    /// <summary>요소의 display 값에 맞는 레이아웃 핸들러를 반환한다.</summary>
    public static ILayoutHandler GetHandler(IElement element, string display)
    {
        if (LayoutTreeBuilder.IsImageElement(element))
        {
            return ImageLayoutHandler.Instance;
        }

        if (IsFlex(display))
        {
            return FlexLayoutHandler.Instance;
        }

        if (IsInlineLevel(element, display))
        {
            return InlineLayoutHandler.Instance;
        }

        return BlockLayoutHandler.Instance;
    }

    /// <summary>display 가 flex 컨테이너(flex / inline-flex)인지 확인한다.</summary>
    public static bool IsFlex(string display)
        => display is "flex" or "inline-flex";

    /// <summary>display 가 인라인 레벨(inline / inline-block …)인지 확인한다. display 가 비면 태그로 추정한다.</summary>
    public static bool IsInlineLevel(IElement element, string display)
        => !string.IsNullOrEmpty(display)
            ? display.StartsWith("inline", StringComparison.Ordinal)
            : !BlockTags.Contains(element.LocalName);

    /// <summary>
    /// 자식이 부모의 인라인 흐름에 참여하는지(=독립 박스를 만들지 않는지) 판정한다.
    /// 이미지/이미지 포함 요소/flex 컨테이너는 박스를 만들므로 제외한다.
    /// </summary>
    public static bool IsInlineChild(IElement element, string display)
        => !LayoutTreeBuilder.IsImageElement(element)
        && !LayoutTreeBuilder.ContainsImage(element)
        && !IsFlex(display)
        && IsInlineLevel(element, display);
}
