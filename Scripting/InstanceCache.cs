using System.Runtime.CompilerServices;

namespace CS2Browser.Scripting;

/// <summary>
/// AngleSharp 객체와 V8 브릿지 객체의 동일성(identity)을 보장하는 약한 참조 캐시.
/// JS 에서 <c>a === b</c> 비교가 성립하려면 같은 AngleSharp 객체에 대해 항상 같은 브릿지를 반환해야 한다.
/// </summary>
public static class InstanceCache
{
    private static readonly ConditionalWeakTable<object, object> Cache = new();

    public static TWrapper GetOrCreate<TTarget, TWrapper>(TTarget target, Func<TTarget, TWrapper> factory)
        where TTarget : class
        where TWrapper : class
    {
        ArgumentNullException.ThrowIfNull(target);
        return (TWrapper)Cache.GetValue(target, key => factory((TTarget)key));
    }
}
