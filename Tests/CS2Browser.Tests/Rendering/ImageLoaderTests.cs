using CS2Browser.Layout;
using CS2Browser.Rendering;
using Vortice.Mathematics;

namespace CS2Browser.Tests;

[TestClass]
public sealed class ImageLoaderTests
{
    private static string LogoPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Res", "wisp-logo.png");
        Assert.IsTrue(File.Exists(path), $"테스트 이미지를 찾을 수 없습니다: {path}");
        return path;
    }

    [TestMethod]
    public void TryGetPixelSize_ReadsPng()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(64, 64) };
            _ = form.Handle;

            using var context = new Direct2DContext();
            context.Initialize(form.Handle, 64, 64);

            Assert.IsTrue(ImageLoader.TryGetPixelSize(context.WicFactory, LogoPath(), out var w, out var h));
            Assert.IsTrue(w > 0 && h > 0);
        });
    }

    [TestMethod]
    public void LoadBitmap_DecodesPngWithPixelSize()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(64, 64) };
            _ = form.Handle;

            using var context = new Direct2DContext();
            context.Initialize(form.Handle, 64, 64);

            ImageLoader.TryGetPixelSize(context.WicFactory, LogoPath(), out var w, out var h);

            using var bitmap = ImageLoader.LoadBitmap(context.RenderTarget, context.WicFactory, LogoPath());
            Assert.IsNotNull(bitmap);
            Assert.AreEqual(w, (int)bitmap!.PixelSize.Width);
            Assert.AreEqual(h, (int)bitmap.PixelSize.Height);
        });
    }

    [TestMethod]
    public void GetImage_CachesBySource()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(64, 64) };
            _ = form.Handle;

            using var context = new Direct2DContext();
            context.Initialize(form.Handle, 64, 64);

            var url = new Uri(LogoPath()).AbsoluteUri;
            var first = context.GetImage(url);
            var second = context.GetImage(url);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
        });
    }

    [TestMethod]
    public void Paint_DrawsImageBox()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(320, 240) };
            _ = form.Handle;

            using var context = new Direct2DContext();
            context.Initialize(form.Handle, 320, 240);

            using var textRenderer = new DirectWriteTextRenderer(context.WriteFactory);
            using var painter = new PaintEngine(textRenderer);

            var root = new LayoutBox { TagName = "#viewport", BackgroundColor = new Color4(1f, 1f, 1f, 1f) };
            root.X = 0f; root.Y = 0f; root.Width = 320f; root.Height = 240f;

            var image = new LayoutBox
            {
                TagName = "img",
                ImageSource = new Uri(LogoPath()).AbsoluteUri,
                Width = 200f,
                Height = 80f
            };
            image.X = 20f; image.Y = 20f;
            root.Children.Add(image);

            context.BeginDraw();
            context.Clear(new Color4(1f, 1f, 1f, 1f));
            painter.Paint(context.RenderTarget, context, root);
            Assert.IsTrue(context.EndDraw());
        });
    }
}
