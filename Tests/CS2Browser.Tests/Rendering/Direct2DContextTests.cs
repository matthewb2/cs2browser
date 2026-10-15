using CS2Browser.Layout;
using CS2Browser.Rendering;
using Vortice.Mathematics;

namespace CS2Browser.Tests;

[TestClass]
public sealed class Direct2DContextTests
{
    [TestMethod]
    public void Initialize_RenderAndResize_ToHiddenWindow()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form
            {
                ClientSize = new System.Drawing.Size(400, 300),
                ShowInTaskbar = false
            };
            _ = form.Handle;

            using var context = new Direct2DContext();
            context.Initialize(form.Handle, 400, 300);
            Assert.IsTrue(context.IsInitialized);

            using var textRenderer = new DirectWriteTextRenderer(context.WriteFactory);
            using var painter = new PaintEngine(textRenderer);

            var root = new LayoutBox { TagName = "#viewport", BackgroundColor = new Color4(1f, 1f, 1f, 1f) };
            root.X = 0f; root.Y = 0f; root.Width = 400f; root.Height = 300f;

            var child = new LayoutBox { TagName = "div", BackgroundColor = new Color4(1f, 0f, 0f, 1f) };
            child.X = 10f; child.Y = 10f; child.Width = 100f; child.Height = 40f;
            root.Children.Add(child);

            context.BeginDraw();
            context.Clear(new Color4(1f, 1f, 1f, 1f));
            painter.Paint(context.RenderTarget, context, root);
            Assert.IsTrue(context.EndDraw(), "첫 번째 EndDraw 가 실패했습니다.");

            context.Resize(500, 400);
            context.BeginDraw();
            context.Clear(new Color4(1f, 1f, 1f, 1f));
            painter.Paint(context.RenderTarget, context, root);
            Assert.IsTrue(context.EndDraw(), "리사이즈 후 EndDraw 가 실패했습니다.");
        });
    }

    [TestMethod]
    public void GetBrush_ReturnsSameInstanceForSameColor()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(64, 64) };
            _ = form.Handle;

            using var context = new Direct2DContext();
            context.Initialize(form.Handle, 64, 64);

            var first = context.GetBrush(new Color4(1f, 0f, 0f, 1f));
            var second = context.GetBrush(new Color4(1f, 0f, 0f, 1f));

            Assert.AreSame(first, second);
        });
    }
}
