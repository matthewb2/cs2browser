using CS2Browser.Layout;
using Vortice.Direct2D1;
using DColor = Vortice.Mathematics.Color4;
using DRect = Vortice.Mathematics.Rect;

namespace CS2Browser.Rendering;

/// <summary>
/// 레이아웃 박스를 순회하며 Direct2D 드로잉 명령으로 변환하는 페인터.
/// 배경 사각형은 Direct2D 로, 텍스트는 DirectWrite 렌더러로 그린다.
/// </summary>
public sealed class PaintEngine : IDisposable
{
    private readonly DirectWriteTextRenderer _textRenderer;

    public PaintEngine(DirectWriteTextRenderer textRenderer) => _textRenderer = textRenderer;

    public void Paint(ID2D1RenderTarget target, Direct2DContext context, LayoutBox root, TextSelection? selection = null)
        => PaintBox(target, context, root, selection);

    /// <summary>
    /// 지정한 링크 위에 마우스가 올라갔을 때, 그 링크에 속한 텍스트 아래에 밑줄을 그린다.
    /// 같은 줄에서 같은 링크에 속한 런들은 하나의 연속된 밑줄로 이어 그린다.
    /// </summary>
    public void PaintLinkUnderlines(ID2D1RenderTarget target, Direct2DContext context, LayoutBox root, LinkTarget hoveredLink)
        => PaintUnderlinesBox(target, context, root, hoveredLink);

    private void PaintUnderlinesBox(ID2D1RenderTarget target, Direct2DContext context, LayoutBox box, LinkTarget hoveredLink)
    {
        foreach (var line in box.Lines)
        {
            var firstX = float.MaxValue;
            var lastX = float.MinValue;
            var thickness = 0f;
            var color = new DColor(0f, 0f, 0f, 1f);
            var any = false;

            foreach (var run in line.Runs)
            {
                if (!ReferenceEquals(run.Link, hoveredLink))
                {
                    continue;
                }

                firstX = MathF.Min(firstX, run.X);
                lastX = MathF.Max(lastX, run.X + run.Width);
                thickness = MathF.Max(thickness, MathF.Max(1f, run.FontSize * 0.06f));
                color = run.Color;
                any = true;
            }

            if (!any || lastX <= firstX)
            {
                continue;
            }

            // 밑줄은 줄 상자 하단 근처(베이스라인 아래)에 둔다.
            var y = line.Y + line.Height - thickness - line.Height * 0.12f;
            var rectangle = new DRect(firstX, y, lastX - firstX, thickness);
            target.FillRectangle(rectangle, context.GetBrush(color));
        }

        foreach (var child in box.Children)
        {
            PaintUnderlinesBox(target, context, child, hoveredLink);
        }
    }

    private void PaintBox(ID2D1RenderTarget target, Direct2DContext context, LayoutBox box, TextSelection? selection)
    {
        if (box.BackgroundColor is DColor background && background.A > 0f)
        {
            var rectangle = new DRect(
                box.PaddingBoxLeft,
                box.PaddingBoxTop,
                MathF.Max(1f, box.PaddingBoxWidth),
                MathF.Max(1f, box.PaddingBoxHeight));

            target.FillRectangle(rectangle, context.GetBrush(background));
        }

        if (selection is not null && !selection.IsEmpty)
        {
            PaintSelectionInBox(target, context, box, selection);
        }

        foreach (var line in box.Lines)
        {
            foreach (var run in line.Runs)
            {
                DrawRun(target, context, run, line, selection);
            }
        }

        if (!string.IsNullOrEmpty(box.ImageSource))
        {
            var bitmap = context.GetImage(box.ImageSource);
            if (bitmap is not null)
            {
                var destination = new DRect(
                    box.X,
                    box.Y,
                    MathF.Max(1f, box.Width),
                    MathF.Max(1f, box.Height));

                target.DrawBitmap(bitmap, destination, 1f, BitmapInterpolationMode.Linear, null);
            }
        }

        foreach (var child in box.Children)
        {
            PaintBox(target, context, child, selection);
        }
    }

    /// <summary>
    /// 선택 범위에 걸친 텍스트 런들의 배경을 하이라이트로 채운다.
    /// 런 일부만 선택된 경우 측정기로 접두 폭을 계산해 정확한 경계를 구하고,
    /// 단어 사이의 공백도 선택 범위에 포함되면 하나의 연속된 사각형으로 채운다.
    /// </summary>
    private void PaintSelectionInBox(ID2D1RenderTarget target, Direct2DContext context, LayoutBox box, TextSelection selection)
    {
        var brush = context.GetBrush(SelectionColor);

        foreach (var line in box.Lines)
        {
            foreach (var span in TextSelectionReader.GetLineHighlights(line, selection, _textRenderer))
            {
                var rectangle = new DRect(span.X, line.Y, span.Width, line.Height);
                target.FillRectangle(rectangle, brush);
            }
        }
    }

    /// <summary>
    /// 한 런을 그린다. 선택 범위에 걸친 경우 선택 구간은 흰색으로,
    /// 나머지는 원래 색으로 나누어(접두 폭 기준) 그린다.
    /// </summary>
    private void DrawRun(ID2D1RenderTarget target, Direct2DContext context, InlineBox run, LineBox line, TextSelection? selection)
    {
        foreach (var segment in TextSelectionReader.GetRunSegments(run, selection))
        {
            var x = run.X + TextSelectionReader.MeasurePrefixWidth(run, segment.Offset, _textRenderer);
            var color = segment.Selected ? SelectedTextColor : run.Color;
            DrawSegment(target, context, run, line, run.Text.Substring(segment.Offset, segment.Length), x, color);
        }
    }

    private void DrawSegment(
        ID2D1RenderTarget target,
        Direct2DContext context,
        InlineBox run,
        LineBox line,
        string text,
        float x,
        DColor color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _textRenderer.DrawText(
            target,
            text,
            x,
            line.Y,
            run.Width + 2f,
            line.Height,
            context.GetBrush(color),
            run.FontFamily,
            run.FontSize,
            run.Bold,
            run.Italic);
    }

    private static readonly DColor SelectedTextColor = new(1f, 1f, 1f, 1f);

    /// <summary>선택 하이라이트 색(진한 파랑). 흰 글자가 위에 그려져도 잘 보이도록 불투명하게 채운다.</summary>
    private static readonly DColor SelectionColor = new(0f, 0.47f, 0.83f, 1f);

    public void Dispose()
    {
        // _textRenderer 는 Direct2DContext 소유이므로 여기서는 해제하지 않는다.
    }
}
