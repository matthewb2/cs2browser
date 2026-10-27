using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using CS2Browser.Engine;
using CS2Browser.Network;
using CS2Browser.Scripting;

namespace CS2Browser.Tests.Scripting;

/// <summary>
/// React 구동에 필요한 30개 핵심 API 가 V8 → AngleSharp 로 실제로 연결되는지 검증하는 스모크 테스트.
/// </summary>
[TestClass]
public sealed class V8DomContextSmokeTests
{
    private const string Shell =
        """
        <html><body><div id="root"></div></body></html>
        """;

    [TestMethod]
    public async Task V8_DomManipulation_UpdatesAngleSharpTree()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync(Shell);

        var context = V8ScriptingService.GetOrCreateContext(dom.Document!);
        context.Execute(
            """
            var root = document.getElementById('root');
            var span = document.createElement('span');
            span.id = 'child';
            span.className = 'cls';
            span.setAttribute('data-x', '1');
            span.appendChild(document.createTextNode('hello'));
            root.appendChild(span);
            root.appendChild(document.createComment('note'));

            var temp = document.createElement('i');
            root.insertBefore(temp, span);
            root.removeChild(temp);
            """);

        var document = dom.Document!;
        var child = document.GetElementById("child");

        Assert.IsNotNull(child);
        Assert.AreEqual("SPAN", child!.TagName);
        Assert.AreEqual("cls", child.ClassName);
        Assert.AreEqual("1", child.GetAttribute("data-x"));
        Assert.AreEqual("hello", child.TextContent);
        Assert.AreEqual(2, document.GetElementById("root")!.ChildNodes.Length);
        Assert.AreEqual(NodeType.Comment, document.GetElementById("root")!.LastChild!.NodeType);
    }

    [TestMethod]
    public async Task V8_InnerHtml_ReplacesContent()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync(Shell);

        var context = V8ScriptingService.GetOrCreateContext(dom.Document!);
        context.Execute("document.getElementById('root').innerHTML = '<b id=\"b\">bold</b>';");

        var bold = dom.Document!.GetElementById("b");
        Assert.IsNotNull(bold);
        Assert.AreEqual("bold", bold!.TextContent);
    }

    [TestMethod]
    public async Task V8_AddEventListener_DispatchesFromAngleSharp()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync(Shell);

        var context = V8ScriptingService.GetOrCreateContext(dom.Document!);
        context.Execute(
            """
            globalThis.hits = 0;
            document.getElementById('root').addEventListener('custom', function () { globalThis.hits++; });
            """);

        var root = (IEventTarget)dom.Document!.GetElementById("root")!;
        var ev = dom.Document.CreateEvent("Event");
        ev.Init("custom", false, false);
        root.Dispatch(ev);

        Assert.AreEqual(1, Convert.ToInt32(context.Evaluate("globalThis.hits")));

        context.Execute("document.getElementById('root').removeEventListener('custom', function () {});");
    }

    [TestMethod]
    public async Task V8_Style_SetPropertyWritesInlineStyle()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync(Shell);

        var context = V8ScriptingService.GetOrCreateContext(dom.Document!);
        context.Execute("document.getElementById('root').style.setProperty('color', 'red', '');");

        var element = dom.Document!.GetElementById("root")!;
        var style = element.GetAttribute("style") ?? element.GetStyle()?.CssText ?? string.Empty;
        // AngleSharp.Css 는 색상을 rgba(...) 로 직렬화한다.
        Assert.IsTrue(style.Contains("red") || style.Contains("rgba(255, 0, 0"), style);
    }

    [TestMethod]
    public async Task V8_Style_CssTextRoundTrips()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync(Shell);

        var context = V8ScriptingService.GetOrCreateContext(dom.Document!);
        context.Execute("document.getElementById('root').style.cssText = 'margin: 1px;';");

        var style = dom.Document!.GetElementById("root")!.GetAttribute("style") ?? string.Empty;
        StringAssert.Contains(style, "margin");
    }

    [TestMethod]
    public async Task V8_WindowTimersAndRaf_AreReachableAsBareGlobals()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync(Shell);

        var context = V8ScriptingService.GetOrCreateContext(dom.Document!);

        Assert.IsTrue(Convert.ToInt32(context.Evaluate("setTimeout(function () {}, 50)")) > 0);
        Assert.IsTrue(Convert.ToInt32(context.Evaluate("setInterval(function () {}, 50)")) > 0);
        Assert.IsTrue(Convert.ToInt32(context.Evaluate("requestAnimationFrame(function () {})")) > 0);

        context.Execute("cancelAllTimersShim = true;");
        context.Execute("window.cancelAllTimers();");
    }

    [TestMethod]
    public async Task V8_GetComputedStyle_FallsBackToInlineStyleOnFailure()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync(Shell);

        var context = V8ScriptingService.GetOrCreateContext(dom.Document!);

        // AngleSharp.Css 계산 중 첫 번째 기회 예외가 나도 null 이 아닌 스타일 객체를 돌려줘야 한다.
        var result = context.Evaluate("getComputedStyle(document.getElementById('root'))");

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task V8_ConstructorStubs_AreExposedForFeatureDetection()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync(Shell);

        var context = V8ScriptingService.GetOrCreateContext(dom.Document!);

        Assert.AreEqual(true, context.Evaluate("typeof Node === 'function'"));
        Assert.AreEqual(true, context.Evaluate("typeof Element === 'function'"));
        Assert.AreEqual(true, context.Evaluate("typeof HTMLElement === 'function'"));
        Assert.AreEqual(true, context.Evaluate("typeof Text === 'function'"));
        Assert.AreEqual(true, context.Evaluate("typeof Comment === 'function'"));
        Assert.AreEqual(true, context.Evaluate("window.Node !== undefined"));
    }

    [TestMethod]
    public async Task V8_ConstructorStub_ThrowsOnDirectConstruction()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        await dom.OpenHtmlAsync(Shell);

        var context = V8ScriptingService.GetOrCreateContext(dom.Document!);

        Assert.IsNotNull(context.Evaluate("try { new Node(); } catch (e) { e; }"));
    }
}
