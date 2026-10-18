using AngleSharp.Dom;
using Microsoft.ClearScript.V8;

namespace CS2Browser.Scripting;

/// <summary>
/// V8(ClearScript) 스크립트 엔진과 AngleSharp 문서를 연결하는 컨텍스트.
/// 전역 객체(document/window/console/navigator)를 주입하고 스크립트를 실행한다.
/// </summary>
/// <remarks>
/// 이 클래스는 아키텍처 뼈대이며, 브라우저 폴리필/이벤트 루프/전체 DOM 바인딩은 단계적으로 확장한다.
/// </remarks>
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
        var window = new WindowBridge(documentBridge);

        _engine.AddHostObject("window", window);
        _engine.AddHostObject("document", documentBridge);
        _engine.AddHostObject("console", window.console);
        _engine.AddHostObject("navigator", window.navigator);

        _engine.Execute("globalThis.self = globalThis;");
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
