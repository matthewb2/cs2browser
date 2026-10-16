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
public sealed partial class LayoutTreeBuilder
{
    private const float DefaultFontSize = 16f;
    private const string DefaultFontFamily = "Malgun Gothic";
    private const float LineHeightFactor = 1.4f;

    /// <summary>현재 레이아웃 중인 문서의 기준 URL(상대 경로 해석용).</summary>
    private string _baseUrl = string.Empty;

    /// <summary>문서 전체의 인라인 텍스트 공유 버퍼. 모든 런의 오프셋이 이 버퍼 기준이다.</summary>
    private readonly StringBuilder _textBuffer = new();

    /// <summary>
    /// 텍스트 폭 측정기. 지정하지 않으면 근사치를 사용한다.
    /// 실제 렌더러(DirectWrite)를 지정하면 렌더링과 동일한 메트릭으로 배치한다.
    /// </summary>
    public ITextMeasurer? TextMeasurer { get; set; }

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "body", "div", "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li",
        "dl", "dt", "dd", "section", "header", "footer", "nav", "article", "main", "aside",
        "form", "table", "thead", "tbody", "tfoot", "tr", "blockquote", "pre", "hr",
        "figure", "figcaption", "fieldset", "address", "details", "summary"
    };

    public LayoutBox Build(IDocument document, float viewportWidth)
    {
        _baseUrl = document.Url ?? string.Empty;
        _textBuffer.Clear();

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

        root.TextBuffer = _textBuffer.ToString();
        return root;
    }

    private float LayoutBlock(IElement element, LayoutBox parent, float left, float top, float availableWidth, LinkTarget? inheritedLink = null)
    {
        var style = SafeStyle(element);
        var display = GetDisplay(style);
        if (display == "none")
        {
            return top;
        }

        var link = GetLinkTarget(element) ?? inheritedLink;

        if (IsImageElement(element))
        {
            return LayoutImage(element, style, parent, left, top, availableWidth, link);
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
            Link = link,
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
        var inlineBuffer = new List<LineFragment>();

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

                    if (IsBlock(childElement, childDisplay) || IsImageElement(childElement) || ContainsImage(childElement))
                    {
                        if (inlineBuffer.Count > 0)
                        {
                            cursorY = FlowInlineContent(box, inlineBuffer, _textBuffer, contentLeft, cursorY, innerWidth);
                            inlineBuffer.Clear();
                        }

                        cursorY = LayoutBlock(childElement, box, contentLeft, cursorY, innerWidth, link);
                    }
                    else
                    {
                        CollectInline(childElement, textStyle, inlineBuffer, _textBuffer, GetLinkTarget(childElement) ?? link);
                    }
                    break;

                case IText textNode:
                    AddTextRun(textNode.Data, textStyle, inlineBuffer, _textBuffer, link);
                    break;
            }
        }

        if (inlineBuffer.Count > 0)
        {
            cursorY = FlowInlineContent(box, inlineBuffer, _textBuffer, contentLeft, cursorY, innerWidth);
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

            var bottom = LayoutBlock(childElement, box, cursorX, contentTop, childWidth, box.Link);
            var childBox = box.Children[^1];

            cursorX = childBox.X + childBox.Width + childBox.PaddingRight + childBox.BorderRight + childBox.MarginRight;
            maxBottom = MathF.Max(maxBottom, bottom);
        }

        return maxBottom;
    }

    /// <summary>자신의 내용만으로 결정되는 축소(shrink-to-fit) 폭을 근사 계산한다.</summary>
    private float EstimateIntrinsicWidth(IElement element, ICssStyleDeclaration? style)
    {
        var horizontalEdges =
            ParseLength(style?.GetPropertyValue("margin-left")) + ParseLength(style?.GetPropertyValue("margin-right"))
            + ParseLength(style?.GetPropertyValue("border-left-width")) + ParseLength(style?.GetPropertyValue("border-right-width"))
            + ParseLength(style?.GetPropertyValue("padding-left")) + ParseLength(style?.GetPropertyValue("padding-right"));

        var textStyle = ReadTextStyle(style);
        var builder = new StringBuilder();
        CollectText(element, builder);
        var text = builder.ToString().Trim();
        var textWidth = MeasureWidth(text, textStyle);

        return horizontalEdges + textWidth + 1f;
    }

    /// <summary>텍스트 폭을 측정한다(측정기가 있으면 정확히, 없으면 근사).</summary>
    private float MeasureWidth(ReadOnlySpan<char> text, float fontSize, string fontFamily, bool bold, bool italic)
    {
        if (TextMeasurer is not null)
        {
            return TextMeasurer.MeasureWidth(text.ToString(), fontSize, fontFamily, bold, italic);
        }

        return ApproximateTextWidth(text, fontSize);
    }

    private float MeasureWidth(string text, TextStyle style)
        => MeasureWidth(text, style.FontSize, style.FontFamily, style.Bold, style.Italic);

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

    private static void CollectInline(IElement element, TextStyle parentStyle, List<LineFragment> fragments, StringBuilder buffer, LinkTarget? link = null)
    {
        var name = element.LocalName.ToLowerInvariant();
        if (name is "script" or "style" or "head" or "title" or "meta" or "link" or "template")
        {
            return;
        }

        var style = SafeStyle(element);
        var textStyle = style is null ? parentStyle : ReadTextStyle(style);

        // <br> 은 강제 줄바꿈이다.
        if (name == "br")
        {
            AppendFragment("\n", textStyle, fragments, buffer, link);
            return;
        }

        foreach (var child in element.ChildNodes)
        {
            switch (child)
            {
                case IText textNode:
                    AddTextRun(textNode.Data, textStyle, fragments, buffer, link);
                    break;
                case IElement childElement:
                    if (GetDisplay(SafeStyle(childElement)) == "none")
                    {
                        continue;
                    }

                    CollectInline(childElement, textStyle, fragments, buffer, link);
                    break;
            }
        }
    }

    /// <summary>
    /// 인라인 조각들을 오프셋 기준으로 흘려보내며 줄을 만든다.
    /// 공유 버퍼 안의 개행 문자('\n') 오프셋에서 강제 줄바꿈하고,
    /// 줄 폭을 넘으면 자동으로 줄바꿈한다.
    /// </summary>
    private float FlowInlineContent(
        LayoutBox box,
        List<LineFragment> fragments,
        StringBuilder textBuffer,
        float x,
        float y,
        float maxWidth)
    {
        if (fragments.Count == 0)
        {
            return y;
        }

        // 모든 조각이 참조할 공유 버퍼를 확정한다.
        var buffer = textBuffer.ToString();
        foreach (var fragment in fragments)
        {
            fragment.Buffer = buffer;
        }

        var lineHeight = LineHeightFactor * fragments
            .Where(f => f.Length > 0)
            .Select(f => f.FontSize)
            .DefaultIfEmpty(DefaultFontSize)
            .Max();

        var lineTop = y;
        var bottom = y;
        var cursorX = x;
        var hasContent = false;      // 현재 줄에 내용이 있는지
        var hasAnyContent = false;   // 블록에 내용이 하나라도 있는지
        var pendingBreaks = 0;       // 아직 반영되지 않은 강제 줄바꿈 수
        var pendingSpace = false;    // 공백이 있었는지(공백에서 줄바꿈 가능)
        var previousWasCjk = false;  // 직전 토큰이 CJK 글자였는지(글자 단위 줄바꿈)
        LineBox? line = null;

        void StartLine()
        {
            line = new LineBox { X = x, Y = lineTop, Height = lineHeight };
            box.Lines.Add(line);
            cursorX = x;
            hasContent = false;
            previousWasCjk = false;
        }

        foreach (var fragment in fragments)
        {
            var span = fragment.Span;
            var index = 0;
            while (index < span.Length)
            {
                var ch = span[index];
                if (ch == '\n')
                {
                    pendingBreaks++;
                    index++;
                    continue;
                }

                if (ch == ' ')
                {
                    pendingSpace = true;
                    index++;
                    continue;
                }

                // 토큰 추출: CJK/한글은 한 글자씩, 그 외는 단어 단위.
                var tokenStart = index;
                var isCjk = IsCjkChar(ch);
                if (isCjk)
                {
                    index++;
                }
                else
                {
                    while (index < span.Length && !IsTokenBoundary(span[index]))
                    {
                        index++;
                    }
                }

                var token = span.Slice(tokenStart, index - tokenStart);
                var tokenWidth = MeasureWidth(token, fragment.FontSize, fragment.FontFamily, fragment.Bold, fragment.Italic);

                // 대기 중인 강제 줄바꿈을 반영한다. 블록 선두의 줄바꿈은 무시한다.
                if (pendingBreaks > 0)
                {
                    if (hasAnyContent)
                    {
                        if (line is not null)
                        {
                            line.Width = cursorX - x;
                        }

                        lineTop += pendingBreaks * lineHeight;
                        line = null;
                        cursorX = x;
                        hasContent = false;
                    }

                    pendingBreaks = 0;
                    pendingSpace = false;
                }

                if (line is null)
                {
                    StartLine();
                }

                // 이 토큰 앞에서 줄을 바꿀 수 있는지:
                //  - CJK 글자이거나, 앞에 공백이 있었거나,
                //  - 직전 토큰이 CJK 글자였으면 (한글은 글자 단위 줄바꿈)
                var canBreakBefore = isCjk || pendingSpace || previousWasCjk;
                var spaceWidth = pendingSpace
                    ? MeasureWidth(" ", fragment.FontSize, fragment.FontFamily, fragment.Bold, fragment.Italic)
                    : 0f;

                if (hasContent && canBreakBefore && cursorX + spaceWidth + tokenWidth > x + maxWidth)
                {
                    line!.Width = cursorX - x;
                    lineTop += lineHeight;
                    StartLine();
                    spaceWidth = 0f;
                }

                if (hasContent && spaceWidth > 0f)
                {
                    cursorX += spaceWidth;
                }

                line!.Runs.Add(new InlineBox
                {
                    Text = token.ToString(),
                    TextStart = fragment.Start + tokenStart,
                    TextLength = token.Length,
                    X = cursorX,
                    Width = tokenWidth,
                    Color = fragment.Color,
                    FontSize = fragment.FontSize,
                    FontFamily = fragment.FontFamily,
                    Bold = fragment.Bold,
                    Italic = fragment.Italic,
                    Link = fragment.Link
                });

                cursorX += tokenWidth;
                hasContent = true;
                hasAnyContent = true;
                previousWasCjk = isCjk;
                pendingSpace = false;
                bottom = MathF.Max(bottom, lineTop + lineHeight);
            }
        }

        if (line is not null)
        {
            line.Width = cursorX - x;
        }

        return bottom;
    }

    private static void AddTextRun(string raw, TextStyle style, List<LineFragment> fragments, StringBuilder buffer, LinkTarget? link = null)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        var normalized = NormalizeInlineWhitespace(raw);
        if (normalized.Length == 0)
        {
            return;
        }

        AppendFragment(normalized, style, fragments, buffer, link);
    }

    /// <summary>공유 버퍼 뒤에 텍스트를 추가하고 그 구간을 참조하는 조각을 만든다.</summary>
    private static void AppendFragment(string text, TextStyle style, List<LineFragment> fragments, StringBuilder buffer, LinkTarget? link = null)
    {
        var start = buffer.Length;
        buffer.Append(text);

        fragments.Add(new LineFragment
        {
            Start = start,
            Length = text.Length,
            Color = style.Color,
            FontSize = style.FontSize,
            FontFamily = style.FontFamily,
            Bold = style.Bold,
            Italic = style.Italic,
            Link = link
        });
    }

    /// <summary>
    /// 연속된 공백/탭은 하나의 공백으로 축약하되, 개행(\r\n, \r, \n)은
    /// 줄바꿈 기준('\n')으로 보존한다.
    /// </summary>
    private static string NormalizeInlineWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            switch (ch)
            {
                case '\r':
                    if (i + 1 < value.Length && value[i + 1] == '\n')
                    {
                        i++;
                    }

                    builder.Append('\n');
                    lastWasSpace = false;
                    break;
                case '\n':
                    builder.Append('\n');
                    lastWasSpace = false;
                    break;
                case ' ' or '\t' or '\f' or '\v':
                    if (!lastWasSpace)
                    {
                        builder.Append(' ');
                        lastWasSpace = true;
                    }

                    break;
                default:
                    builder.Append(ch);
                    lastWasSpace = false;
                    break;
            }
        }

        return builder.ToString();
    }

    private static float ApproximateTextWidth(ReadOnlySpan<char> text, float fontSize)
    {
        var width = 0f;
        foreach (var ch in text)
        {
            if (IsCjkChar(ch))
            {
                width += fontSize; // CJK/한글 등 전각 문자
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

    /// <summary>단어 토큰의 경계가 되는 문자(공백/개행/CJK 글자)인지 확인한다.</summary>
    private static bool IsTokenBoundary(char ch)
        => ch == ' ' || ch == '\n' || IsCjkChar(ch);

    /// <summary>
    /// 글자 단위 줄바꿈이 가능한 CJK(한글/한자/가나) 문자인지 확인한다.
    /// 한글 음절은 물론 자모/호환 자모, 한자, 가나, 전각 문자를 포함한다.
    /// </summary>
    private static bool IsCjkChar(char ch)
        => (ch >= '\u1100' && ch <= '\u11FF')   // 한글 자모
        || (ch >= '\u2E80' && ch <= '\u303F')   // CJK 부수/기호/구두점
        || (ch >= '\u3040' && ch <= '\u30FF')   // 히라가나/가타카나
        || (ch >= '\u3130' && ch <= '\u318F')   // 한글 호환 자모
        || (ch >= '\u3400' && ch <= '\u4DBF')   // CJK 확장 A
        || (ch >= '\u4E00' && ch <= '\u9FFF')   // CJK 통합 한자
        || (ch >= '\uAC00' && ch <= '\uD7A3')   // 한글 음절
        || (ch >= '\uF900' && ch <= '\uFAFF')   // CJK 호환 한자
        || (ch >= '\uFF00' && ch <= '\uFFEF');  // 전각/반각 형태

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
