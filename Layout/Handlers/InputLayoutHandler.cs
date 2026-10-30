using System.Globalization;
using System.Text;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Vortice.Mathematics;

namespace CS2Browser.Layout;

/// <summary>
/// &lt;input type="text"&gt; 및 &lt;textarea&gt;와 같은 텍스트 입력 요소를 레이아웃한다.
/// </summary>
internal sealed class InputLayoutHandler : ILayoutHandler
{
    public static readonly InputLayoutHandler Instance = new();

    private const float DefaultInputWidth = 160f;
    private const float DefaultInputHeight = 20f;
    private const float DefaultTextareaMinHeight = 48f;
    private const float TextareaLineHeightFactor = 1.6f;

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
        var textStyle = LayoutTreeBuilder.ReadTextStyle(style);

        // label 등 주변 인라인 요소와의 기본 간격(2px) 적용
        var marginLeft = edges.MarginLeft > 0f ? edges.MarginLeft : 2f;
        var marginRight = edges.MarginRight > 0f ? edges.MarginRight : 2f;

        // 브라우저 UA 스타일과 유사하게, CSS 가 전혀 없어도 입력 상자가 보이도록
        // 기본 테두리/안쪽 여백을 준다.
        var (borderTop, borderRight, borderBottom, borderLeft) = EnsureBorder(edges);
        var (padTop, padRight, padBottom, padLeft) = EnsurePadding(edges);

        var leftInner = left + marginLeft + borderLeft + padLeft;
        var topInner = top + edges.MarginTop + borderTop + padTop;

        var innerWidth = availableWidth
            - marginLeft - marginRight
            - borderLeft - borderRight
            - padLeft - padRight;
        if (innerWidth < 10f)
        {
            innerWidth = 10f;
        }

        var isTextarea = IsTextarea(element);
        var fontSize = textStyle.FontSize > 0f ? textStyle.FontSize : LayoutTreeBuilder.DefaultFontSize;
        var lineHeight = fontSize * LayoutTreeBuilder.LineHeightFactor;

        var value = GetValue(element);

        float width;
        float height;

        var cssWidth = LayoutTreeBuilder.ParseLength(style?.GetPropertyValue("width"));
        var cssHeight = LayoutTreeBuilder.ParseLength(style?.GetPropertyValue("height"));

        if (isTextarea)
        {
            width = cssWidth > 0f ? cssWidth : innerWidth;
            if (width < 40f)
            {
                width = innerWidth;
            }

            if (width > innerWidth)
            {
                width = innerWidth;
            }

            if (width < 20f)
            {
                width = MathF.Min(200f, innerWidth);
            }

            var rowsAttr = ParseInt(element.GetAttribute("rows"));
            var rows = rowsAttr > 0 ? rowsAttr : 2;
            var effectiveLine = cssHeight > 0f ? fontSize * TextareaLineHeightFactor : lineHeight;
            height = cssHeight > 0f ? cssHeight : rows * effectiveLine + padTop + padBottom;
            if (height < DefaultTextareaMinHeight)
            {
                height = DefaultTextareaMinHeight;
            }
        }
        else
        {
            width = cssWidth > 0f ? cssWidth : DefaultInputWidth;
            if (width > innerWidth)
            {
                width = innerWidth;
            }
            if (width < 20f)
            {
                width = MathF.Min(DefaultInputWidth, innerWidth);
            }

            height = cssHeight > 0f ? cssHeight : DefaultInputHeight;
            if (height < fontSize + 2f)
            {
                height = fontSize + 2f;
            }
        }

        var box = new LayoutBox
        {
            TagName = element.LocalName.ToLowerInvariant(),
            BackgroundColor = GetBackground(style),
            BorderColor = GetBorderColor(style),
            Link = link,
            MarginTop = edges.MarginTop,
            MarginRight = marginRight,
            MarginBottom = edges.MarginBottom,
            MarginLeft = marginLeft,
            BorderTop = borderTop,
            BorderRight = borderRight,
            BorderBottom = borderBottom,
            BorderLeft = borderLeft,
            PaddingTop = padTop,
            PaddingRight = padRight,
            PaddingBottom = padBottom,
            PaddingLeft = padLeft,
            X = leftInner,
            Y = topInner,
            Width = width,
            Height = height,
            IsTextInput = true,
            InputText = value ?? string.Empty,
            CaretIndex = 0,
            IsFocused = false,
            IsCaretVisible = false,
            FontSize = fontSize,
            FontFamily = textStyle.FontFamily,
            TextColor = textStyle.Color
        };

        parent.Children.Add(box);

        return top + edges.MarginTop + borderTop + height + borderBottom + edges.MarginBottom;
    }

    /// <summary>명시된 테두리가 전혀 없으면 입력 상자가 보이도록 기본 1px 테두리를 준다.</summary>
    private static (float Top, float Right, float Bottom, float Left) EnsureBorder(LayoutTreeBuilder.BoxEdges edges)
        => edges.BorderTop > 0f || edges.BorderRight > 0f || edges.BorderBottom > 0f || edges.BorderLeft > 0f
            ? (edges.BorderTop, edges.BorderRight, edges.BorderBottom, edges.BorderLeft)
            : (1f, 1f, 1f, 1f);

    /// <summary>안쪽 여백이 0 인 변에는 브라우저와 유사한 기본 패딩을 준다.</summary>
    private static (float Top, float Right, float Bottom, float Left) EnsurePadding(LayoutTreeBuilder.BoxEdges edges)
        => (
            edges.PaddingTop > 0f ? edges.PaddingTop : 1f,
            edges.PaddingRight > 0f ? edges.PaddingRight : 2f,
            edges.PaddingBottom > 0f ? edges.PaddingBottom : 1f,
            edges.PaddingLeft > 0f ? edges.PaddingLeft : 2f);

    private static bool IsTextarea(IElement element)
        => string.Equals(element.LocalName, "textarea", StringComparison.OrdinalIgnoreCase);

    private static string? GetValue(IElement element)
    {
        if (IsTextarea(element))
        {
            var sb = new StringBuilder();
            foreach (var child in element.ChildNodes)
            {
                if (child is IText text)
                {
                    sb.Append(text.Data);
                }
            }
            return sb.ToString();
        }

        return element.GetAttribute("value");
    }

    private static Color4 GetBackground(ICssStyleDeclaration? style)
    {
        if (CssColor.TryParse(style?.GetPropertyValue("background-color"), out var color) && color.A > 0f)
        {
            return color;
        }
        return new Color4(1f, 1f, 1f, 1f);
    }

    private static Color4 GetBorderColor(ICssStyleDeclaration? style)
    {
        // 테두리 색 미지정 시 currentColor(= 글자색) 또는 기본 회색을 사용한다.
        if (CssColor.TryParse(style?.GetPropertyValue("border-top-color"), out var color) && color.A > 0f)
        {
            return color;
        }

        return new Color4(0.47f, 0.47f, 0.47f, 1f);
    }

    private static int ParseInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
