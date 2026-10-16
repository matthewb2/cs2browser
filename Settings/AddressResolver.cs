namespace CS2Browser.Settings;

/// <summary>
/// 주소창 입력을 URL 또는 검색어로 해석한다.
/// 현대 브라우저처럼 URL 로 보이면 그대로 이동하고(스킴이 없으면 https:// 를 붙임),
/// 그렇지 않으면 설정된 검색 엔진으로 검색한다.
/// </summary>
public static class AddressResolver
{
    /// <summary>입력이 이동 대상 URL 처럼 보이는지 판단한다(검색어가 아닌지).</summary>
    public static bool LooksLikeUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.Trim();

        // 공백이 있으면 검색어로 본다.
        if (text.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (text.Contains("://", StringComparison.Ordinal))
        {
            return true;
        }

        if (text.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // "example.com", "example.com/path", "192.168.0.1" 등 도메인 형태.
        if (text.Contains('.'))
        {
            return true;
        }

        return false;
    }

    /// <summary>입력을 실제 이동할 URL 로 변환한다. URL 이 아니면 검색 URL 을 만든다.</summary>
    public static string Resolve(string input, AppSettings settings)
    {
        var text = (input ?? string.Empty).Trim();

        if (LooksLikeUrl(text))
        {
            if (HasScheme(text))
            {
                return text;
            }

            return "https://" + text;
        }

        return settings.BuildSearchUrl(text);
    }

    private static bool HasScheme(string text)
        => text.Contains("://", StringComparison.Ordinal)
        || text.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
}
