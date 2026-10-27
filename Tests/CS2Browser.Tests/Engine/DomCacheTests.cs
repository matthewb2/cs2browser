using AngleSharp.Dom;
using CS2Browser.Engine;
using CS2Browser.Network;

namespace CS2Browser.Tests;

[TestClass]
public sealed class DomCacheTests
{
    private static async Task<DomManager> OpenAsync(string html)
    {
        var dom = new DomManager(new ResourceLoader());
        await dom.OpenHtmlAsync(html);
        return dom;
    }

    [TestMethod]
    public async Task Commit_AppliesCacheMutationsToOriginalOnlyAfterCommit()
    {
        var dom = await OpenAsync("<html><body><div id='d'>old</div></body></html>");
        var cache = dom.Cache!;

        cache.Document.GetElementById("d")!.TextContent = "new";
        cache.Document.GetElementById("d")!.SetAttribute("class", "x");

        Assert.AreEqual("old", dom.Document!.GetElementById("d")!.TextContent);
        Assert.IsTrue(cache.Commit());
        Assert.AreEqual("new", dom.Document.GetElementById("d")!.TextContent);
        Assert.AreEqual("x", dom.Document.GetElementById("d")!.GetAttribute("class"));
    }

    [TestMethod]
    public async Task Commit_WithoutChanges_ReturnsFalse()
    {
        var dom = await OpenAsync("<html><body><div id='d'>old</div></body></html>");

        Assert.IsFalse(dom.Cache!.Commit());
    }

    [TestMethod]
    public async Task Commit_KeepsUntouchedOriginalNodesAndAddsNewOnes()
    {
        var dom = await OpenAsync("<html><body><p id='keep'>keep</p></body></html>");
        var keep = dom.Document!.GetElementById("keep");
        var cacheDoc = dom.Cache!.Document;

        var added = cacheDoc.CreateElement("span");
        added.Id = "added";
        added.TextContent = "hi";
        cacheDoc.Body!.AppendChild(added);
        dom.Cache.Commit();

        Assert.AreSame(keep, dom.Document.GetElementById("keep"));
        Assert.AreEqual("hi", dom.Document.GetElementById("added")!.TextContent);
        Assert.AreNotSame(added, dom.Document.GetElementById("added"));
    }

    [TestMethod]
    public async Task Commit_MovesAndRemovesNodes()
    {
        var dom = await OpenAsync("<html><body><ul id='u'><li id='a'>A</li><li id='b'>B</li><li id='c'>C</li></ul></body></html>");
        var originalA = dom.Document!.GetElementById("a");
        var cacheDoc = dom.Cache!.Document;
        var list = cacheDoc.GetElementById("u")!;

        list.InsertBefore(cacheDoc.GetElementById("c")!, list.FirstChild);
        list.RemoveChild(cacheDoc.GetElementById("b")!);
        dom.Cache.Commit();

        var ids = dom.Document.GetElementById("u")!.Children.Select(e => e.Id).ToArray();
        CollectionAssert.AreEqual(new[] { "c", "a" }, ids);
        Assert.AreSame(originalA, dom.Document.GetElementById("a"));
    }

    [TestMethod]
    public async Task ScriptEngine_ReadsCacheAndCommitsToOriginal()
    {
        var dom = await OpenAsync("<html><body><div id='d'>old</div></body></html>");
        var script = new ScriptEngine();
        script.Attach(dom);

        script.Evaluate("document.getElementById('d').textContent = 'new';");

        Assert.AreEqual("new", dom.Cache!.Document.GetElementById("d")!.TextContent);
        Assert.AreEqual("new", dom.Document!.GetElementById("d")!.TextContent);
    }

    [TestMethod]
    public async Task BrowserEngine_RelayoutsAfterScriptMutation()
    {
        using var engine = new BrowserEngine();
        await engine.LoadHtmlAsync("<html><body><div id='root'></div></body></html>", 800f);
        var before = engine.LayoutRoot!.Height;
        var layouts = 0;
        engine.LayoutUpdated += (_, _) => layouts++;

        engine.ScriptEngine.Evaluate(
            """
            var root = document.getElementById('root');
            for (var i = 0; i < 10; i++) {
              var p = document.createElement('p');
              p.textContent = 'line ' + i;
              root.appendChild(p);
            }
            """);

        Assert.IsNull(engine.ScriptEngine.LastError);
        Assert.AreEqual(1, layouts);
        Assert.IsTrue(engine.LayoutRoot!.Height > before);
    }
}
