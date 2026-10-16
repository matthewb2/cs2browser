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
/// 하나의 &lt;a href&gt; 앵커를 나타낸다. 앵커의 모든 조각/박스가 동일 인스턴스를 공유하므로
/// 참조 비교로 "같은 링크"를 구분할 수 있다(같은 URL의 다른 링크와도 구별됨).
/// </summary>
public sealed class LinkTarget
{
    public LinkTarget(string url) => Url = url;

    /// <summary>해석된 절대 URL.</summary>
    public string Url { get; }
}
