using CS2Browser.Settings;

namespace CS2Browser.Tests;

[TestClass]
public sealed class AddressResolverTests
{
    private static readonly AppSettings Settings = AppSettings.CreateDefault();

    [DataTestMethod]
    [DataRow("https://example.com", true)]
    [DataRow("http://example.com/path", true)]
    [DataRow("example.com", true)]
    [DataRow("example.com/path?x=1", true)]
    [DataRow("192.168.0.1", true)]
    [DataRow("about:local", true)]
    [DataRow("localhost:8080", true)]
    [DataRow("hello world", false)]
    [DataRow("duckduckgo", false)]
    [DataRow("c#", false)]
    [DataRow("   ", false)]
    public void LooksLikeUrl_ClassifiesInputs(string input, bool expected)
    {
        Assert.AreEqual(expected, AddressResolver.LooksLikeUrl(input));
    }

    [TestMethod]
    public void Resolve_UrlWithScheme_Unchanged()
    {
        Assert.AreEqual(
            "https://example.com/page",
            AddressResolver.Resolve("https://example.com/page", Settings));
    }

    [TestMethod]
    public void Resolve_BareDomain_PrependsHttps()
    {
        Assert.AreEqual("https://example.com", AddressResolver.Resolve("example.com", Settings));
    }

    [TestMethod]
    public void Resolve_SearchQuery_UsesDuckDuckGo()
    {
        Assert.AreEqual(
            "https://duckduckgo.com/?q=hello%20world",
            AddressResolver.Resolve("hello world", Settings));
    }

    [TestMethod]
    public void Resolve_AboutAddress_Unchanged()
    {
        Assert.AreEqual("about:local", AddressResolver.Resolve("about:local", Settings));
    }

    [TestMethod]
    public void Resolve_TrimsInput()
    {
        Assert.AreEqual("https://example.com", AddressResolver.Resolve("  example.com  ", Settings));
    }

    [TestMethod]
    public void Resolve_CustomSearchEngine_IsUsed()
    {
        var settings = new AppSettings
        {
            SearchUrlTemplate = "https://www.bing.com/search?q={query}"
        };

        Assert.AreEqual(
            "https://www.bing.com/search?q=cats",
            AddressResolver.Resolve("cats", settings));
    }
}
