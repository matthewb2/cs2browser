using AngleSharp.Dom;

namespace CS2Browser.Engine;

/// <summary>
/// V8 자바스크립트 엔진 연동.
/// 문서 로드 시 인라인 스크립트는 V8ScriptingService 에 의해 자동 실행되며,
/// 여기서는 임의의 스크립트 문자열을 직접 평가(evaluate)하는 기능을 제공한다.
/// </summary>
public sealed class ScriptEngine
{
    private DomManager? _domManager;

    public string? LastError { get; private set; }

    public void Attach(DomManager domManager) => _domManager = domManager;

    /// <summary>현재 문서 컨텍스트에서 스크립트를 실행한다.</summary>
    public object? Evaluate(string script)
    {
        var document = _domManager?.Document;
        if (document is null)
        {
            LastError = "문서가 로드되지 않았습니다.";
            return null;
        }

        try
        {
            LastError = null;
            var v8 = V8ScriptingService.GetOrCreateContext(document);
            return v8.Evaluate(script);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }
}
