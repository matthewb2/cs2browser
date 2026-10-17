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
    private const float DefaultInputHeight = 22f;
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

        var leftInner = left + edges.MarginLeft + edges.BorderLeft + edges.PaddingLeft;
        var topInner = top + edges.MarginTop + edges.BorderTop + edges.PaddingTop;

        var innerWidth = availableWidth
            - edges.MarginLeft - edges.MarginRight
            - edges.BorderLeft - edges.BorderRight
            - edges.PaddingLeft - edges.PaddingRight;
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
            height = cssHeight > 0f ? cssHeight : rows * effectiveLine + edges.PaddingTop + edges.PaddingBottom;
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
            if (height < lineHeight + 6f)
            {
                height = lineHeight + 6f;
            }
        }

        var box = new LayoutBox
        {
            TagName = element.LocalName.ToLowerInvariant(),
            BackgroundColor = GetBackground(style),
            Link = link,
            MarginTop = edges.MarginTop,
            MarginRight = edges.MarginRight,
            MarginBottom = edges.MarginBottom,
            MarginLeft = edges.MarginLeft,
            BorderTop = edges.BorderTop,
            BorderRight = edges.BorderRight,
            BorderBottom = edges.BorderBottom,
            BorderLeft = edges.BorderLeft,
            PaddingTop = edges.PaddingTop,
            PaddingRight = edges.PaddingRight,
            PaddingBottom = edges.PaddingBottom,
            PaddingLeft = edges.PaddingLeft,
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

        return top + edges.MarginTop + height + edges.BorderBottom + edges.MarginBottom;
    }

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

    private static int ParseInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
