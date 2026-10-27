namespace CS2Browser.IdlGenerator.Generation;

public enum StaticKind
{
    Property,
    Method
}

/// <summary>
/// 알려진 DOM 멤버에 대해 AngleSharp 객체로 직접 연결하는 정적 바인딩 정의.
/// <see cref="Body"/> 는 메서드 본문 표현식 또는 속성 getter 표현식이며,
/// 반환값이 DOM 노드인 경우 <c>Wrap(...)</c> 로 감싼다.
/// </summary>
public sealed record StaticBinding(
    StaticKind Kind,
    string ReturnType,
    string Body,
    string? SetBody = null,
    string[]? Parameters = null);

/// <summary>
/// 자주 쓰이는 DOM 멤버를 AngleSharp API 로 직접 매핑하는 테이블.
/// 여기에 없는 멤버는 런타임 디스패처(BridgeRuntime)로 위임한다.
/// </summary>
public static class StaticBindingMap
{
    private static readonly Dictionary<string, StaticBinding> Bindings = new(StringComparer.Ordinal)
    {
        // ---------------------------------------------------------- EventTarget
        // 리스너는 V8 ScriptObject 이므로 AngleSharp 이벤트 시스템과 연결하려면 레지스트리가 필요하다.
        ["EventTarget.addEventListener"] = new(StaticKind.Method, "void", "DomEventRegistry.AddListener(DomTarget, type, listener, DomEngine);", null, new[] { "type", "listener" }),
        ["EventTarget.removeEventListener"] = new(StaticKind.Method, "void", "DomEventRegistry.RemoveListener(DomTarget, type, listener);", null, new[] { "type", "listener" }),

        // ---------------------------------------------------------------- Node
        ["Node.nodeType"] = new(StaticKind.Property, "ushort", "(ushort)((AngleSharp.Dom.INode)DomTarget).NodeType"),
        ["Node.nodeName"] = new(StaticKind.Property, "string", "((AngleSharp.Dom.INode)DomTarget).NodeName"),
        ["Node.nodeValue"] = new(StaticKind.Property, "string?", "((AngleSharp.Dom.INode)DomTarget).NodeValue", "((AngleSharp.Dom.INode)DomTarget).NodeValue = value!;"),
        ["Node.textContent"] = new(StaticKind.Property, "string?", "((AngleSharp.Dom.INode)DomTarget).TextContent", "((AngleSharp.Dom.INode)DomTarget).TextContent = value!;"),
        ["Node.parentNode"] = new(StaticKind.Property, "object?", "Wrap(((AngleSharp.Dom.INode)DomTarget).Parent)"),
        ["Node.firstChild"] = new(StaticKind.Property, "object?", "Wrap(((AngleSharp.Dom.INode)DomTarget).FirstChild)"),
        ["Node.lastChild"] = new(StaticKind.Property, "object?", "Wrap(((AngleSharp.Dom.INode)DomTarget).LastChild)"),
        ["Node.nextSibling"] = new(StaticKind.Property, "object?", "Wrap(((AngleSharp.Dom.INode)DomTarget).NextSibling)"),
        ["Node.previousSibling"] = new(StaticKind.Property, "object?", "Wrap(((AngleSharp.Dom.INode)DomTarget).PreviousSibling)"),
        ["Node.childNodes"] = new(StaticKind.Property, "object?", "Wrap(((AngleSharp.Dom.INode)DomTarget).ChildNodes)"),
        ["Node.appendChild"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.INode)DomTarget).AppendChild((AngleSharp.Dom.INode)V8DomFactory.Unwrap(node)!))", null, new[] { "node" }),
        ["Node.removeChild"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.INode)DomTarget).RemoveChild((AngleSharp.Dom.INode)V8DomFactory.Unwrap(child)!))", null, new[] { "child" }),
        ["Node.insertBefore"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.INode)DomTarget).InsertBefore((AngleSharp.Dom.INode)V8DomFactory.Unwrap(newChild)!, (AngleSharp.Dom.INode)V8DomFactory.Unwrap(refChild)!))", null, new[] { "newChild", "refChild" }),
        ["Node.contains"] = new(StaticKind.Method, "bool", "((AngleSharp.Dom.INode)DomTarget).Contains((AngleSharp.Dom.INode)V8DomFactory.Unwrap(other)!)", null, new[] { "other" }),

        // -------------------------------------------------------- CharacterData
        ["CharacterData.data"] = new(StaticKind.Property, "string", "((AngleSharp.Dom.ICharacterData)DomTarget).Data", "((AngleSharp.Dom.ICharacterData)DomTarget).Data = value!;"),
        ["CharacterData.length"] = new(StaticKind.Property, "uint", "(uint)((AngleSharp.Dom.ICharacterData)DomTarget).Length"),

        // ---------------------------------------------------------------- Element
        ["Element.tagName"] = new(StaticKind.Property, "string", "((AngleSharp.Dom.IElement)DomTarget).TagName"),
        ["Element.localName"] = new(StaticKind.Property, "string", "((AngleSharp.Dom.IElement)DomTarget).LocalName"),
        ["Element.id"] = new(StaticKind.Property, "string", "((AngleSharp.Dom.IElement)DomTarget).Id!", "((AngleSharp.Dom.IElement)DomTarget).Id = value;"),
        ["Element.className"] = new(StaticKind.Property, "string", "((AngleSharp.Dom.IElement)DomTarget).ClassName!", "((AngleSharp.Dom.IElement)DomTarget).ClassName = value;"),
        ["Element.innerHTML"] = new(StaticKind.Property, "string?", "((AngleSharp.Dom.IElement)DomTarget).InnerHtml", "((AngleSharp.Dom.IElement)DomTarget).InnerHtml = value!;"),
        ["Element.outerHTML"] = new(StaticKind.Property, "string?", "((AngleSharp.Dom.IElement)DomTarget).OuterHtml", "((AngleSharp.Dom.IElement)DomTarget).OuterHtml = value!;"),
        // element.style 은 AngleSharp.Css 의 인라인 스타일 선언을 감싼 CSSStyleDeclarationBridge 로 노출한다.
        ["Element.style"] = new(StaticKind.Property, "object?", "V8DomFactory.WrapInlineStyle((AngleSharp.Dom.IElement)DomTarget, DomEngine)"),
        ["Element.children"] = new(StaticKind.Property, "object?", "Wrap(((AngleSharp.Dom.IElement)DomTarget).Children)"),
        ["Element.getAttribute"] = new(StaticKind.Method, "string?", "((AngleSharp.Dom.IElement)DomTarget).GetAttribute(qualifiedName)", null, new[] { "qualifiedName" }),
        ["Element.setAttribute"] = new(StaticKind.Method, "void", "((AngleSharp.Dom.IElement)DomTarget).SetAttribute(qualifiedName, value);", null, new[] { "qualifiedName", "value" }),
        ["Element.removeAttribute"] = new(StaticKind.Method, "void", "((AngleSharp.Dom.IElement)DomTarget).RemoveAttribute(qualifiedName);", null, new[] { "qualifiedName" }),
        ["Element.hasAttribute"] = new(StaticKind.Method, "bool", "((AngleSharp.Dom.IElement)DomTarget).HasAttribute(qualifiedName)", null, new[] { "qualifiedName" }),
        ["Element.querySelector"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.IElement)DomTarget).QuerySelector(selectors))", null, new[] { "selectors" }),
        ["Element.querySelectorAll"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.IElement)DomTarget).QuerySelectorAll(selectors))", null, new[] { "selectors" }),

        // ---------------------------------------------------------------- Document
        ["Document.title"] = new(StaticKind.Property, "string", "((AngleSharp.Dom.IDocument)DomTarget).Title!", "((AngleSharp.Dom.IDocument)DomTarget).Title = value;"),
        ["Document.body"] = new(StaticKind.Property, "object?", "Wrap(((AngleSharp.Dom.IDocument)DomTarget).Body)"),
        ["Document.documentElement"] = new(StaticKind.Property, "object?", "Wrap(((AngleSharp.Dom.IDocument)DomTarget).DocumentElement)"),
        ["Document.getElementById"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.IDocument)DomTarget).GetElementById(elementId))", null, new[] { "elementId" }),
        ["Document.createElement"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.IDocument)DomTarget).CreateElement(localName))", null, new[] { "localName" }),
        ["Document.createTextNode"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.IDocument)DomTarget).CreateTextNode(data))", null, new[] { "data" }),
        ["Document.createComment"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.IDocument)DomTarget).CreateComment(data))", null, new[] { "data" }),
        ["Document.querySelector"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.IDocument)DomTarget).QuerySelector(selectors))", null, new[] { "selectors" }),
        ["Document.querySelectorAll"] = new(StaticKind.Method, "object?", "Wrap(((AngleSharp.Dom.IDocument)DomTarget).QuerySelectorAll(selectors))", null, new[] { "selectors" }),

        // ------------------------------------------------- CSSStyleDeclaration
        // AngleSharp.Css 는 priority 가 "" 이면 값을 무시하므로, 브라우저처럼 일반 우선순위(null)로 취급한다.
        ["CSSStyleDeclaration.setProperty"] = new(StaticKind.Method, "void", "((AngleSharp.Css.Dom.ICssStyleDeclaration)DomTarget).SetProperty(property, value, string.IsNullOrEmpty(priority) ? null : priority);", null, new[] { "property", "value", "priority" })
    };

    public static bool TryGet(string interfaceName, string memberName, out StaticBinding binding)
        => Bindings.TryGetValue(interfaceName + "." + memberName, out binding!);

    public static bool HasInterface(string interfaceName)
        => Bindings.Keys.Any(key => key.StartsWith(interfaceName + ".", StringComparison.Ordinal));
}
