using CS2Browser.Rendering;
using Vortice.DirectWrite;
using DwFontStyle = Vortice.DirectWrite.FontStyle;
using DwFactoryType = Vortice.DirectWrite.FactoryType;

namespace CS2Browser.Tests;

[TestClass]
public sealed class DirectWriteTextRendererTests
{
    [TestMethod]
    public void GetFormat_CachesByKey()
    {
        using var factory = DWrite.DWriteCreateFactory<IDWriteFactory>(DwFactoryType.Shared);
        using var renderer = new DirectWriteTextRenderer(factory);

        var a = renderer.GetFormat("Malgun Gothic", 16f, false, false);
        var b = renderer.GetFormat("Malgun Gothic", 16f, false, false);
        var c = renderer.GetFormat("Malgun Gothic", 20f, false, false);

        Assert.AreSame(a, b);
        Assert.AreNotSame(a, c);
        Assert.AreEqual(16f, a.FontSize, 0.01f);
    }

    [TestMethod]
    public void DirectWrite_MeasuresTextWidth()
    {
        using var factory = DWrite.DWriteCreateFactory<IDWriteFactory>(DwFactoryType.Shared);
        using var format = factory.CreateTextFormat(
            "Malgun Gothic", FontWeight.Normal, DwFontStyle.Normal, FontStretch.Normal, 20f);
        using var layout = factory.CreateTextLayout("Hello", format, 500f, 100f);

        Assert.IsTrue(layout.Metrics.Width > 0f, "텍스트 폭이 0입니다.");
    }
}
