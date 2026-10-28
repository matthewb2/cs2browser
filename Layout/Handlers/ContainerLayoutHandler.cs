using AngleSharp.Css.Dom;
using AngleSharp.Dom;

namespace CS2Browser.Layout;

/// <summary>
/// 자식들을 내용 박스 안에 배치하는 컨테이너 핸들러의 공통 구현.
/// 인라인 내용은 <see cref="InlineFlow"/> 로 흐르고, 박스 자식(블록/이미지/flex)은 세로로 쌓인다.
/// display:block 과 display:inline 요소가 이 레이아웃을 공유한다.
/// </summary>
internal abstract class ContainerLayoutHandler : ILayoutHandler
{
    public virtual float Layout(
        IElement element,
        ICssStyleDeclaration? style,
        LayoutBox parent,
        float left,
        float top,
        float availableWidth,
        LinkTarget? link,
        LayoutTreeBuilder builder)
    {
        var textStyle = LayoutTreeBuilder.ReadTextStyle(style);

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

        var flow = new InlineFlow(builder, box, contentLeft, contentTop, innerWidth);

        foreach (var child in element.ChildNodes)
        {
            switch (child)
            {
                case IElement childElement:
                    var childStyle = LayoutTreeBuilder.SafeStyle(childElement);
                    var childDisplay = LayoutTreeBuilder.GetDisplay(childStyle);
                    if (childDisplay == "none")
                    {
                        continue;
                    }

                    if (LayoutHandlerFactory.IsInlineChild(childElement, childDisplay))
                    {
                        flow.AppendInlineElement(childElement, textStyle, link);
                    }
                    else
                    {
                        flow.LayoutBoxChild(childElement, link);
                    }

                    break;

                case IText textNode:
                    flow.AppendText(textNode.Data, textStyle, link);
                    break;
            }
        }

        var bottom = flow.Flush();

        // CSS 표준 마진 병합 규칙: 부모에 하단 패딩/테두리가 없고 마지막 자식의 하단 마진이 있는 경우 처리
        var edges = LayoutTreeBuilder.ReadEdges(style);
        var finalBottom = bottom;

        box.Height = MathF.Max(0f, finalBottom - contentTop);
        return finalBottom + box.PaddingBottom + box.BorderBottom + box.MarginBottom;
    }
}