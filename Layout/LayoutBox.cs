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
}
