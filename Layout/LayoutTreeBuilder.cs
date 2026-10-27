using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Vortice.Mathematics;

namespace CS2Browser.Layout;

/// <summary>
/// DOM 트리와 계산된 스타일(ComputeStyle)을 바탕으로 레이아웃 트리를 만든다.
/// 요소의 display(block / inline / flex / img)에 따라 <see cref="LayoutHandlerFactory"/> 가
/// 적절한 <see cref="ILayoutHandler"/> 를 선택해 디스패치하며,
/// 인라인 텍스트는 문서 전체 공유 버퍼의 오프셋을 기준으로 흐른다.
/// </summary>
public sealed class LayoutTreeBuilder
{
    internal const float DefaultFontSize = 16f;
    private const string DefaultFontFamily = "Malgun Gothic";
    internal const float LineHeightFactor = 1.4f;

    /// <summary>현재 레이아웃 중인 문서의 기준 URL(상대 경로 해석용).</summary>
    private string _baseUrl = string.Empty;

    /// <summary>문서 전체의 인라인 텍스트 공유 버퍼. 모든 런의 오프셋이 이 버퍼 기준이다.</summary>
    private readonly StringBuilder _textBuffer = new();

    /// <summary>
    /// 텍스트 폭 측정기. 지정하지 않으면 근사치를 사용한다.
    /// 실제 렌더러(DirectWrite)를 지정하면 렌더링과 동일한 메트릭으로 배치한다.
    /// </summary>
    public ITextMeasurer? TextMeasurer { get; set; }

    /// <summary>상대 URL 해석에 쓰는 문서 기준 URL.</summary>
    internal string BaseUrl => _baseUrl;

    /// <summary>인라인 런들이 오프셋으로 참조하는 문서 전체 공유 버퍼.</summary>
    internal StringBuilder TextBuffer => _textBuffer;

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
            var bottom = LayoutElement(body, root, 0f, 0f, viewportWidth, null);
            root.Height = bottom;
        }

        root.TextBuffer = _textBuffer.ToString();

        // 디버깅: 레이아웃 트리 구조 출력
        Debug.WriteLine("=== LAYOUT TREE DEBUG START ===");
        PrintLayoutBoxDebug(root, 0);
        Debug.WriteLine("=== LAYOUT TREE DEBUG END ===");

        return root;
    }

    private void PrintLayoutBoxDebug(LayoutBox box, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 2);
        Debug.WriteLine($"{indent}[{box.TagName}] Pos: ({box.X:F1}, {box.Y:F1}), Size: {box.Width:F1}x{box.Height:F1}, Margin: [{box.MarginTop:F1},{box.MarginRight:F1},{box.MarginBottom:F1},{box.MarginLeft:F1}], Padding: [{box.PaddingTop:F1},{box.PaddingRight:F1},{box.PaddingBottom:F1},{box.PaddingLeft:F1}]");
        
        foreach (var child in box.Children)
        {
            PrintLayoutBoxDebug(child, indentLevel + 1);
        }
    }

    /// <summary>
    /// 요소의 display/타입에 맞는 레이아웃 핸들러를 팩토리에서 획득해 실행한다.
    /// </summary>
    internal float LayoutElement(
        IElement element,
        LayoutBox parent,
        float left,
        float top,
        float availableWidth,
        LinkTarget? inheritedLink)
    {
        var style = SafeStyle(element);
        var display = GetDisplay(style);
        if (display == "none")
        {
            return top;
        }

        var link = GetLinkTarget(element) ?? inheritedLink;
        var handler = LayoutHandlerFactory.GetHandler(element, display);

        var resultY = handler.Layout(element, style, parent, left, top, availableWidth, link, this);
        Debug.WriteLine($"[LayoutElement] Element: <{element.LocalName}>, Display: {display}, Calculated Bottom: {resultY:F1}");
        return resultY;
    }

    /// <summary>
    /// 테두리/여백/패딩을 적용한 내용(content) 박스를 만들어 부모에 추가한다.
    /// 모든 박스 핸들러가 공통으로 사용하며, 내용 영역 좌상단과 폭을 반환한다.
    /// </summary>
    internal LayoutBox CreateContentBox(
        IElement element,
        ICssStyleDeclaration? style,
        LayoutBox parent,
        float left,
        float top,
        float availableWidth,
        LinkTarget? link,
        out float contentLeft,
        out float contentTop,
        out float innerWidth)
    {
        var edges = ReadEdges(style);

        contentLeft = left + edges.MarginLeft + edges.BorderLeft + edges.PaddingLeft;
        contentTop = top + edges.MarginTop + edges.BorderTop + edges.PaddingTop;
        innerWidth = MathF.Max(
            0f,
            availableWidth
            - edges.MarginLeft - edges.MarginRight
            - edges.BorderLeft - edges.BorderRight
            - edges.PaddingLeft - edges.PaddingRight);

        var box = new LayoutBox
        {
            TagName = element.LocalName,
            BackgroundColor = ReadBackground(style),
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
            PaddingLeft = edges.PaddingLeft
        };
        box.X = contentLeft;
        box.Y = contentTop;
        box.Width = innerWidth;
        parent.Children.Add(box);

        return box;
    }

    /// <summary>텍스트 폭을 측정한다(측정기가 있으면 정확히, 없으면 근사).</summary>
    internal float MeasureWidth(ReadOnlySpan<char> text, float fontSize, string fontFamily, bool bold, bool italic)
    {
        if (TextMeasurer is not null)
        {
            return TextMeasurer.MeasureWidth(text.ToString(), fontSize, fontFamily, bold, italic);
        }

        return ApproximateTextWidth(text, fontSize);
    }

    internal float MeasureWidth(string text, TextStyle style)
        => MeasureWidth(text, style.FontSize, style.FontFamily, style.Bold, style.Italic);

    internal static void CollectText(INode node, StringBuilder builder)
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

    /// <summary>대체 요소(replaced element)인 &lt;img&gt; 인지 확인한다.</summary>
    internal static bool IsImageElement(IElement element)
        => string.Equals(element.LocalName, "img", StringComparison.OrdinalIgnoreCase);

    /// <summary>이 요소의 자손 중 대체 요소(&lt;img&gt;)가 있는지 확인한다.</summary>
    internal static bool ContainsImage(IElement element)
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

    /// <summary>
    /// 요소가 &lt;a href&gt; 이면 기준 URL로 해석한 <see cref="LinkTarget"/> 을 반환한다. 아니면 null.
    /// 앵커마다 새 인스턴스를 만들어 같은 href 의 다른 링크와 구분한다.
    /// </summary>
    internal LinkTarget? GetLinkTarget(IElement element)
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

    /// <summary>상대 URL 을 문서 기준 URL 로 해석한다.</summary>
    internal static string ResolveUrl(string baseUrl, string? source)
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

    /// <summary>
    /// 글자 단위 줄바꿈이 가능한 CJK(한글/한자/가나) 문자인지 확인한다.
    /// 한글 음절은 물론 자모/호환 자모, 한자, 가나, 전각 문자를 포함한다.
    /// </summary>
    internal static bool IsCjkChar(char ch)
        => (ch >= '\u1100' && ch <= '\u11FF')   // 한글 자모
        || (ch >= '\u2E80' && ch <= '\u303F')   // CJK 부수/기호/구두점
        || (ch >= '\u3040' && ch <= '\u30FF')   // 히라가나/가타카나
        || (ch >= '\u3130' && ch <= '\u318F')   // 한글 호환 자모
        || (ch >= '\u3400' && ch <= '\u4DBF')   // CJK 확장 A
        || (ch >= '\u4E00' && ch <= '\u9FFF')   // CJK 통합 한자
        || (ch >= '\uAC00' && ch <= '\uD7A3')   // 한글 음절
        || (ch >= '\uF900' && ch <= '\uFAFF')   // CJK 호환 한자
        || (ch >= '\uFF00' && ch <= '\uFFEF');  // 전각/반각 형태

    internal static string GetDisplay(ICssStyleDeclaration? style)
        => style?.GetPropertyValue("display").Trim().ToLowerInvariant() ?? string.Empty;


    internal static ICssStyleDeclaration? SafeStyle(IElement? element)
    {
        if (element == null)
        {
            return null;
        }

        // 1. AngleSharp.Css 내부 연산 오류를 유발하는 것으로 확인된 클래스나 패턴 사전 차단
        string className = element.ClassName ?? string.Empty;
        if (className.Contains("logo_homepage", StringComparison.OrdinalIgnoreCase))
        {
            // 해당 엘리먼트는 계산을 생략하고 null 반환 (필요시 기본 레이아웃 스타일 적용)
            return null;
        }

        // 2. 소속 문서 및 컨텍스트 유효성 검사
        if (element.Owner?.Context == null)
        {
            return null;
        }

        try
        {
            // 3. 스타일시트나 인라인 스타일이 없는 경우 생략
            if (element.Owner.StyleSheets.Length == 0 && string.IsNullOrEmpty(element.GetAttribute("style")))
            {
                return null;
            }

            return element.ComputeCurrentStyle();
        }
        catch (Exception ex)
        {
            // 혹시 모를 다른 예외 대비 이중 방어
            Debug.WriteLine($"[SafeStyle ERROR] <{element.TagName} class='{className}'>: {ex.Message}");
            return null;
        }
    }

    internal static TextStyle ReadTextStyle(ICssStyleDeclaration? style)
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

    internal static Color4? ReadBackground(ICssStyleDeclaration? style)
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

    internal static float ParseLength(string? value)
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

    /// <summary>박스의 여백/테두리/패딩 두께(px).</summary>
    internal readonly struct BoxEdges
    {
        public float MarginTop { get; init; }
        public float MarginRight { get; init; }
        public float MarginBottom { get; init; }
        public float MarginLeft { get; init; }
        public float BorderTop { get; init; }
        public float BorderRight { get; init; }
        public float BorderBottom { get; init; }
        public float BorderLeft { get; init; }
        public float PaddingTop { get; init; }
        public float PaddingRight { get; init; }
        public float PaddingBottom { get; init; }
        public float PaddingLeft { get; init; }
    }

    /// <summary>스타일에서 여백/테두리/패딩 두께를 한 번에 읽는다.</summary>
    internal static BoxEdges ReadEdges(ICssStyleDeclaration? style) => new()
    {
        MarginTop = ParseLength(style?.GetPropertyValue("margin-top")),
        MarginRight = ParseLength(style?.GetPropertyValue("margin-right")),
        MarginBottom = ParseLength(style?.GetPropertyValue("margin-bottom")),
        MarginLeft = ParseLength(style?.GetPropertyValue("margin-left")),
        BorderTop = ParseLength(style?.GetPropertyValue("border-top-width")),
        BorderRight = ParseLength(style?.GetPropertyValue("border-right-width")),
        BorderBottom = ParseLength(style?.GetPropertyValue("border-bottom-width")),
        BorderLeft = ParseLength(style?.GetPropertyValue("border-left-width")),
        PaddingTop = ParseLength(style?.GetPropertyValue("padding-top")),
        PaddingRight = ParseLength(style?.GetPropertyValue("padding-right")),
        PaddingBottom = ParseLength(style?.GetPropertyValue("padding-bottom")),
        PaddingLeft = ParseLength(style?.GetPropertyValue("padding-left"))
    };

    internal struct TextStyle
    {
        public float FontSize;
        public string FontFamily;
        public Color4 Color;
        public bool Bold;
        public bool Italic;
    }

    /// <summary>
    /// 공유 텍스트 버퍼 내의 오프셋 구간과 스타일을 나타낸다.
    /// 버퍼 자체는 <see cref="LayoutTreeBuilder"/> 가 소유하며, 런은 오프셋만 참조한다.
    /// </summary>
    internal struct TextRunInfo
    {
        public int Start;
        public int Length;
        public TextStyle Style;
        public LinkTarget? Link;
    }
}
