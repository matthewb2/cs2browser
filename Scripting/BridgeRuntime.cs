using System.Collections;
using System.Globalization;
using System.Reflection;
using Microsoft.ClearScript.V8;

namespace CS2Browser.Scripting;

/// <summary>
/// Web IDL 의 이름을 AngleSharp 객체 멤버로 런타임에 연결하는 디스패처.
/// 정적으로 매핑되지 않은 멤버(생성된 브릿지의 Get/Set/Call)가 리플렉션으로 위임한다.
/// </summary>
public static class BridgeRuntime
{
    public static object? GetProperty(object target, string name, V8ScriptEngine engine)
    {
        var property = FindProperty(target.GetType(), name);
        if (property is null || !property.CanRead)
        {
            return null;
        }

        return V8DomFactory.Wrap(property.GetValue(target), engine);
    }

    public static void SetProperty(object target, string name, object? value)
    {
        var property = FindProperty(target.GetType(), name);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        property.SetValue(target, ConvertArgument(value, property.PropertyType));
    }

    public static object? Invoke(object target, string name, V8ScriptEngine engine, object?[] args)
    {
        foreach (var method in FindMethods(target.GetType(), name, args.Length))
        {
            var parameters = method.GetParameters();
            var converted = new object?[parameters.Length];

            if (parameters.Length == 1 && args.Length != 1
                && parameters[0].GetCustomAttribute<ParamArrayAttribute>() is not null)
            {
                var elementType = parameters[0].ParameterType.GetElementType()!;
                var array = Array.CreateInstance(elementType, args.Length);
                for (var i = 0; i < args.Length; i++)
                {
                    array.SetValue(ConvertArgument(args[i], elementType), i);
                }

                converted[0] = array;
            }
            else
            {
                for (var i = 0; i < args.Length; i++)
                {
                    converted[i] = ConvertArgument(args[i], parameters[i].ParameterType);
                }
            }

            object? result;
            try
            {
                result = method.Invoke(target, converted);
            }
            catch (TargetParameterCountException)
            {
                continue;
            }
            catch (ArgumentException)
            {
                continue;
            }

            return V8DomFactory.Wrap(result, engine);
        }

        return null;
    }

    /// <summary>브릿지 객체/배열을 감싸고 있는 실제 AngleSharp 객체로 되돌린다.</summary>
    public static object? Unwrap(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case DomBridgeBase bridge:
                return bridge.DomTarget;
            case string:
                return value;
            case IEnumerable enumerable when value is not string:
                var items = new List<object?>();
                foreach (var item in enumerable)
                {
                    items.Add(Unwrap(item));
                }

                return items.ToArray();
            default:
                return value;
        }
    }

    private static PropertyInfo? FindProperty(Type type, string name)
        => type.GetProperty(
            name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

    private static IEnumerable<MethodInfo> FindMethods(Type type, string name, int argCount)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!string.Equals(method.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == argCount)
            {
                yield return method;
            }
            else if (parameters.Length == 1
                && parameters[0].GetCustomAttribute<ParamArrayAttribute>() is not null
                && argCount != 1)
            {
                yield return method;
            }
        }
    }

    private static object? ConvertArgument(object? value, Type targetType)
    {
        value = Unwrap(value);

        if (value is null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }
        catch (Exception) when (value is IConvertible)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return value;
        }
    }
}
