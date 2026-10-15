using CS2Browser.Engine;
using CS2Browser.Network;

namespace CS2Browser.Tests;

[TestClass]
public sealed class ScriptEngineTests
{
    [TestMethod]
    public async Task Evaluate_MutatesDocument()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync("<html><body><div id='d'>old</div></body></html>");

        var script = new ScriptEngine();
        script.Attach(dom);
        script.Evaluate("document.getElementById('d').textContent = 'new';");

        Assert.IsNull(script.LastError);
        Assert.AreEqual("new", dom.Document!.GetElementById("d")!.TextContent.Trim());
    }

    [TestMethod]
    public void Evaluate_WithoutDocument_ReportsError()
    {
        var script = new ScriptEngine();

        var result = script.Evaluate("1 + 1");

        Assert.IsNull(result);
        Assert.IsNotNull(script.LastError);
    }

    [TestMethod]
    public async Task Evaluate_CapturesRuntimeError()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync("<html><body></body></html>");

        var script = new ScriptEngine();
        script.Attach(dom);
        script.Evaluate("throw new Error('boom');");

        Assert.IsNotNull(script.LastError);
    }
}
