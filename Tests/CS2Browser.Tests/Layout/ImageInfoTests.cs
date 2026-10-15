using System.Buffers.Binary;
using System.Text;
using CS2Browser.Layout;

namespace CS2Browser.Tests;

[TestClass]
public sealed class ImageInfoTests
{
    [TestMethod]
    public void TryGetSize_Png()
    {
        Assert.IsTrue(ImageInfo.TryGetSize(Png(280, 90), out var w, out var h));
        Assert.AreEqual(280, w);
        Assert.AreEqual(90, h);
    }

    [TestMethod]
    public void TryGetSize_Gif()
    {
        Assert.IsTrue(ImageInfo.TryGetSize(Gif(120, 40), out var w, out var h));
        Assert.AreEqual(120, w);
        Assert.AreEqual(40, h);
    }

    [TestMethod]
    public void TryGetSize_Bmp()
    {
        Assert.IsTrue(ImageInfo.TryGetSize(Bmp(64, 32), out var w, out var h));
        Assert.AreEqual(64, w);
        Assert.AreEqual(32, h);
    }

    [TestMethod]
    public void TryGetSize_Unknown_ReturnsFalse()
    {
        Assert.IsFalse(ImageInfo.TryGetSize(Encoding.ASCII.GetBytes("not an image at all"), out _, out _));
    }

    [TestMethod]
    public void TryGetSizeFromFile_ReadsRealPng()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Res", "wisp-logo.png");
        Assert.IsTrue(File.Exists(path), $"테스트 이미지를 찾을 수 없습니다: {path}");

        Assert.IsTrue(ImageInfo.TryGetSizeFromFile(path, out var w, out var h));
        Assert.IsTrue(w > 0 && h > 0, $"크기 읽기 실패: {w}x{h}");
    }

    private static byte[] Png(int width, int height)
    {
        var data = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(data, 0);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8, 4), 13);
        data[12] = (byte)'I'; data[13] = (byte)'H'; data[14] = (byte)'D'; data[15] = (byte)'R';
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(20, 4), height);
        return data;
    }

    private static byte[] Gif(int width, int height)
    {
        var data = new byte[10];
        Encoding.ASCII.GetBytes("GIF89a").CopyTo(data, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6, 2), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8, 2), (ushort)height);
        return data;
    }

    private static byte[] Bmp(int width, int height)
    {
        var data = new byte[26];
        data[0] = (byte)'B'; data[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(18, 4), width);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(22, 4), height);
        return data;
    }
}
