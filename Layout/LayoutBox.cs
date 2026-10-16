using System.Text;
using Vortice.Mathematics;

namespace CS2Browser.Layout;

/// <summary>
/// 레이아웃 박스. 화면에 그려질 요소의 기하학적 위치/크기와
/// 여백(Margin), 테두리(Border), 안쪽 여백(Padding), 배경색을 담는다.
/// X, Y, Width, Height 는 내용(Content) 박스를 나타낸다.
/// </summary>
public sealed class LayoutBox
{
    public string? TagName { get; set; }

    public LayoutBox? Parent { get; set; }

    public List<LayoutBox> Children { get; } = new();

    /// <summary>이 박스 안에 직접 흐르는 텍스트 줄들.</summary>
    public List<LineBox> Lines { get; } = new();

    // Content box
    public float X;
    public float Y;
    public float Width;
    public float Height;

    // Edges (px)
    public float MarginTop;
    public float MarginRight;
    public float MarginBottom;
    public float MarginLeft;
    public float BorderTop;
    public float BorderRight;
    public float BorderBottom;
    public float BorderLeft;
    public float PaddingTop;
    public float PaddingRight;
    public float PaddingBottom;
    public float PaddingLeft;

    public Color4? BackgroundColor { get; set; }

    /// <summary>이 박스가 나타내는 링크(&lt;a href&gt;). null 이면 일반 박스.</summary>
    public LinkTarget? Link { get; set; }

    /// <summary>링크의 절대 URL(편의용).</summary>
    public string? LinkUrl => Link?.Url;

    /// <summary>렌더링할 이미지의 소스(절대 URL 또는 로컬 경로). null 이면 일반 박스.</summary>
    public string? ImageSource { get; set; }

    /// <summary>
    /// 문서 전체의 인라인 텍스트 공유 버퍼(루트 박스에만 설정된다).
    /// InlineBox.TextStart/TextLength 는 이 버퍼의 오프셋이다.
    /// </summary>
    public string? TextBuffer { get; set; }

    /// <summary>배경을 채울 때 사용하는 패딩 박스(내용 + 패딩).</summary>
    public float PaddingBoxLeft => X - PaddingLeft;
    public float PaddingBoxTop => Y - PaddingTop;
    public float PaddingBoxWidth => Width + PaddingLeft + PaddingRight;
    public float PaddingBoxHeight => Height + PaddingTop + PaddingBottom;

    /// <summary>전체 여백 박스의 아래쪽 Y 좌표.</summary>
    public float MarginBoxBottom => Y + Height + PaddingBottom + BorderBottom + MarginBottom;
}

/// <summary>한 줄의 텍스트 정보. InlineBox 들을 담는다.</summary>
public sealed class LineBox
{
    public float X;
    public float Y;
    public float Width;
    public float Height;

    public List<InlineBox> Runs { get; } = new();
}

/// <summary>줄 안에 배치된 텍스트 조각(스타일 포함).</summary>
public sealed class InlineBox
{
    public string Text { get; set; } = string.Empty;

    public float X;
    public float Width;

    /// <summary>이 런의 텍스트가 <see cref="LayoutBox.TextBuffer"/> 에서 시작하는 오프셋.</summary>
    public int TextStart;

    /// <summary>이 런의 텍스트 길이(버퍼 오프셋 기준).</summary>
    public int TextLength;

    /// <summary>이 런이 참조하는 버퍼 구간의 끝 오프셋(배타).</summary>
    public int TextEnd => TextStart + TextLength;

    public Color4 Color { get; set; } = new(0f, 0f, 0f, 1f);
    public float FontSize { get; set; } = 16f;
    public string FontFamily { get; set; } = "Malgun Gothic";
    public bool Bold { get; set; }
    public bool Italic { get; set; }

    /// <summary>이 텍스트 조각이 속한 링크(&lt;a href&gt;). null 이면 링크 아님.</summary>
    public LinkTarget? Link { get; set; }

    /// <summary>링크의 절대 URL(편의용).</summary>
    public string? LinkUrl => Link?.Url;
}

/// <summary>
/// 인라인 텍스트 조각. 인라인 콘텐츠 전체를 담는 공유 버퍼(<see cref="Buffer"/>)에서
/// [<see cref="Start"/>, <see cref="End"/>) 오프셋 구간을 참조하며 텍스트 스타일을 함께 가진다.
/// 줄바꿈은 이 구간 안의 개행 문자('\n') 오프셋을 기준으로 판정된다.
/// </summary>
public sealed class LineFragment
{
    /// <summary>모든 조각이 공유하는 인라인 텍스트 버퍼.</summary>
    public string Buffer { get; set; } = string.Empty;

    /// <summary>버퍼 내 시작 오프셋.</summary>
    public int Start { get; set; }

    /// <summary>버퍼 내 길이.</summary>
    public int Length { get; set; }

    /// <summary>버퍼 내 끝 오프셋(배타).</summary>
    public int End => Start + Length;

    /// <summary>이 조각이 참조하는 버퍼 구간.</summary>
    public ReadOnlySpan<char> Span =>
        Start >= 0 && Start + Length <= Buffer.Length
            ? Buffer.AsSpan(Start, Length)
            : ReadOnlySpan<char>.Empty;

    /// <summary>이 조각의 텍스트.</summary>
    public string Text => Span.ToString();

    public Color4 Color { get; set; } = new(0f, 0f, 0f, 1f);
    public float FontSize { get; set; } = 16f;
    public string FontFamily { get; set; } = "Malgun Gothic";
    public bool Bold { get; set; }
    public bool Italic { get; set; }

    /// <summary>이 조각이 속한 링크(&lt;a href&gt;). null 이면 링크 아님.</summary>
    public LinkTarget? Link { get; set; }

    /// <summary>링크의 절대 URL(편의용).</summary>
    public string? LinkUrl => Link?.Url;
}

/// <summary>
/// 하나의 &lt;a href&gt; 앵커를 나타낸다. 앵커의 모든 조각/박스가 동일 인스턴스를 공유하므로
/// 참조 비교로 "같은 링크"를 구분할 수 있다(같은 URL의 다른 링크와도 구별됨).
/// </summary>
public sealed class LinkTarget
{
    public LinkTarget(string url) => Url = url;

    /// <summary>해석된 절대 URL.</summary>
    public string Url { get; }
}

/// <summary>
/// 마우스 드래그로 선택된 텍스트 범위.
/// 두 끝점을 <see cref="LayoutBox.TextBuffer"/> 의 오프셋(앵커/포커스)으로 저장한다.
/// 드래그 방향과 무시하게 정규화된 실제 범위는 <see cref="Start"/>/<see cref="End"/> 다.
/// </summary>
public sealed class TextSelection
{
    public TextSelection(int anchor, int focus)
    {
        Anchor = anchor;
        Focus = focus;
    }

    /// <summary>드래그가 시작된 지점의 버퍼 오프셋.</summary>
    public int Anchor { get; }

    /// <summary>드래그 중인 현재 지점의 버퍼 오프셋.</summary>
    public int Focus { get; set; }

    /// <summary>정규화된 선택 시작 오프셋(작은 쪽).</summary>
    public int Start => Math.Min(Anchor, Focus);

    /// <summary>정규화된 선택 끝 오프셋(큰 쪽, 배타).</summary>
    public int End => Math.Max(Anchor, Focus);

    /// <summary>선택된 길이가 0 이면 참(아무것도 선택되지 않음).</summary>
    public bool IsEmpty => Anchor == Focus;
}

/// <summary>
/// 레이아웃 트리 위의 텍스트 선택을 다루는 헬퍼.
/// 지정한 문서 좌표를 버퍼 오프셋으로 변환하고(히트 테스트),
/// 선택 범위의 텍스트를 추출한다.
/// </summary>
public static class TextSelectionReader
{
    /// <summary>
    /// 문서 좌표 (x, y) 가 가리키는 텍스트 위치의 버퍼 오프셋을 찾는다.
    /// y 는 스크롤이 반영된 문서 기준 좌표여야 한다.
    /// 글자 단위로 정밀하게 찾으려면 <paramref name="measurer"/> 를 넘긴다(없으면 런 단위 근사).
    /// </summary>
    public static bool TryHitTest(LayoutBox? root, float x, float y, ITextMeasurer? measurer, out int offset)
    {
        offset = 0;
        if (root is null)
        {
            return false;
        }

        LineBox? bestLine = null;
        var bestDistance = float.MaxValue;

        foreach (var line in WalkLines(root))
        {
            if (y >= line.Y && y <= line.Y + line.Height)
            {
                bestLine = line;
                break;
            }

            var centerY = line.Y + line.Height * 0.5f;
            var distance = MathF.Abs(centerY - y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestLine = line;
            }
        }

        if (bestLine is null || bestLine.Runs.Count == 0)
        {
            return false;
        }

        offset = HitTestLine(bestLine, x, measurer);
        return true;
    }

    /// <summary>선택 범위의 텍스트를 추출한다. 줄 사이에는 개행, 건너뛴 공백에는 공백을 넣는다.</summary>
    public static string GetSelectedText(LayoutBox? root, TextSelection? selection)
    {
        if (root is null || selection is null || selection.IsEmpty)
        {
            return string.Empty;
        }

        var buffer = root.TextBuffer;
        if (string.IsNullOrEmpty(buffer))
        {
            return string.Empty;
        }

        var start = selection.Start;
        var end = Math.Min(selection.End, buffer.Length);
        if (start >= end)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        LineBox? previousLine = null;
        var previousRunEnd = -1;

        foreach (var (box, line) in WalkLinesWithBox(root))
        {
            foreach (var run in line.Runs)
            {
                var runStart = run.TextStart;
                var runEnd = run.TextEnd;
                if (runStart >= end || runEnd <= start)
                {
                    continue;
                }

                if (previousRunEnd >= 0)
                {
                    if (!ReferenceEquals(previousLine, line))
                    {
                        builder.Append('\n');
                    }
                    else if (runStart > previousRunEnd)
                    {
                        // 런 사이에 건너뛴 공백(버퍼 간격)이 있으면 공백으로 보존한다.
                        builder.Append(' ');
                    }
                }

                var sliceStart = Math.Max(runStart, start);
                var sliceEnd = Math.Min(runEnd, end);
                builder.Append(buffer, sliceStart, sliceEnd - sliceStart);

                previousLine = line;
                previousRunEnd = runEnd;
            }
        }

        return builder.ToString();
    }

    private static int HitTestLine(LineBox line, float x, ITextMeasurer? measurer)
    {
        var runs = line.Runs;

        if (x <= line.X)
        {
            return runs[0].TextStart;
        }

        if (x >= line.X + line.Width)
        {
            return runs[^1].TextEnd;
        }

        foreach (var run in runs)
        {
            if (x < run.X)
            {
                // 런 사이의 간격(공백)이면 앞 런의 끝으로 돌린다.
                return PreviousRunEnd(runs, run);
            }

            if (x <= run.X + run.Width)
            {
                return HitTestRun(run, x, measurer);
            }
        }

        // 줄 폭을 넘어섰으면 마지막 런 끝으로 클램프한다.
        return runs[^1].TextEnd;
    }

    private static int PreviousRunEnd(List<InlineBox> runs, InlineBox current)
    {
        var index = runs.IndexOf(current);
        return index > 0 ? runs[index - 1].TextEnd : current.TextStart;
    }

    /// <summary>런 안의 x 지점에 대응하는 버퍼 오프셋을 글자 단위로 계산한다.</summary>
    private static int HitTestRun(InlineBox run, float x, ITextMeasurer? measurer)
    {
        var localX = x - run.X;
        if (localX <= 0f)
        {
            return run.TextStart;
        }

        if (measurer is null)
        {
            // 측정기가 없으면 근사 폭 기준으로 글자 수를 역산한다.
            var approxCharWidth = MathF.Max(1f, run.FontSize * 0.5f);
            var chars = Math.Clamp((int)(localX / approxCharWidth), 0, run.Text.Length);
            return run.TextStart + chars;
        }

        // 글자 단위로 접두 폭을 재며, x 를 넘지 않는 가장 큰 글자 수를 찾는다.
        var count = 0;
        for (var i = 1; i <= run.Text.Length; i++)
        {
            if (MeasurePrefixWidth(run, i, measurer) > localX)
            {
                break;
            }

            count = i;
        }

        return run.TextStart + count;
    }

    /// <summary>런의 앞쪽 <paramref name="length"/> 글자까지의 렌더링 폭을 구한다.</summary>
    public static float MeasurePrefixWidth(InlineBox run, int length, ITextMeasurer? measurer)
    {
        if (length <= 0)
        {
            return 0f;
        }

        if (length >= run.Text.Length)
        {
            return run.Width;
        }

        if (measurer is null)
        {
            return run.Width * ((float)length / run.Text.Length);
        }

        return measurer.MeasureWidth(
            run.Text[..length], run.FontSize, run.FontFamily, run.Bold, run.Italic);
    }

    /// <summary>
    /// 한 줄에서 선택 범위에 해당하는 하이라이트 구간(수평 X/폭, 병합됨)을 계산한다.
    /// 런과 런 사이의 공백도 선택 범위에 포함되면 하나의 연속된 구간으로 병합한다.
    /// </summary>
    public static List<(float X, float Width)> GetLineHighlights(LineBox line, TextSelection selection, ITextMeasurer? measurer)
    {
        var spans = new List<(float X, float Width)>();
        var runs = line.Runs;

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            var selectedStart = Math.Max(run.TextStart, selection.Start);
            var selectedEnd = Math.Min(run.TextEnd, selection.End);
            if (selectedStart < selectedEnd)
            {
                var startX = run.X + MeasurePrefixWidth(run, selectedStart - run.TextStart, measurer);
                var endX = run.X + MeasurePrefixWidth(run, selectedEnd - run.TextStart, measurer);
                AddMerged(spans, startX, endX);
            }

            if (i + 1 >= runs.Count)
            {
                continue;
            }

            // 런 사이의 건너뛴 공백(버퍼 간격)도 선택 범위에 포함되면 하이라이트한다.
            var next = runs[i + 1];
            var gapStart = run.TextEnd;
            var gapEnd = next.TextStart;
            if (gapStart >= gapEnd)
            {
                continue;
            }

            var gapStartX = run.X + run.Width;
            var gapEndX = next.X;
            if (gapEndX <= gapStartX)
            {
                continue;
            }

            if (selection.Start <= gapStart && selection.End >= gapEnd)
            {
                AddMerged(spans, gapStartX, gapEndX);
            }
            else if (selection.Start <= gapStart && selection.End > gapStart && selection.End < gapEnd)
            {
                var fraction = (float)(selection.End - gapStart) / (gapEnd - gapStart);
                AddMerged(spans, gapStartX, gapStartX + fraction * (gapEndX - gapStartX));
            }
            else if (selection.Start > gapStart && selection.Start < gapEnd && selection.End >= gapEnd)
            {
                var fraction = (float)(selection.Start - gapStart) / (gapEnd - gapStart);
                AddMerged(spans, gapStartX + fraction * (gapEndX - gapStartX), gapEndX);
            }
        }

        return spans;
    }

    /// <summary>
    /// 한 런을 선택 여부에 따라 최대 3개의 세그먼트(앞: 원래 색, 가운데: 선택, 뒤: 원래 색)로 나눈다.
    /// 각 세그먼트는 런 내부 오프셋과 길이, 선택 여부를 담는다. 빈 세그먼트는 포함하지 않는다.
    /// </summary>
    public static List<(int Offset, int Length, bool Selected)> GetRunSegments(InlineBox run, TextSelection? selection)
    {
        var segments = new List<(int Offset, int Length, bool Selected)>();
        var length = run.Text.Length;
        var selectStart = 0;
        var selectEnd = 0;
        if (selection is { IsEmpty: false })
        {
            selectStart = Math.Clamp(selection.Start - run.TextStart, 0, length);
            selectEnd = Math.Clamp(selection.End - run.TextStart, 0, length);
        }

        if (selectStart >= selectEnd)
        {
            if (length > 0)
            {
                segments.Add((0, length, false));
            }

            return segments;
        }

        if (selectStart > 0)
        {
            segments.Add((0, selectStart, false));
        }

        segments.Add((selectStart, selectEnd - selectStart, true));

        if (selectEnd < length)
        {
            segments.Add((selectEnd, length - selectEnd, false));
        }

        return segments;
    }

    private static void AddMerged(List<(float X, float Width)> spans, float startX, float endX)
    {
        if (endX <= startX)
        {
            return;
        }

        if (spans.Count > 0)
        {
            var last = spans[^1];
            var lastEnd = last.X + last.Width;
            if (startX <= lastEnd + 0.01f)
            {
                spans[^1] = (last.X, MathF.Max(lastEnd, endX) - last.X);
                return;
            }
        }

        spans.Add((startX, endX - startX));
    }

    private static IEnumerable<LineBox> WalkLines(LayoutBox box)
    {
        foreach (var line in box.Lines)
        {
            yield return line;
        }

        foreach (var child in box.Children)
        {
            foreach (var line in WalkLines(child))
            {
                yield return line;
            }
        }
    }

    private static IEnumerable<(LayoutBox Box, LineBox Line)> WalkLinesWithBox(LayoutBox box)
    {
        foreach (var line in box.Lines)
        {
            yield return (box, line);
        }

        foreach (var child in box.Children)
        {
            foreach (var pair in WalkLinesWithBox(child))
            {
                yield return pair;
            }
        }
    }
}
