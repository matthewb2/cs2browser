using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using AngleSharp.Dom.Events;
using CS2Browser.Engine;
using CS2Browser.Scripting.Generated;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace CS2Browser.Scripting;

/// <summary>
/// JS <c>addEventListener</c>/<c>removeEventListener</c> 를 AngleSharp 이벤트 시스템에 연결하는 레지스트리.
/// 리스너는 V8 <see cref="ScriptObject"/> 로 보관하고, AngleSharp 가 이벤트를 발생시키면
/// 생성된 <see cref="EventBridge"/> 로 감싸 되돌려준다.
/// 리스너는 캐시 DOM 노드에 등록되며, 대응하는 원본 노드에서 발생한 이벤트는 캐시 노드로 전달된다.
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

    // 원본 노드 -> 캐시 노드로 전달 중인 이벤트 타입.
    private static readonly ConditionalWeakTable<INode, HashSet<string>> ForwardedTypes = new();

    // 같은 원본 이벤트가 버블링 중 여러 번 전달되지 않도록 표시한다.
    private static readonly ConditionalWeakTable<Event, object> ForwardedEvents = new();

    private static readonly ConditionalWeakTable<DomCache, object> SubscribedCaches = new();

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
        ForwardFromOriginal(target, type);
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
        Event evt,
        V8ScriptEngine engine)
    {
        if (!buckets.TryGetValue(type, out var bucket) || bucket.Listeners.Count == 0)
        {
            return;
        }

        var bridge = new EventBridge(evt, engine);
        V8DomContext.RunTask(engine, () =>
        {
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
        });
    }

    /// <summary>캐시 노드에 리스너가 생기면 대응하는 원본 노드의 이벤트를 캐시 노드로 전달하도록 연결한다.</summary>
    private static void ForwardFromOriginal(object target, string type)
    {
        if (target is not INode node)
        {
            return;
        }

        var document = node as IDocument ?? node.Owner;
        if (document is null || !DomCache.TryGet(document, out var cache) || !ReferenceEquals(cache.Document, document))
        {
            return;
        }

        EnsureSubscribed(cache);
        if (cache.GetOriginal(node) is { } original)
        {
            HookOriginal(cache, original, type);
        }
    }

    /// <summary>나중에 원본에 동기화되는 노드도 이벤트 전달이 연결되도록 캐시의 매핑 이벤트를 구독한다.</summary>
    private static void EnsureSubscribed(DomCache cache)
    {
        if (SubscribedCaches.TryGetValue(cache, out _))
        {
            return;
        }

        SubscribedCaches.AddOrUpdate(cache, new object());
        cache.NodeMapped += (cached, original) =>
        {
            if (Table.TryGetValue(cached, out var buckets))
            {
                foreach (var type in buckets.Keys)
                {
                    HookOriginal(cache, original, type);
                }
            }
        };
    }

    private static void HookOriginal(DomCache cache, INode original, string type)
    {
        var types = ForwardedTypes.GetOrCreateValue(original);
        if (!types.Add(type))
        {
            return;
        }

        ((IEventTarget)original).AddEventListener(type, (_, ev) => Forward(cache, original, ev), false);
    }

    /// <summary>원본 문서에서 발생한 이벤트를 대응하는 캐시 노드에서 다시 발생시킨다.</summary>
    private static void Forward(DomCache cache, INode hooked, Event evt)
    {
        if (ForwardedEvents.TryGetValue(evt, out _))
        {
            return;
        }

        ForwardedEvents.AddOrUpdate(evt, new object());

        var source = evt.OriginalTarget as INode;
        var target = (source is null ? null : cache.GetCached(source)) ?? cache.GetCached(hooked);
        if (target is not IEventTarget cacheTarget)
        {
            return;
        }

        var copy = cache.Document.CreateEvent("Event");
        copy.Init(evt.Type, evt.IsBubbling, evt.IsCancelable);
        cacheTarget.Dispatch(copy);

        if (copy.IsDefaultPrevented)
        {
            evt.Cancel();
        }
    }
}
