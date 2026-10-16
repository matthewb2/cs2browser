using System.Text;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;

namespace CS2Browser.Layout;

/// <summary>
/// display:flex / inline-flex 컨테이너용 레이아웃 핸들러.
/// flex-direction:row(기본값) 는 자식들을 내용 크기로 가로 배치하고,
/// 그 외 방향은 일반 컨테이너 레이아웃으로 폴백한다.
/// </summary>
internal sealed class FlexLayoutHandler : ContainerLayoutHandler
{
    public static readonly FlexLayoutHandler Instance = new();

    public override float Layout(
        IElement element,
        ICssStyleDeclaration? style,
        LayoutBox parent,
        float left,
        float top,
        float availableWidth,
        LinkTarget? link,
        LayoutTreeBuilder builder)
    {
        var flexDirection = (style?.GetPropertyValue("flex-direction") ?? string.Empty).Trim().ToLowerInvariant();
        if (flexDirection.Length > 0 && !flexDirection.StartsWith("row", StringComparison.Ordinal))
        {
            // row 가 아니면 일반 컨테이너 레이아웃으로 처리한다.
            return base.Layout(element, style, parent, left, top, availableWidth, link, builder);
        }

        var box = builder.CreateContentBox(
            element,
            style,
            parent,
            left,
            top,
            availableWidth,
            link,
            out var contentLeft,
            out var contentTop,
            out var innerWidth);

        var cursorX = contentLeft;
        var maxBottom = contentTop;

        foreach (var child in element.ChildNodes)
        {
            if (child is not IElement childElement)
            {
                continue;
            }

            var childStyle = LayoutTreeBuilder.SafeStyle(childElement);
            if (LayoutTreeBuilder.GetDisplay(childStyle) == "none")
            {
                continue;
            }

            var remaining = MathF.Max(1f, contentLeft + innerWidth - cursorX);
            var childWidth = MathF.Min(EstimateIntrinsicWidth(childElement, childStyle, builder), remaining);

            var bottom = builder.LayoutElement(childElement, box, cursorX, contentTop, childWidth, box.Link);
            var childBox = box.Children[^1];

            cursorX = childBox.X + childBox.Width + childBox.PaddingRight + childBox.BorderRight + childBox.MarginRight;
            maxBottom = MathF.Max(maxBottom, bottom);
        }

        box.Height = MathF.Max(0f, maxBottom - contentTop);
        return maxBottom + box.PaddingBottom + box.BorderBottom + box.MarginBottom;
    }

    /// <summary>자신의 내용만으로 결정되는 축소(shrink-to-fit) 폭을 근사 계산한다.</summary>
    private static float EstimateIntrinsicWidth(IElement element, ICssStyleDeclaration? style, LayoutTreeBuilder builder)
    {
        var horizontalEdges =
            LayoutTreeBuilder.ParseLength(style?.GetPropertyValue("margin-left")) + LayoutTreeBuilder.ParseLength(style?.GetPropertyValue("margin-right"))
            + LayoutTreeBuilder.ParseLength(style?.GetPropertyValue("border-left-width")) + LayoutTreeBuilder.ParseLength(style?.GetPropertyValue("border-right-width"))
            + LayoutTreeBuilder.ParseLength(style?.GetPropertyValue("padding-left")) + LayoutTreeBuilder.ParseLength(style?.GetPropertyValue("padding-right"));

        var textStyle = LayoutTreeBuilder.ReadTextStyle(style);
        var textBuilder = new StringBuilder();
        LayoutTreeBuilder.CollectText(element, textBuilder);
        var text = textBuilder.ToString().Trim();
        var textWidth = builder.MeasureWidth(text, textStyle);

        return horizontalEdges + textWidth + 1f;
    }
}
