using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using CS2Browser.Scripting.Generated;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace CS2Browser.Scripting;

/// <summary>
/// JS <c>addEventListener</c>/<c>removeEventListener</c> 를 AngleSharp 이벤트 시스템에 연결하는 레지스트리.
/// 리스너는 V8 <see cref="ScriptObject"/> 로 보관하고, AngleSharp 가 이벤트를 발생시키면
/// 생성된 <see cref="EventBridge"/> 로 감싸 되돌려준다.
/// </summary>
public static class DomEventRegistry
{
    private sealed class Bucket
    {
        public List<ScriptObject> Listeners { get; } = new();

        public bool Hooked { get; set; }
    }

    // (이벤트 타깃, 이벤트 타입) -> 리스너 버킷. 약한 참조라 DOM 노드 GC 를 막지 않는다.
    private static readonly ConditionalWeakTable<object, Dictionary<string, Bucket>> Table = new();

    public static void AddListener(object target, string type, object? listener, V8ScriptEngine engine)
    {
        if (listener is not ScriptObject scriptListener)
        {
            return;
        }

        var buckets = Table.GetOrCreateValue(target);
        if (!buckets.TryGetValue(type, out var bucket))
        {
            bucket = new Bucket();
            buckets[type] = bucket;
        }

        if (bucket.Listeners.Contains(scriptListener))
        {
            return;
        }

        bucket.Listeners.Add(scriptListener);

        if (bucket.Hooked)
        {
            return;
        }

        bucket.Hooked = true;
        ((IEventTarget)target).AddEventListener(type, (_, ev) => Dispatch(buckets, type, ev, engine), false);
    }

    public static void RemoveListener(object target, string type, object? listener)
    {
        if (!Table.TryGetValue(target, out var buckets) || !buckets.TryGetValue(type, out var bucket))
        {
            return;
        }

        if (listener is ScriptObject scriptListener)
        {
            bucket.Listeners.Remove(scriptListener);
        }
    }

    private static void Dispatch(
        Dictionary<string, Bucket> buckets,
        string type,
        AngleSharp.Dom.Events.Event evt,
        V8ScriptEngine engine)
    {
        if (!buckets.TryGetValue(type, out var bucket) || bucket.Listeners.Count == 0)
        {
            return;
        }

        var bridge = new EventBridge(evt, engine);
        foreach (var listener in bucket.Listeners.ToArray())
        {
            try
            {
                listener.InvokeAsFunction(new object[] { bridge });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JS:listener {type}] {ex.Message}");
            }
        }
    }
}
