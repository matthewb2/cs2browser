using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace CS2Browser.Scripting;

/// <summary>
/// JS <c>window</c> ê°ì²´(ìµœì†Œ êµ¬í˜„). document/console/navigator ?€ ?€?´ë¨¸ë¥??¸ì¶œ?œë‹¤.
/// ?€?´ë¨¸??UI ?¤ë ˆ?œì˜ WinForms Timer ë¡??™ì‘?˜ë?ë¡?ë°˜ë“œ??UI ?¤ë ˆ?œì—???ì„±?´ì•¼ ?œë‹¤.
/// </summary>
[NoDefaultScriptAccess]
public sealed class WindowBridge
{
    private readonly Dictionary<int, WinFormsTimer> _timers = new();
    private readonly object _document;
    private readonly ConsoleBridge _console = new();
    private readonly NavigatorBridge _navigator = new();
    private int _nextTimerId = 1;

    public WindowBridge(object documentBridge) => _document = documentBridge;

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
    public int setTimeout(Action callback, int delay) => Schedule(callback, delay, repeat: false);

    [ScriptMember]
    public int setInterval(Action callback, int delay) => Schedule(callback, delay, repeat: true);

    [ScriptMember]
    public void clearTimeout(int id) => Cancel(id);

    [ScriptMember]
    public void clearInterval(int id) => Cancel(id);

    [ScriptMember]
    public void cancelAllTimers()
    {
        foreach (var id in _timers.Keys.ToArray())
        {
            Cancel(id);
        }
    }

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
                callback();
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
}
