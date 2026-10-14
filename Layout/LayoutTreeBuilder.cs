using System.Globalization;
using System.Text;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Vortice.Mathematics;

namespace CS2Browser.Layout;

/// <summary>
/// DOM 트리와 계산된 스타일(ComputeStyle)을 바탕으로 레이아웃 트리를 만든다.
/// 블록 요소는 세로로 쌓이고, 텍스트/인라인 내용은 줄바꿈하며 흐른다.
/// (개략적인 블록/인라인 플로우 구현)
/// </summary>
public sealed class LayoutTreeBuilder
{
    private const float DefaultFontSize = 16f;
    private const string DefaultFontFamily = "Malgun Gothic";
    private const float LineHeightFactor = 1.4f;

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "body", "div", "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li",
        "dl", "dt", "dd", "section", "header", "footer", "nav", "article", "main", "aside",
        "form", "table", "thead", "tbody", "tfoot", "tr", "blockquote", "pre", "hr",
        "figure", "figcaption", "fieldset", "address", "details", "summary"
    };

    public LayoutBox Build(IDocument document, float viewportWidth)
    {
        var root = new LayoutBox
        {
            TagName = "#viewport",
            BackgroundColor = Colors.White
        };
        root.X = 0f;
        root.Y = 0f;
        root.Width = viewportWidth;

        var body = document.Body ?? document.DocumentElement;
        if (body is not null)
        {
            var bottom = LayoutBlock(body, root, 0f, 0f, viewportWidth);
            root.Height = bottom;
        }

        return root;
    }

    private float LayoutBlock(IElement element, LayoutBox parent, float left, float top, float availableWidth)
    {
        var style = SafeStyle(element);
        var display = GetDisplay(style);
        if (display == "none")
        {
            return top;
        }

        var textStyle = ReadTextStyle(style);

        var marginTop = ParseLength(style?.GetPropertyValue("margin-top"));
        var marginRight = ParseLength(style?.GetPropertyValue("margin-right"));
        var marginBottom = ParseLength(style?.GetPropertyValue("margin-bottom"));
        var marginLeft = ParseLength(style?.GetPropertyValue("margin-left"));
        var borderTop = ParseLength(style?.GetPropertyValue("border-top-width"));
        var borderRight = ParseLength(style?.GetPropertyValue("border-right-width"));
        var borderBottom = ParseLength(style?.GetPropertyValue("border-bottom-width"));
        var borderLeft = ParseLength(style?.GetPropertyValue("border-left-width"));
        var paddingTop = ParseLength(style?.GetPropertyValue("padding-top"));
        var paddingRight = ParseLength(style?.GetPropertyValue("padding-right"));
        var paddingBottom = ParseLength(style?.GetPropertyValue("padding-bottom"));
        var paddingLeft = ParseLength(style?.GetPropertyValue("padding-left"));

        var contentLeft = left + marginLeft + borderLeft + paddingLeft;
        var contentTop = top + marginTop + borderTop + paddingTop;
        var innerWidth = MathF.Max(
            1f,
            availableWidth
            - marginLeft - marginRight
            - borderLeft - borderRight
            - paddingLeft - paddingRight);

        var box = new LayoutBox
        {
            TagName = element.LocalName,
            Parent = parent,
            BackgroundColor = ReadBackground(style),
            MarginTop = marginTop,
            MarginRight = marginRight,
            MarginBottom = marginBottom,
            MarginLeft = marginLeft,
            BorderTop = borderTop,
            BorderRight = borderRight,
            BorderBottom = borderBottom,
            BorderLeft = borderLeft,
            PaddingTop = paddingTop,
            PaddingRight = paddingRight,
            PaddingBottom = paddingBottom,
            PaddingLeft = paddingLeft
        };
        box.X = contentLeft;
        box.Y = contentTop;
        box.Width = innerWidth;
        parent.Children.Add(box);

        var cursorY = contentTop;
        var inlineBuffer = new List<InlineBox>();

        // flex-direction:row 인 컨테이너는 자식들을 가로로 배치한다.
        if (display is "flex" or "inline-flex")
        {
            var flexDirection = (style?.GetPropertyValue("flex-direction") ?? string.Empty).Trim().ToLowerInvariant();
            if (flexDirection.Length == 0 || flexDirection.StartsWith("row", StringComparison.Ordinal))
            {
                var flexBottom = FlowFlexRow(element, box, contentLeft, contentTop, innerWidth);
                box.Height = MathF.Max(0f, flexBottom - contentTop);
                return flexBottom + paddingBottom + borderBottom + marginBottom;
            }
        }

        foreach (var child in element.ChildNodes)
        {
            switch (child)
            {
                case IElement childElement:
                    var childDisplay = GetDisplay(SafeStyle(childElement));
                    if (childDisplay == "none")
                    {
                        continue;
                    }

                    if (IsBlock(childElement, childDisplay))
                    {
                        if (inlineBuffer.Count > 0)
                        {
                            cursorY = FlowInlineContent(box, inlineBuffer, contentLeft, cursorY, innerWidth);
                            inlineBuffer.Clear();
                        }

                        cursorY = LayoutBlock(childElement, box, contentLeft, cursorY, innerWidth);
                    }
                    else
                    {
                        CollectInline(childElement, textStyle, inlineBuffer);
                    }
                    break;

                case IText textNode:
                    AddTextRun(textNode.Data, textStyle, inlineBuffer);
                    break;
            }
        }

        if (inlineBuffer.Count > 0)
        {
            cursorY = FlowInlineContent(box, inlineBuffer, contentLeft, cursorY, innerWidth);
        }

        box.Height = MathF.Max(0f, cursorY - contentTop);
        return cursorY + paddingBottom + borderBottom + marginBottom;
    }

    /// <summary>flex-direction:row 흐름. 자식의 내용 크기(축소)로 가로 배치한다.</summary>
    private float FlowFlexRow(IElement element, LayoutBox box, float contentLeft, float contentTop, float innerWidth)
    {
        var cursorX = contentLeft;
        var maxBottom = contentTop;

        foreach (var child in element.ChildNodes)
        {
            if (child is not IElement childElement)
            {
                continue;
            }

            var childStyle = SafeStyle(childElement);
            if (GetDisplay(childStyle) == "none")
            {
                continue;
            }

            var remaining = MathF.Max(1f, contentLeft + innerWidth - cursorX);
            var childWidth = MathF.Min(EstimateIntrinsicWidth(childElement, childStyle), remaining);

            var bottom = LayoutBlock(childElement, box, cursorX, contentTop, childWidth);
            var childBox = box.Children[^1];

            cursorX = childBox.X + childBox.Width + childBox.PaddingRight + childBox.BorderRight + childBox.MarginRight;
            maxBottom = MathF.Max(maxBottom, bottom);
        }

        return maxBottom;
    }

    /// <summary>자신의 내용만으로 결정되는 축소(shrink-to-fit) 폭을 근사 계산한다.</summary>
    private static float EstimateIntrinsicWidth(IElement element, ICssStyleDeclaration? style)
    {
        var horizontalEdges =
            ParseLength(style?.GetPropertyValue("margin-left")) + ParseLength(style?.GetPropertyValue("margin-right"))
            + ParseLength(style?.GetPropertyValue("border-left-width")) + ParseLength(style?.GetPropertyValue("border-right-width"))
            + ParseLength(style?.GetPropertyValue("padding-left")) + ParseLength(style?.GetPropertyValue("padding-right"));

        var textStyle = ReadTextStyle(style);
        var builder = new StringBuilder();
        CollectText(element, builder);
        var textWidth = ApproximateTextWidth(builder.ToString().Trim(), textStyle.FontSize);

        return horizontalEdges + textWidth + 1f;
    }

    private static void CollectText(INode node, StringBuilder builder)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText text:
                    builder.Append(text.Data).Append(' ');
                    break;
                case IElement childElement when childElement.LocalName is not ("script" or "style"):
                    CollectText(childElement, builder);
                    break;
            }
        }
    }

    private static void CollectInline(IElement element, TextStyle parentStyle, List<InlineBox> buffer)
    {
        var name = element.LocalName.ToLowerInvariant();
        if (name is "script" or "style" or "head" or "title" or "meta" or "link" or "template")
        {
            return;
        }

        var style = SafeStyle(element);
        var textStyle = style is null ? parentStyle : ReadTextStyle(style);

        foreach (var child in element.ChildNodes)
        {
            switch (child)
            {
                case IText textNode:
                    AddTextRun(textNode.Data, textStyle, buffer);
                    break;
                case IElement childElement:
                    if (GetDisplay(SafeStyle(childElement)) == "none")
                    {
                        continue;
                    }

                    CollectInline(childElement, textStyle, buffer);
                    break;
            }
        }
    }

    private static float FlowInlineContent(LayoutBox box, List<InlineBox> runs, float x, float y, float maxWidth)
    {
        var words = new List<InlineBox>();
        foreach (var run in runs)
        {
            foreach (var part in run.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                words.Add(new InlineBox
                {
                    Text = part,
                    Color = run.Color,
                    FontSize = run.FontSize,
                    FontFamily = run.FontFamily,
                    Bold = run.Bold,
                    Italic = run.Italic
                });
            }
        }

        if (words.Count == 0)
        {
            return y;
        }

        var lineHeight = words.Max(w => w.FontSize) * LineHeightFactor;
        var cursorY = y;
        var line = new LineBox { X = x, Y = cursorY, Height = lineHeight };
        box.Lines.Add(line);

        var cursorX = x;
        var first = true;

        foreach (var word in words)
        {
            var wordWidth = ApproximateTextWidth(word.Text, word.FontSize);
            var spaceWidth = word.FontSize * 0.3f;
            var advance = (first ? 0f : spaceWidth) + wordWidth;

            if (!first && cursorX + advance > x + maxWidth)
            {
                line.Width = cursorX - x;
                cursorY += lineHeight;
                line = new LineBox { X = x, Y = cursorY, Height = lineHeight };
                box.Lines.Add(line);
                cursorX = x;
                first = true;
                advance = wordWidth;
            }

            if (!first)
            {
                cursorX += spaceWidth;
            }

            var placed = new InlineBox
            {
                Text = word.Text,
                X = cursorX,
                Width = wordWidth,
                Color = word.Color,
                FontSize = word.FontSize,
                FontFamily = word.FontFamily,
                Bold = word.Bold,
                Italic = word.Italic
            };
            line.Runs.Add(placed);
            cursorX += wordWidth;
            first = false;
        }

        line.Width = cursorX - x;
        return cursorY + lineHeight;
    }

    private static void AddTextRun(string raw, TextStyle style, List<InlineBox> buffer)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        var normalized = CollapseWhitespace(raw);
        if (normalized.Length == 0)
        {
            return;
        }

        buffer.Add(new InlineBox
        {
            Text = normalized,
            Color = style.Color,
            FontSize = style.FontSize,
            FontFamily = style.FontFamily,
            Bold = style.Bold,
            Italic = style.Italic
        });
    }

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                builder.Append(ch);
                lastWasSpace = false;
            }
        }

        return builder.ToString();
    }

    private static float ApproximateTextWidth(string text, float fontSize)
    {
        var width = 0f;
        foreach (var ch in text)
        {
            if (ch > 0x2E80)
            {
                width += fontSize; // CJK 등 전각 문자
            }
            else if (ch == ' ')
            {
                width += fontSize * 0.3f;
            }
            else
            {
                width += fontSize * 0.5f;
            }
        }

        return width;
    }

    private static bool IsBlock(IElement element, string display)
    {
        if (!string.IsNullOrEmpty(display))
        {
            if (display.StartsWith("inline", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        return BlockTags.Contains(element.LocalName);
    }

    private static string GetDisplay(ICssStyleDeclaration? style)
        => style?.GetPropertyValue("display").Trim().ToLowerInvariant() ?? string.Empty;

    private static ICssStyleDeclaration? SafeStyle(IElement element)
    {
        try
        {
            return element.ComputeCurrentStyle();
        }
        catch
        {
            return null;
        }
    }

    private static TextStyle ReadTextStyle(ICssStyleDeclaration? style)
    {
        var result = new TextStyle
        {
            FontSize = DefaultFontSize,
            FontFamily = DefaultFontFamily,
            Color = new Color4(0f, 0f, 0f, 1f)
        };

        if (style is null)
        {
            return result;
        }

        var fontSize = ParseLength(style.GetPropertyValue("font-size"));
        result.FontSize = fontSize > 0f ? fontSize : DefaultFontSize;

        var family = style.GetPropertyValue("font-family");
        if (!string.IsNullOrWhiteSpace(family))
        {
            result.FontFamily = FirstFontFamily(family);
        }

        if (CssColor.TryParse(style.GetPropertyValue("color"), out var color))
        {
            result.Color = color;
        }

        var weight = style.GetPropertyValue("font-weight").Trim().ToLowerInvariant();
        result.Bold = weight == "bold" || weight == "bolder"
            || (int.TryParse(weight, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) && numeric >= 600);

        var fontStyle = style.GetPropertyValue("font-style").Trim().ToLowerInvariant();
        result.Italic = fontStyle is "italic" or "oblique";

        return result;
    }

    private static string FirstFontFamily(string value)
    {
        var first = value.Split(',')[0].Trim().Trim('"', '\'').Trim();
        return string.IsNullOrEmpty(first) ? DefaultFontFamily : first;
    }

    private static Color4? ReadBackground(ICssStyleDeclaration? style)
    {
        if (style is null)
        {
            return null;
        }

        if (CssColor.TryParse(style.GetPropertyValue("background-color"), out var color) && color.A > 0f)
        {
            return color;
        }

        return null;
    }

    private static float ParseLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0f;
        }

        value = value.Trim();
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^2];
        }
        else if (value.EndsWith('%') || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return 0f;
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0f;
    }

    private struct TextStyle
    {
        public float FontSize;
        public string FontFamily;
        public Color4 Color;
        public bool Bold;
        public bool Italic;
    }
}
