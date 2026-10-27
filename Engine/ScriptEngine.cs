using AngleSharp.Dom;

namespace CS2Browser.Engine;

/// <summary>
/// V8 자바스크립트 엔진 연동.
/// 문서 로드 시 스크립트는 파싱이 끝난 뒤 V8ScriptingService 가 캐시 DOM 에서 실행하며,
/// 여기서는 임의의 스크립트 문자열을 별도 스레드에서 직접 평가(evaluate)하는 기능을 제공한다.
/// </summary>
public sealed class ScriptEngine
{
    private DomManager? _domManager;

    public string? LastError { get; private set; }

    public void Attach(DomManager domManager) => _domManager = domManager;

    /// <summary>현재 문서 컨텍스트에서 스크립트를 동기적으로 실행한다.</summary>
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

    /// <summary>현재 문서 컨텍스트에서 스크립트를 별도 스레드에서 비동기적으로 실행한다.</summary>
    public Task<object?> EvaluateAsync(string script, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Evaluate(script);
        }, cancellationToken);
    }
}
