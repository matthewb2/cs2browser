using System.Text.Json;
using System.Text.Json.Serialization;

namespace CS2Browser.Settings;

/// <summary>
/// 브라우저 설정. 설정 파일(settings.json)에서 로드하며,
/// 파일이 없거나 값이 잘못되면 기본값(DuckDuckGo 검색)으로 대체한다.
/// </summary>
public sealed class AppSettings
{
    /// <summary>검색어를 삽입하는 자리 표시자.</summary>
    public const string SearchQueryPlaceholder = "{query}";

    /// <summary>기본 검색 엔진 URL 템플릿(덕덕고).</summary>
    public const string DefaultSearchUrlTemplate = "https://duckduckgo.com/?q={query}";

    /// <summary>기본 검색 엔진 이름.</summary>
    public const string DefaultSearchEngineName = "DuckDuckGo";

    /// <summary>검색 엔진 표시 이름.</summary>
    [JsonPropertyName("searchEngine")]
    public string SearchEngineName { get; set; } = DefaultSearchEngineName;

    /// <summary>검색 URL 템플릿. <see cref="SearchQueryPlaceholder"/> 가 검색어로 치환된다.</summary>
    [JsonPropertyName("searchUrlTemplate")]
    public string SearchUrlTemplate { get; set; } = DefaultSearchUrlTemplate;

    /// <summary>기본값으로 만든 설정을 반환한다.</summary>
    public static AppSettings CreateDefault() => new();

    /// <summary>
    /// 설정 파일을 로드한다. 파일이 없거나 읽을 수 없으면 기본값을 반환한다.
    /// </summary>
    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
                if (settings is not null)
                {
                    return settings.Normalize();
                }
            }
        }
        catch (Exception)
        {
            // 손상된 설정 파일 등은 무시하고 기본값을 사용한다.
        }

        return CreateDefault();
    }

    /// <summary>잘못된 값을 기본값으로 보정한다.</summary>
    public AppSettings Normalize()
    {
        if (string.IsNullOrWhiteSpace(SearchUrlTemplate)
            || !SearchUrlTemplate.Contains(SearchQueryPlaceholder, StringComparison.Ordinal))
        {
            SearchUrlTemplate = DefaultSearchUrlTemplate;
        }

        if (string.IsNullOrWhiteSpace(SearchEngineName))
        {
            SearchEngineName = DefaultSearchEngineName;
        }

        return this;
    }

    /// <summary>검색어를 설정된 검색 엔진의 절대 URL로 변환한다.</summary>
    public string BuildSearchUrl(string query)
        => SearchUrlTemplate.Replace(
            SearchQueryPlaceholder,
            Uri.EscapeDataString(query ?? string.Empty),
            StringComparison.Ordinal);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
}
