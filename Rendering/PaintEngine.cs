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

    public void Paint(ID2D1RenderTarget target, Direct2DContext context, LayoutBox root)
        => PaintBox(target, context, root);

    private void PaintBox(ID2D1RenderTarget target, Direct2DContext context, LayoutBox box)
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

        foreach (var line in box.Lines)
        {
            foreach (var run in line.Runs)
            {
                _textRenderer.DrawText(
                    target,
                    run.Text,
                    run.X,
                    line.Y,
                    run.Width + 2f,
                    line.Height,
                    context.GetBrush(run.Color),
                    run.FontFamily,
                    run.FontSize,
                    run.Bold,
                    run.Italic);
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
            PaintBox(target, context, child);
        }
    }

    public void Dispose()
    {
        // _textRenderer 는 Direct2DContext 소유이므로 여기서는 해제하지 않는다.
    }
}
