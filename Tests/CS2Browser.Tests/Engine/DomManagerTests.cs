using AngleSharp.Dom;
using CS2Browser.Engine;
using CS2Browser.Network;

namespace CS2Browser.Tests;

[TestClass]
public sealed class DomManagerTests
{
    [TestMethod]
    public async Task OpenHtmlAsync_ParsesDocumentAndTitle()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);

        var document = await dom.OpenHtmlAsync(
            "<html><head><title>Test Title</title></head><body><p id='p'>hi</p></body></html>");

        Assert.AreEqual("Test Title", document.Title);
        Assert.IsNotNull(document.Body);
        Assert.AreEqual("p", document.GetElementById("p")?.LocalName);
        Assert.AreSame(document, dom.Document);
    }

    [TestMethod]
    public async Task OpenHtmlAsync_RaisesDocumentLoadedEvent()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        IDocument? loaded = null;
        dom.DocumentLoaded += (_, d) => loaded = d;

        var document = await dom.OpenHtmlAsync("<html><body>ok</body></html>");

        Assert.IsNotNull(loaded);
        Assert.AreSame(document, loaded);
    }
}
