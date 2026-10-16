using CS2Browser.Engine;
using CS2Browser.Layout;
using CS2Browser.Network;
using Vortice.Mathematics;

namespace CS2Browser.Tests;

[TestClass]
public sealed class LocalPageTests
{
    private static string StartPageUrl()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Res", "index.html");
        Assert.IsTrue(File.Exists(path), $"시작 페이지를 찾을 수 없습니다: {path}");
        return new Uri(path).AbsoluteUri;
    }

    [TestMethod]
    public async Task OpenAsync_LoadsLocalPageWithExternalCss()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);

        var document = await dom.OpenAsync(StartPageUrl());

        Assert.AreEqual("Wisp Browser", document.Title);

        var root = new LayoutTreeBuilder().Build(document, 1024f);
        var body = root.Children[0];
        Assert.AreEqual("body", body.TagName);

        // 외부 style.css 의 body { background-color: #fffbe6 } 가 적용되어야 한다.
        Assert.IsNotNull(body.BackgroundColor, "외부 style.css 가 적용되지 않았습니다.");
        ColorAssert.AreEqual(new Color4(1f, 251f / 255f, 230f / 255f, 1f), body.BackgroundColor!.Value);
    }

    [TestMethod]
    public async Task OpenAsync_LocalPage_RendersHeadingText()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);

        var document = await dom.OpenAsync(StartPageUrl());
        var root = new LayoutTreeBuilder().Build(document, 1024f);

        var words = CollectText(root).ToList();
        CollectionAssert.Contains(words, "Wisp");
        CollectionAssert.Contains(words, "Download");
    }

    [TestMethod]
    public async Task OpenAsync_LocalPage_LaysOutLogoImage()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);

        var document = await dom.OpenAsync(StartPageUrl());
        var root = new LayoutTreeBuilder().Build(document, 1024f);

        var images = FindImages(root).ToList();
        Assert.AreEqual(1, images.Count, "index.html 의 로고 <img> 가 정확히 하나여야 합니다.");

        var logo = images[0];
        Assert.IsNotNull(logo.ImageSource);
        StringAssert.EndsWith(logo.ImageSource!, "wisp-logo.png");
        StringAssert.StartsWith(logo.ImageSource!, "file:");

        // style.css 의 .logo { width: 280px } 가 적용되어야 한다.
        Assert.AreEqual(280f, logo.Width, 0.5f);
        Assert.IsTrue(logo.Height > 0f, "이미지 높이가 계산되지 않았습니다.");
    }

    private static IEnumerable<LayoutBox> FindImages(LayoutBox box)
    {
        if (!string.IsNullOrEmpty(box.ImageSource))
        {
            yield return box;
        }

        foreach (var child in box.Children)
        {
            foreach (var image in FindImages(child))
            {
                yield return image;
            }
        }
    }

    private static IEnumerable<string> CollectLinks(LayoutBox box)
    {
        if (!string.IsNullOrEmpty(box.LinkUrl))
        {
            yield return box.LinkUrl!;
        }

        foreach (var line in box.Lines)
        {
            foreach (var run in line.Runs)
            {
                if (!string.IsNullOrEmpty(run.LinkUrl))
                {
                    yield return run.LinkUrl!;
                }
            }
        }

        foreach (var child in box.Children)
        {
            foreach (var link in CollectLinks(child))
            {
                yield return link;
            }
        }
    }

    [TestMethod]
    public async Task OpenAsync_LocalPage_ResolvesAnchorLinksToAbsoluteUrls()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);

        var document = await dom.OpenAsync(StartPageUrl());
        var root = new LayoutTreeBuilder().Build(document, 1024f);

        var links = CollectLinks(root).ToList();

        // 텍스트 링크(예: Home -> index.html, About -> about.html)가 절대 URL로 해석되어야 한다.
        Assert.IsTrue(links.Any(u => u.EndsWith("index.html", StringComparison.OrdinalIgnoreCase)),
            "index.html 링크를 찾지 못했습니다: " + string.Join(", ", links));
        Assert.IsTrue(links.Any(u => u.EndsWith("about.html", StringComparison.OrdinalIgnoreCase)),
            "about.html 링크를 찾지 못했습니다: " + string.Join(", ", links));
        Assert.IsTrue(links.All(u => u.StartsWith("file:", StringComparison.OrdinalIgnoreCase)),
            "링크가 절대 URL로 해석되지 않았습니다: " + string.Join(", ", links));
    }

    private static IEnumerable<string> CollectText(LayoutBox box)
    {
        foreach (var line in box.Lines)
        {
            foreach (var run in line.Runs)
            {
                yield return run.Text;
            }
        }

        foreach (var child in box.Children)
        {
            foreach (var text in CollectText(child))
            {
                yield return text;
            }
        }
    }

    [TestMethod]
    public async Task OpenAsync_LineBreakDemo_BreaksDummyStringIntoLines()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);

        var document = await dom.OpenAsync(StartPageUrl());
        var root = new LayoutTreeBuilder().Build(document, 1024f);

        // 각 줄의 텍스트를 공백 없이 합쳐서 비교한다.
        var lines = CollectLines(root)
            .Select(line => string.Concat(line.Runs.Select(run => run.Text)))
            .ToList();
        var joined = string.Join(" | ", lines);

        // 텍스트 안의 개행이 각각 별도 줄로 나뉘어야 한다.
        Assert.IsTrue(lines.Any(t => t.StartsWith("첫번째줄")), joined);
        Assert.IsTrue(lines.Any(t => t.StartsWith("두번째줄")), joined);
        Assert.IsTrue(lines.Any(t => t.StartsWith("세번째줄")), joined);

        // <br> 도 줄을 나눈다.
        Assert.IsTrue(lines.Any(t => t.StartsWith("BR앞")), joined);
        Assert.IsTrue(lines.Any(t => t.StartsWith("BR뒤첫줄")), joined);
        Assert.IsTrue(lines.Any(t => t.StartsWith("빈줄뒤줄")), joined);
    }

    private static IEnumerable<LineBox> CollectLines(LayoutBox box)
    {
        foreach (var line in box.Lines)
        {
            yield return line;
        }

        foreach (var child in box.Children)
        {
            foreach (var line in CollectLines(child))
            {
                yield return line;
            }
        }
    }
}
