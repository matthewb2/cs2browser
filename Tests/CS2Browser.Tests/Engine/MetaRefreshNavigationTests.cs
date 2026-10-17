using CS2Browser.Engine;

namespace CS2Browser.Tests;

[TestClass]
public sealed class MetaRefreshNavigationTests
{
    [TestMethod]
    public async Task NavigateAsync_FollowsMetaRefresh()
    {
        var dir = CreateTempDir();
        try
        {
            WritePage(dir, "start.html",
                "<html><head><meta http-equiv=\"refresh\" content=\"0;URL=target.html\"></head><body>start</body></html>");
            WritePage(dir, "target.html",
                "<html><head><title>Target</title></head><body>redirected-content</body></html>");

            using var engine = new BrowserEngine();
            await engine.NavigateAsync(Url(dir, "start.html"), 800f);

            StringAssert.EndsWith(engine.CurrentAddress!, "target.html");
            StringAssert.Contains(engine.LayoutRoot?.TextBuffer ?? string.Empty, "redirected-content");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public async Task NavigateAsync_FollowsNoScriptMetaRefresh()
    {
        var dir = CreateTempDir();
        try
        {
            WritePage(dir, "start.html",
                "<html><head><noscript><meta http-equiv=\"refresh\" content=\"0;URL=target.html\"></noscript></head>"
                + "<body>start</body></html>");
            WritePage(dir, "target.html",
                "<html><head><title>Target</title></head><body>noscript-content</body></html>");

            using var engine = new BrowserEngine();
            await engine.NavigateAsync(Url(dir, "start.html"), 800f);

            StringAssert.EndsWith(engine.CurrentAddress!, "target.html");
            StringAssert.Contains(engine.LayoutRoot?.TextBuffer ?? string.Empty, "noscript-content");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public async Task NavigateAsync_FollowsNoScriptMetaRefresh_WithEncodedQuotes()
    {
        var dir = CreateTempDir();
        try
        {
            // 덕덕고처럼 noscript 안에서 URL이 &quot; 엔티티로 감싸진 경우.
            WritePage(dir, "start.html",
                "<html><head><noscript><meta http-equiv=\"refresh\" content=\"0;URL=&quot;target.html&quot;\"></noscript></head>"
                + "<body>start</body></html>");
            WritePage(dir, "target.html",
                "<html><head><title>Target</title></head><body>encoded-content</body></html>");

            using var engine = new BrowserEngine();
            await engine.NavigateAsync(Url(dir, "start.html"), 800f);

            StringAssert.EndsWith(engine.CurrentAddress!, "target.html");
            StringAssert.Contains(engine.LayoutRoot?.TextBuffer ?? string.Empty, "encoded-content");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public async Task NavigateAsync_WithoutMetaRefresh_StaysOnRequestedPage()
    {
        var dir = CreateTempDir();
        try
        {
            WritePage(dir, "plain.html",
                "<html><head><title>Plain</title></head><body>plain-content</body></html>");

            using var engine = new BrowserEngine();
            await engine.NavigateAsync(Url(dir, "plain.html"), 800f);

            StringAssert.EndsWith(engine.CurrentAddress!, "plain.html");
            StringAssert.Contains(engine.LayoutRoot?.TextBuffer ?? string.Empty, "plain-content");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cs2browser-meta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WritePage(string dir, string name, string html)
        => File.WriteAllText(Path.Combine(dir, name), html);

    private static string Url(string dir, string name)
        => new Uri(Path.Combine(dir, name)).AbsoluteUri;
}
