using System.Text;
using AngleSharp.Dom;

namespace CS2Browser.Layout;

/// <summary>
/// 하나의 인라인 포매팅 컨텍스트.
/// 부모 박스 안에서 연속된 텍스트/인라인 요소를 공유 버퍼 오프셋 기준으로 흘려보내 줄을 만들고,
/// 도중에 만난 박스(블록/이미지/flex) 자식은 현재 줄을 끊고 세로로 배치한다.
/// </summary>
internal sealed class InlineFlow
{
    private readonly LayoutTreeBuilder _builder;
    private readonly LayoutBox _box;
    private readonly List<LayoutTreeBuilder.TextRunInfo> _runs = new();
    private readonly float _x;
    private readonly float _maxWidth;
    private float _y;

    public InlineFlow(LayoutTreeBuilder builder, LayoutBox box, float x, float y, float maxWidth)
    {
        _builder = builder;
        _box = box;
        _x = x;
        _y = y;
        _maxWidth = maxWidth;
    }

    /// <summary>텍스트를 공유 버퍼에 추가하고 인라인 런으로 예약한다.</summary>
    public void AppendText(string raw, LayoutTreeBuilder.TextStyle style, LinkTarget? link)
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

        var buffer = _builder.TextBuffer;
        var start = buffer.Length;
        buffer.Append(normalized);

        _runs.Add(new LayoutTreeBuilder.TextRunInfo
        {
            Start = start,
            Length = normalized.Length,
            Style = style,
            Link = link
        });
    }

    /// <summary>
    /// 인라인 요소를 흐름에 이어 붙인다.
    /// 순수 인라인은 같은 줄로 재귀하고, 박스를 만드는 자손은 현재 줄을 끊어 배치한다.
    /// </summary>
    public void AppendInlineElement(IElement element, LayoutTreeBuilder.TextStyle parentStyle, LinkTarget? link = null)
    {
        var name = element.LocalName.ToLowerInvariant();
        if (name is "script" or "style" or "head" or "title" or "meta" or "link" or "template")
        {
            return;
        }

        if (name is "input" or "textarea")
        {
            LayoutBoxChild(element, link);
            return;
        }

        var style = LayoutTreeBuilder.SafeStyle(element);
        var display = LayoutTreeBuilder.GetDisplay(style);
        if (display == "none")
        {
            return;
        }

        var textStyle = style is null ? parentStyle : LayoutTreeBuilder.ReadTextStyle(style);
        var childLink = _builder.GetLinkTarget(element) ?? link;

        // <br> 은 강제 줄바꿈이다.
        if (name == "br")
        {
            AppendText("\n", textStyle, childLink);
            return;
        }

        if (LayoutHandlerFactory.IsInlineChild(element, display))
        {
            foreach (var child in element.ChildNodes)
            {
                switch (child)
                {
                    case IText textNode:
                        AppendText(textNode.Data, textStyle, childLink);
                        break;
                    case IElement childElement:
                        AppendInlineElement(childElement, textStyle, childLink);
                        break;
                }
            }
        }
        else
        {
            LayoutBoxChild(element, childLink);
        }
    }

    /// <summary>현재 줄을 끊고 박스 자식을 세로로 배치한다.</summary>
    public void LayoutBoxChild(IElement element, LinkTarget? link)
    {
        Flush();
        _y = _builder.LayoutElement(element, _box, _x, _y, _maxWidth, link);
    }

    /// <summary>예약된 인라인 런을 실제 줄로 배치하고, 다음 콘텐츠의 Y 좌표를 반환한다.</summary>
    public float Flush()
    {
        if (_runs.Count == 0)
        {
            return _y;
        }

        _y = Flow(_runs);
        _runs.Clear();
        return _y;
    }

    /// <summary>
    /// 인라인 런들을 오프셋 기준으로 순회하며 줄바꿈 및 배치를 수행한다.
    /// 공유 버퍼 안의 개행 문자('\n') 오프셋에서 강제 줄바꿈하고,
    /// 줄 폭을 넘으면 자동으로 줄바꿈한다.
    /// </summary>
    private float Flow(List<LayoutTreeBuilder.TextRunInfo> runs)
    {
        // 공유 버퍼의 현재 스냅샷. 각 런의 [Start, Start+Length) 오프셋이 이 버퍼를 가리킨다.
        var buffer = _builder.TextBuffer.ToString();

        var lineHeight = LayoutTreeBuilder.LineHeightFactor * runs
            .Where(r => r.Length > 0)
            .Select(r => r.Style.FontSize)
            .DefaultIfEmpty(LayoutTreeBuilder.DefaultFontSize)
            .Max();

        var x = _x;
        var maxWidth = _maxWidth;
        var lineTop = _y;
        var bottom = _y;
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
            _box.Lines.Add(line);
            cursorX = x;
            hasContent = false;
            previousWasCjk = false;
        }

        foreach (var run in runs)
        {
            var runEnd = run.Start + run.Length;
            var index = run.Start;
            var style = run.Style;

            while (index < runEnd && index < buffer.Length)
            {
                var ch = buffer[index];
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
                var isCjk = LayoutTreeBuilder.IsCjkChar(ch);
                if (isCjk)
                {
                    index++;
                }
                else
                {
                    while (index < runEnd && index < buffer.Length && !IsTokenBoundary(buffer[index]))
                    {
                        index++;
                    }
                }

                var token = buffer.AsSpan(tokenStart, index - tokenStart);
                var tokenWidth = _builder.MeasureWidth(token, style.FontSize, style.FontFamily, style.Bold, style.Italic);

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
                    ? _builder.MeasureWidth(" ", style.FontSize, style.FontFamily, style.Bold, style.Italic)
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
                    TextStart = tokenStart,
                    TextLength = token.Length,
                    X = cursorX,
                    Width = tokenWidth,
                    Color = style.Color,
                    FontSize = style.FontSize,
                    FontFamily = style.FontFamily,
                    Bold = style.Bold,
                    Italic = style.Italic,
                    Link = run.Link
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

    /// <summary>단어 토큰의 경계가 되는 문자(공백/개행/CJK 글자)인지 확인한다.</summary>
    private static bool IsTokenBoundary(char ch)
        => ch == ' ' || ch == '\n' || LayoutTreeBuilder.IsCjkChar(ch);
}
