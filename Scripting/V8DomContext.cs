using AngleSharp.Dom;
using Microsoft.ClearScript.V8;

namespace CS2Browser.Scripting;

public sealed class V8DomContext : IDisposable
{
    private readonly V8ScriptEngine _engine;
    private bool _disposed;

    public V8DomContext(IDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Document = document;

        _engine = new V8ScriptEngine();
        var documentBridge = V8DomFactory.Wrap(document, _engine)!;
        var window = new WindowBridge(documentBridge, document, _engine);

        _engine.AddHostObject("__cs2HostWindow", window);
        _engine.AddHostObject("document", documentBridge);
        _engine.AddHostObject("console", window.console);
        _engine.AddHostObject("navigator", window.navigator);

        _engine.Execute("globalThis.self = globalThis;");
        InstallWindowProxy();
        InstallGlobalAliases();
        InstallConstructorStubs();
    }

    private void InstallGlobalAliases()
    {
        _engine.Execute(@"
(function (global) {
  var names = [
    'setTimeout', 'clearTimeout', 'setInterval', 'clearInterval',
    'requestAnimationFrame', 'cancelAnimationFrame', 'getComputedStyle'
  ];
  names.forEach(function (name) {
    var w = global.window;
    if (!w) return;
    if (!w[name]) return;
    // 호스트 메서드에는 Function.prototype.apply 가 없으므로 직접 호출한다.
    global[name] = function () { return w[name](...arguments); };
  });
})(globalThis);
");
    }

    private void InstallConstructorStubs()
    {
        _engine.Execute(@"
(function (global) {
  function illegal(name) {
    return function () { throw new TypeError(name + ' cannot be constructed directly'); };
  }
  global.Node = illegal('Node');
  global.Element = illegal('Element');
  global.HTMLElement = illegal('HTMLElement');
  global.HTMLInputElement = illegal('HTMLInputElement');
  global.HTMLTextAreaElement = illegal('HTMLTextAreaElement');
  global.Text = illegal('Text');
  global.Comment = illegal('Comment');
  global.DocumentFragment = illegal('DocumentFragment');
  global.Event = illegal('Event');
})(globalThis);
");
    }

    /// <summary>
    /// <c>window.X</c> 가 호스트 멤버에 없으면 전역(globalThis)으로 대체되도록 window 를 Proxy 로 감싼다.
    /// </summary>
    private void InstallWindowProxy()
    {
        _engine.Execute(@"
(function (global) {
  var host = global.__cs2HostWindow;
  delete global.__cs2HostWindow;
  global.window = new Proxy(host, {
    get: function (t, p) { var v = t[p]; return v === undefined ? global[p] : v; },
    set: function (t, p, v) { global[p] = v; return true; },
    has: function (t, p) { return p in t || p in global; }
  });
})(globalThis);
");
    }

    public IDocument Document { get; }
    public V8ScriptEngine Engine => _engine;
    public object? Evaluate(string script) => _engine.Evaluate(script);
    public void Execute(string script) => _engine.Execute(script);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _engine.Dispose();
    }
}
