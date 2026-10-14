using System.Globalization;
using Vortice.Mathematics;

namespace CS2Browser.Layout;

/// <summary>
/// CSS 색상 문자열(rgb/rgba/hex/이름)을 Direct2D 용 Color4 로 변환하는 도우미.
/// </summary>
internal static class CssColor
{
    private static readonly Dictionary<string, Color4> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = new(0f, 0f, 0f, 1f),
        ["white"] = new(1f, 1f, 1f, 1f),
        ["red"] = new(1f, 0f, 0f, 1f),
        ["green"] = new(0f, 0.502f, 0f, 1f),
        ["blue"] = new(0f, 0f, 1f, 1f),
        ["yellow"] = new(1f, 1f, 0f, 1f),
        ["gray"] = new(0.502f, 0.502f, 0.502f, 1f),
        ["grey"] = new(0.502f, 0.502f, 0.502f, 1f),
        ["silver"] = new(0.753f, 0.753f, 0.753f, 1f),
        ["orange"] = new(1f, 0.647f, 0f, 1f),
        ["purple"] = new(0.502f, 0f, 0.502f, 1f),
        ["navy"] = new(0f, 0f, 0.502f, 1f),
        ["teal"] = new(0f, 0.502f, 0.502f, 1f)
    };

    public static bool TryParse(string? value, out Color4 color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim().ToLowerInvariant();

        if (value == "transparent")
        {
            color = new Color4(0f, 0f, 0f, 0f);
            return true;
        }

        if (value.StartsWith('#'))
        {
            return TryParseHex(value[1..], out color);
        }

        if (value.StartsWith("rgb", StringComparison.Ordinal))
        {
            var open = value.IndexOf('(');
            var close = value.IndexOf(')');
            if (open < 0 || close < 0 || close <= open)
            {
                return false;
            }

            var parts = value[(open + 1)..close]
                .Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return false;
            }

            color = new Color4(
                ParseChannel(parts[0]),
                ParseChannel(parts[1]),
                ParseChannel(parts[2]),
                parts.Length >= 4 ? ParseAlpha(parts[3]) : 1f);
            return true;
        }

        return Named.TryGetValue(value, out color);
    }

    private static bool TryParseHex(string hex, out Color4 color)
    {
        color = default;
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length == 6)
        {
            hex += "ff";
        }

        if (hex.Length != 8
            || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return false;
        }

        color = new Color4(
            ((packed >> 24) & 0xFF) / 255f,
            ((packed >> 16) & 0xFF) / 255f,
            ((packed >> 8) & 0xFF) / 255f,
            (packed & 0xFF) / 255f);
        return true;
    }

    private static float ParseChannel(string value)
    {
        value = value.Trim();
        if (value.EndsWith('%'))
        {
            return float.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
                ? Math.Clamp(percent / 100f, 0f, 1f)
                : 0f;
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? Math.Clamp(number / 255f, 0f, 1f)
            : 0f;
    }

    private static float ParseAlpha(string value)
    {
        value = value.Trim();
        if (value.EndsWith('%'))
        {
            return float.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
                ? Math.Clamp(percent / 100f, 0f, 1f)
                : 1f;
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? Math.Clamp(number, 0f, 1f)
            : 1f;
    }
}
