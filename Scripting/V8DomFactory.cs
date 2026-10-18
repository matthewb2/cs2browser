using System.Collections;
using AngleSharp.Dom;
using CS2Browser.Scripting.Generated;
using Microsoft.ClearScript.V8;

namespace CS2Browser.Scripting;

/// <summary>
/// AngleSharp 객체를 V8 브릿지로 감싸고, 반대 방향(브릿지 → AngleSharp) 변환을 담당하는 팩토리.
/// 같은 객체에는 <see cref="InstanceCache"/> 를 통해 항상 같은 브릿지를 반환한다.
/// </summary>
public static class V8DomFactory
{
    /// <summary>AngleSharp 객체/값을 V8 에 노출 가능한 형태로 감싼다.</summary>
    public static object? Wrap(object? value, V8ScriptEngine engine)
    {
        switch (value)
        {
            case null:
                return null;
            case DomBridgeBase:
                return value;
            case string:
                return value;
            case IDocument document:
                return InstanceCache.GetOrCreate(document, d => (object)new DocumentBridge(d, engine));
            case IElement element:
                return InstanceCache.GetOrCreate(element, e => (object)new ElementBridge(e, engine));
            case INode node:
                return InstanceCache.GetOrCreate(node, n => (object)new NodeBridge(n, engine));
            case IEnumerable enumerable:
                return WrapList(enumerable, engine);
            default:
                return value;
        }
    }

    /// <summary>V8 브릿지(또는 배열)를 실제 AngleSharp 객체로 되돌린다.</summary>
    public static object? Unwrap(object? value) => BridgeRuntime.Unwrap(value);

    private static object?[] WrapList(IEnumerable source, V8ScriptEngine engine)
    {
        var items = new List<object?>();
        foreach (var item in source)
        {
            items.Add(Wrap(item, engine));
        }

        return items.ToArray();
    }
}
