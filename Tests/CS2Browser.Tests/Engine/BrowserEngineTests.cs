using AngleSharp.Dom;
using CS2Browser.Engine;

namespace CS2Browser.Tests;

[TestClass]
public sealed class BrowserEngineTests
{
    [TestMethod]
    public async Task LoadHtmlAsync_BuildsLayoutTree()
    {
        using var engine = new BrowserEngine();

        await engine.LoadHtmlAsync(TestDocuments.Simple, 800f);

        Assert.IsNotNull(engine.LayoutRoot);
        Assert.AreEqual("#viewport", engine.LayoutRoot!.TagName);
        Assert.AreEqual(800f, engine.LayoutRoot.Width, 0.01f);
        Assert.IsTrue(engine.LayoutRoot.Children.Count > 0);
    }

    [TestMethod]
    public async Task LoadHtmlAsync_RaisesLayoutAndTitleEvents()
    {
        using var engine = new BrowserEngine();
        var layoutRaised = false;
        string? title = null;
        engine.LayoutUpdated += (_, _) => layoutRaised = true;
        engine.TitleChanged += (_, _) => title = engine.Title;

        await engine.LoadHtmlAsync(TestDocuments.Simple, 800f);

        Assert.IsTrue(layoutRaised);
        Assert.AreEqual("Simple Page", title);
    }

    [TestMethod]
    public async Task LoadHtmlAsync_ExecutesInlineScript()
    {
        using var engine = new BrowserEngine();

        await engine.LoadHtmlAsync(TestDocuments.WithScript, 800f);

        var div = engine.DomManager.Document!.GetElementById("target");
        Assert.IsNotNull(div);
        Assert.AreEqual("after", div!.TextContent.Trim());
    }

    [TestMethod]
    public void BuildLayout_WithNullDocument_KeepsNoRoot()
    {
        using var engine = new BrowserEngine();

        engine.BuildLayout(800f);

        Assert.IsNull(engine.LayoutRoot);
    }
}
