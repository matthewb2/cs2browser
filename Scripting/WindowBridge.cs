using AngleSharp.Dom;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace CS2Browser.Scripting;

/// <summary>
/// JS <c>window</c> 객체(최소 구현). document/console/navigator, 타이머,
/// requestAnimationFrame/cancelAnimationFrame, getComputedStyle 를 노출한다.
/// 타이머는 UI 스레드의 WinForms Timer 로 동작하므로 반드시 UI 스레드에서 생성해야 한다.
/// </summary>
[NoDefaultScriptAccess]
public sealed class WindowBridge
{
    private readonly Dictionary<int, WinFormsTimer> _timers = new();
    private readonly List<(int Id, Action Callback)> _frameCallbacks = new();
    private readonly object _document;
    private readonly IDocument _angleSharpDocument;
    private readonly V8ScriptEngine _engine;
    private readonly ConsoleBridge _console = new();
    private readonly NavigatorBridge _navigator = new();
    private WinFormsTimer? _frameTimer;
    private int _nextTimerId = 1;
    private int _nextFrameId = 1;

    public WindowBridge(object documentBridge, IDocument angleSharpDocument, V8ScriptEngine engine)
    {
        _document = documentBridge;
        _angleSharpDocument = angleSharpDocument;
        _engine = engine;
    }

    [ScriptMember]
    public object document => _document;

    [ScriptMember]
    public object console => _console;

    [ScriptMember]
    public object navigator => _navigator;

    [ScriptMember]
    public int innerWidth => 1024;

    [ScriptMember]
    public int innerHeight => 768;

    [ScriptMember]
    public int setTimeout(object callback, int delay = 0) => Schedule(ToAction(callback), delay, repeat: false);

    [ScriptMember]
    public int setInterval(object callback, int delay = 0) => Schedule(ToAction(callback), delay, repeat: true);

    [ScriptMember]
    public void clearTimeout(int id) => Cancel(id);

    [ScriptMember]
    public void clearInterval(int id) => Cancel(id);

    [ScriptMember]
    public int requestAnimationFrame(object callback)
    {
        var action = ToAction(callback);

        var id = _nextFrameId++;
        _frameCallbacks.Add((id, action));
        EnsureFrameTimer();
        return id;
    }

    [ScriptMember]
    public void cancelAnimationFrame(int id)
    {
        var index = _frameCallbacks.FindIndex(c => c.Id == id);
        if (index >= 0)
        {
            _frameCallbacks.RemoveAt(index);
        }
    }

    /// <summary>
    /// 계산된 스타일을 반환한다. AngleSharp.Css 계산 중 첫 번째 기회 예외가 나면
    /// 인라인 스타일 브릿지로 대체해 렌더링이 중단되지 않게 한다.
    /// </summary>
    [ScriptMember]
    public object? getComputedStyle(object? element, object? pseudoElt = null)
    {
        if (V8DomFactory.Unwrap(element) is not IElement target)
        {
            return null;
        }

        var view = _angleSharpDocument.DefaultView;
        if (view is not null)
        {
            try
            {
                var computed = AngleSharp.Dom.WindowExtensions.GetComputedStyle(view, target, pseudoElt as string);
                if (computed is not null)
                {
                    return V8DomFactory.Wrap(computed, _engine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JS:getComputedStyle] {ex.Message}");
            }
        }

        return V8DomFactory.WrapInlineStyle(target, _engine);
    }

    [ScriptMember]
    public void cancelAllTimers()
    {
        foreach (var id in _timers.Keys.ToArray())
        {
            Cancel(id);
        }

        _frameCallbacks.Clear();
        _frameTimer?.Stop();
    }

    /// <summary>JS 함수(<see cref="ScriptObject"/>)는 자동으로 델리게이트로 변환되지 않으므로 직접 감싼다.</summary>
    private static Action ToAction(object callback) => callback switch
    {
        null => throw new ArgumentNullException(nameof(callback)),
        Action action => action,
        ScriptObject script => () => script.Invoke(false),
        _ => throw new ArgumentException("callback 은 함수여야 합니다.", nameof(callback))
    };

    private int Schedule(Action callback, int delay, bool repeat)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var id = _nextTimerId++;
        var timer = new WinFormsTimer { Interval = Math.Max(1, delay) };
        timer.Tick += (_, _) =>
        {
            if (!repeat)
            {
                Cancel(id);
            }

            try
            {
                V8DomContext.RunTask(_engine, callback);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JS:timer] {ex.Message}");
            }
        };

        _timers[id] = timer;
        timer.Start();
        return id;
    }

    private void Cancel(int id)
    {
        if (_timers.Remove(id, out var timer))
        {
            timer.Stop();
            timer.Dispose();
        }
    }

    /// <summary>rAF 콜백은 16ms 주기의 타이머로 한 번에 몰아 실행한다.</summary>
    private void EnsureFrameTimer()
    {
        if (_frameTimer is not null)
        {
            return;
        }

        _frameTimer = new WinFormsTimer { Interval = 16 };
        _frameTimer.Tick += (_, _) =>
        {
            var batch = _frameCallbacks.ToArray();
            _frameCallbacks.Clear();

            // 한 프레임의 rAF 콜백을 모두 실행한 뒤 한 번만 커밋한다.
            V8DomContext.RunTask(_engine, () =>
            {
                foreach (var (_, callback) in batch)
                {
                    try
                    {
                        callback();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[JS:rAF] {ex.Message}");
                    }
                }
            });

            if (_frameCallbacks.Count == 0)
            {
                _frameTimer!.Stop();
            }
        };

        _frameTimer.Start();
    }
}
