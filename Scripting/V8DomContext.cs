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

        _engine.AddHostObject("window", window);
        _engine.AddHostObject("document", documentBridge);
        _engine.AddHostObject("console", window.console);
        _engine.AddHostObject("navigator", window.navigator);

        _engine.Execute("globalThis.self = globalThis;");
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
    var fn = w[name];
    if (typeof fn !== 'function') return;
    global[name] = function () { return fn.apply(w, arguments); };
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
