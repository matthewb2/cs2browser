using System.Buffers.Binary;
using System.IO;

namespace CS2Browser.Layout;

/// <summary>
/// 이미지 파일의 헤더만 읽어 고유 크기(intrinsic size)를 알아낸다.
/// WIC/Direct2D 없이 레이아웃 단계에서 이미지 크기를 정하기 위해 사용한다.
/// PNG / GIF / BMP / JPEG / WEBP 를 지원한다.
/// </summary>
internal static class ImageInfo
{
    public static bool TryGetSizeFromFile(string path, out int width, out int height)
    {
        width = 0;
        height = 0;

        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            return TryGetSize(File.ReadAllBytes(path), out width, out height);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TryGetSize(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (data.Length < 8)
        {
            return false;
        }

        return TryPng(data, out width, out height)
            || TryGif(data, out width, out height)
            || TryBmp(data, out width, out height)
            || TryJpeg(data, out width, out height)
            || TryWebp(data, out width, out height);
    }

    private static bool TryPng(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;

        // 89 50 4E 47 0D 0A 1A 0A
        if (data.Length < 24
            || data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47)
        {
            return false;
        }

        // IHDR 는 항상 첫 청크(오프셋 16 부터 width, height).
        width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4));
        height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4));
        return width > 0 && height > 0;
    }

    private static bool TryGif(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (data.Length < 10 || data[0] != 'G' || data[1] != 'I' || data[2] != 'F')
        {
            return false;
        }

        width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6, 2));
        height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8, 2));
        return width > 0 && height > 0;
    }

    private static bool TryBmp(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (data.Length < 26 || data[0] != 'B' || data[1] != 'M')
        {
            return false;
        }

        width = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(18, 4));
        height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(22, 4)));
        return width > 0 && height > 0;
    }

    private static bool TryJpeg(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return false;
        }

        var index = 2;
        while (index + 9 < data.Length)
        {
            if (data[index] != 0xFF)
            {
                index++;
                continue;
            }

            var marker = data[index + 1];
            // SOF0..SOF15 (단, DHT=C4, JPG=C8, DAC=CC 는 제외)
            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                height = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(index + 5, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(index + 7, 2));
                return width > 0 && height > 0;
            }

            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                index += 2;
                continue;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(index + 2, 2));
            if (segmentLength < 2)
            {
                return false;
            }

            index += 2 + segmentLength;
        }

        return false;
    }

    private static bool TryWebp(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (data.Length < 30
            || data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F'
            || data[8] != 'W' || data[9] != 'E' || data[10] != 'B' || data[11] != 'P')
        {
            return false;
        }

        var format = System.Text.Encoding.ASCII.GetString(data, 12, 4);
        if (format == "VP8X" && data.Length >= 30)
        {
            width = 1 + (data[24] | (data[25] << 8) | (data[26] << 16));
            height = 1 + (data[27] | (data[28] << 8) | (data[29] << 16));
            return true;
        }

        if (format == "VP8 " && data.Length >= 30)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(26, 2)) & 0x3FFF;
            height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(28, 2)) & 0x3FFF;
            return width > 0 && height > 0;
        }

        return false;
    }
}
