using Vortice.Mathematics;

namespace CS2Browser.Tests;

internal static class ColorAssert
{
    public static void AreEqual(Color4 expected, Color4 actual, float tolerance = 0.01f)
    {
        Assert.AreEqual(expected.R, actual.R, tolerance, $"R: {expected.R} vs {actual.R}");
        Assert.AreEqual(expected.G, actual.G, tolerance, $"G: {expected.G} vs {actual.G}");
        Assert.AreEqual(expected.B, actual.B, tolerance, $"B: {expected.B} vs {actual.B}");
        Assert.AreEqual(expected.A, actual.A, tolerance, $"A: {expected.A} vs {actual.A}");
    }
}
