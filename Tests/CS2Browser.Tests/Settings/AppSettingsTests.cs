using CS2Browser.Settings;

namespace CS2Browser.Tests;

[TestClass]
public sealed class AppSettingsTests
{
    [TestMethod]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var settings = AppSettings.Load(path);

        Assert.AreEqual(AppSettings.DefaultSearchEngineName, settings.SearchEngineName);
        Assert.AreEqual(AppSettings.DefaultSearchUrlTemplate, settings.SearchUrlTemplate);
    }

    [TestMethod]
    public void Load_ValidFile_UsesConfiguredValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.WriteAllText(path, """
            {
              "searchEngine": "DuckDuckGo",
              "searchUrlTemplate": "https://duckduckgo.com/?q={query}"
            }
            """);

        try
        {
            var settings = AppSettings.Load(path);
            Assert.AreEqual("DuckDuckGo", settings.SearchEngineName);
            Assert.AreEqual("https://duckduckgo.com/?q={query}", settings.SearchUrlTemplate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_InvalidJson_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.WriteAllText(path, "{ this is not valid json ");

        try
        {
            var settings = AppSettings.Load(path);
            Assert.AreEqual(AppSettings.DefaultSearchUrlTemplate, settings.SearchUrlTemplate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Normalize_TemplateWithoutPlaceholder_FallsBackToDefault()
    {
        var settings = new AppSettings { SearchUrlTemplate = "https://example.com/search" }.Normalize();

        Assert.AreEqual(AppSettings.DefaultSearchUrlTemplate, settings.SearchUrlTemplate);
    }

    [TestMethod]
    public void BuildSearchUrl_EncodesQuery()
    {
        var settings = AppSettings.CreateDefault();
        var url = settings.BuildSearchUrl("hello world & more");

        Assert.AreEqual("https://duckduckgo.com/?q=hello%20world%20%26%20more", url);
    }

    [TestMethod]
    public void BuildSearchUrl_UsesCustomTemplate()
    {
        var settings = new AppSettings
        {
            SearchUrlTemplate = "https://www.bing.com/search?q={query}"
        };

        Assert.AreEqual("https://www.bing.com/search?q=test", settings.BuildSearchUrl("test"));
    }
}
