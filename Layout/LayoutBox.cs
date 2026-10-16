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
