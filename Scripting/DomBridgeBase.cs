using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace CS2Browser.Scripting;

/// <summary>
/// 생성된 모든 브릿지 클래스의 기반 클래스.
/// AngleSharp 객체(<see cref="DomTarget"/>)와 V8 엔진(<see cref="DomEngine"/>)을 보관하고,
/// 런타임 디스패처(Get/Set/Call)와 래핑(Wrap) 헬퍼를 제공한다.
/// </summary>
/// <remarks>
/// ClearScript 7.5 에서는 <c>ScriptableObject</c> 기반 클래스가 제거되었으므로,
/// <see cref="NoDefaultScriptAccessAttribute"/> 로 기본 노출을 차단하고
/// 개별 멤버에 <see cref="ScriptMemberAttribute"/> 를 지정해 노출한다.
/// </remarks>
[NoDefaultScriptAccess]
public abstract class DomBridgeBase
{
    protected DomBridgeBase(object target, V8ScriptEngine engine)
    {
        DomTarget = target ?? throw new ArgumentNullException(nameof(target));
        DomEngine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>이 브릿지가 감싸는 실제 AngleSharp 객체(스크립트에 노출하지 않음).</summary>
    internal object DomTarget { get; }

    /// <summary>브릿지가 속한 V8 엔진(스크립트에 노출하지 않음).</summary>
    internal V8ScriptEngine DomEngine { get; }

    protected object? Get(string name) => BridgeRuntime.GetProperty(DomTarget, name, DomEngine);

    protected void Set(string name, object? value) => BridgeRuntime.SetProperty(DomTarget, name, value);

    protected object? Call(string name, params object?[] args) => BridgeRuntime.Invoke(DomTarget, name, DomEngine, args);

    protected object? Wrap(object? value) => V8DomFactory.Wrap(value, DomEngine);
}
