using AngleSharp.Css.Dom;
using AngleSharp.Dom;

namespace CS2Browser.Layout;

/// <summary>
/// 요소 하나를 레이아웃하는 전략. 요소의 display(block / inline / flex / img)마다 구현이 다르며,
/// <see cref="LayoutHandlerFactory"/> 가 요소의 display 에 맞는 구현을 선택한다.
/// </summary>
internal interface ILayoutHandler
{
    /// <summary>요소를 부모 박스 안에 배치하고, 다음 형제가 시작할 Y 좌표를 반환한다.</summary>
    float Layout(
        IElement element,
        ICssStyleDeclaration? style,
        LayoutBox parent,
        float left,
        float top,
        float availableWidth,
        LinkTarget? link,
        LayoutTreeBuilder builder);
}
