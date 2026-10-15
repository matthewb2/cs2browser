using CS2Browser.Layout;
using Vortice.Mathematics;

namespace CS2Browser.Tests;

[TestClass]
public sealed class CssColorTests
{
    [DataTestMethod]
    [DataRow("#f00", 1f, 0f, 0f, 1f)]
    [DataRow("#00ff00", 0f, 1f, 0f, 1f)]
    [DataRow("#ff8800", 1f, 136f / 255f, 0f, 1f)]
    [DataRow("rgb(0, 0, 255)", 0f, 0f, 1f, 1f)]
    [DataRow("rgba(255, 255, 0, 0.5)", 1f, 1f, 0f, 0.5f)]
    [DataRow("red", 1f, 0f, 0f, 1f)]
    [DataRow("transparent", 0f, 0f, 0f, 0f)]
    public void TryParse_ValidValues(string input, float r, float g, float b, float a)
    {
        Assert.IsTrue(CssColor.TryParse(input, out var color));
        ColorAssert.AreEqual(new Color4(r, g, b, a), color);
    }

    [TestMethod]
    public void TryParse_Invalid_ReturnsFalse()
    {
        Assert.IsFalse(CssColor.TryParse("not-a-color", out _));
        Assert.IsFalse(CssColor.TryParse(string.Empty, out _));
        Assert.IsFalse(CssColor.TryParse(null, out _));
    }
}
