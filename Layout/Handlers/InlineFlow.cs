using System.Diagnostics;
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

    // 마진 병합(Margin Collapsing) 상태 추적용 필드
    private bool _isFirstBoxChild = true;
    private float _previousMarginBottom = 0f;

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

        var style = LayoutTreeBuilder.SafeStyle(element);
        var display = LayoutTreeBuilder.GetDisplay(style);
        if (display == "none")
        {
            return;
        }

        if (name is "input" or "textarea")
        {
            LayoutInlineBoxChild(element, link);
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

    /// <summary>
    /// 인라인 흐름 안에서 atomic inline 박스(예: input, textarea, inline-block)를 가로 선상에 배치한다.
    /// </summary>
    public void LayoutInlineBoxChild(IElement element, LinkTarget? link)
    {
        // 먼저 쌓여있는 인라인 텍스트 런들을 같은 줄로 배치
        Flush();

        // 현재 가장 아래쪽에 생성된 라인의 Y 위치 또는 기본 Y 위치 계산
        float curY = _y;
        float curX = _x;

        if (_box.Lines.Count > 0)
        {
            var lastLine = _box.Lines[^1];
            curX = lastLine.X + lastLine.Width;
            curY = lastLine.Y;
        }

        float availableWidth = MathF.Max(0f, _maxWidth - (curX - _x));

        // 공간이 부족하면 다음 줄로 넘김
        if (availableWidth < 30f && curX > _x)
        {
            curX = _x;
            curY = _y;
            availableWidth = _maxWidth;
        }

        float bottomY = _builder.LayoutElement(element, _box, curX, curY, availableWidth, link);

        if (_box.Children.Count > 0)
        {
            var childBox = _box.Children[^1];
            float totalChildWidth = childBox.Width + childBox.BorderLeft + childBox.BorderRight + childBox.PaddingLeft + childBox.PaddingRight + childBox.MarginLeft + childBox.MarginRight;
            float totalChildHeight = childBox.Height + childBox.BorderTop + childBox.BorderBottom + childBox.PaddingTop + childBox.PaddingBottom + childBox.MarginTop + childBox.MarginBottom;

            if (_box.Lines.Count > 0 && curX > _x)
            {
                var lastLine = _box.Lines[^1];
                lastLine.Width += totalChildWidth;
                lastLine.Height = MathF.Max(lastLine.Height, totalChildHeight);
            }
            else
            {
                var newNominalLine = new LineBox
                {
                    X = curX,
                    Y = curY,
                    Width = totalChildWidth,
                    Height = totalChildHeight
                };
                _box.Lines.Add(newNominalLine);
            }
        }

        _y = MathF.Max(_y, bottomY);
    }

    /// <summary>현재 줄을 끊고 박스 자식을 세로로 배치한다.</summary>
    public void LayoutBoxChild(IElement element, LinkTarget? link)
    {
        Flush();

        var style = LayoutTreeBuilder.SafeStyle(element);
        var edges = LayoutTreeBuilder.ReadEdges(style);

        float topArg;

        if (_isFirstBoxChild)
        {
            _isFirstBoxChild = false;

            // W3C 표준 마진 병합: 부모의 PaddingTop과 BorderTop이 0이면,
            // 첫 번째 자식의 상단 마진은 부모 바깥으로 완전히 병합됩니다.
            if (_box.PaddingTop == 0f && _box.BorderTop == 0f)
            {
                // [수정] 첫 번째 자식의 상단 마진이 부모 위로 완전히 빠져나가므로,
                // 레이아웃 시작 좌표(topArg)를 마진만큼 뺀 위치(_y)에서 시작하여 
                // 결과적으로 body의 시작점과 h1의 경계가 정확히 일치하도록 합니다.
                topArg = _y - edges.MarginTop;
                _previousMarginBottom = edges.MarginBottom;
            }
            else
            {
                topArg = _y;
                _previousMarginBottom = edges.MarginBottom;
            }
        }
        else
        {
            // 인접 형제 간 마진 병합
            float collapsedMargin = MathF.Max(_previousMarginBottom, edges.MarginTop);
            topArg = _y - _previousMarginBottom + collapsedMargin - edges.MarginTop;
            _previousMarginBottom = edges.MarginBottom;
        }

        _y = _builder.LayoutElement(element, _box, _x, topArg, _maxWidth, link);
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
        var hasContent = false;
        var hasAnyContent = false;
        var pendingBreaks = 0;
        var pendingSpace = false;
        var previousWasCjk = false;
        LineBox? line = null;

        if (_box.Lines.Count > 0)
        {
            line = _box.Lines[^1];
            cursorX = line.X + line.Width;
            lineTop = line.Y;
            lineHeight = MathF.Max(line.Height, lineHeight);
            if (line.Width > 0)
            {
                hasContent = true;
                hasAnyContent = true;
            }
        }

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
            line.Width = cursorX - line.X;
        }

        return bottom;
    }

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

    private static bool IsTokenBoundary(char ch)
        => ch == ' ' || ch == '\n' || LayoutTreeBuilder.IsCjkChar(ch);
}
