using System.Collections;
using System.Runtime.CompilerServices;
using AngleSharp.Css.Dom;
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
    /// <summary>엘리먼트별 인라인 스타일 브릿지 캐시.</summary>
    private static readonly ConditionalWeakTable<IElement, CSSStyleDeclarationBridge> InlineStyleCache = new();

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
            case IText text:
                return InstanceCache.GetOrCreate(text, t => (object)new TextBridge(t, engine));
            case IComment comment:
                return InstanceCache.GetOrCreate(comment, c => (object)new CommentBridge(c, engine));
            case ICharacterData characterData:
                return InstanceCache.GetOrCreate(characterData, c => (object)new CharacterDataBridge(c, engine));
            case IElement element:
                return InstanceCache.GetOrCreate(element, e => (object)new ElementBridge(e, engine));
            case INode node:
                return InstanceCache.GetOrCreate(node, n => (object)new NodeBridge(n, engine));
            case AngleSharp.Css.Dom.ICssStyleDeclaration style:
                return InstanceCache.GetOrCreate(style, s => (object)new CSSStyleDeclarationBridge(s, engine));
            case IEnumerable enumerable:
                return WrapList(enumerable, engine);
            default:
                return value;
        }
    }

    /// <summary>V8 브릿지(또는 배열)를 실제 AngleSharp 객체로 되돌린다.</summary>
    public static object? Unwrap(object? value) => BridgeRuntime.Unwrap(value);

    /// <summary>
    /// <c>element.style</c> / <c>getComputedStyle</c> 용 인라인 스타일 선언 브릿지를 반환한다.
    /// 같은 엘리먼트에 대해 항상 같은 브릿지를 돌려주어 <c>===</c> 비교가 성립하도록 한다.
    /// </summary>
    public static object? WrapInlineStyle(AngleSharp.Dom.IElement element, V8ScriptEngine engine)
    {
        var declaration = element.GetStyle();
        if (declaration is null)
        {
            return null;
        }

        return InlineStyleCache.GetValue(element, _ => new CSSStyleDeclarationBridge(declaration, engine));
    }

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
