using CS2Browser.IdlGenerator.Model;

namespace CS2Browser.IdlGenerator.Generation;

/// <summary>Web IDL 타입 → C# 타입 변환기.</summary>
public static class TypeMapper
{
    private static readonly Dictionary<string, string> PrimitiveMap = new(StringComparer.Ordinal)
    {
        ["DOMString"] = "string",
        ["ByteString"] = "string",
        ["USVString"] = "string",
        ["boolean"] = "bool",
        ["byte"] = "sbyte",
        ["octet"] = "byte",
        ["short"] = "short",
        ["unsigned short"] = "ushort",
        ["long"] = "int",
        ["unsigned long"] = "uint",
        ["long long"] = "long",
        ["unsigned long long"] = "ulong",
        ["float"] = "float",
        ["unrestricted float"] = "float",
        ["double"] = "double",
        ["unrestricted double"] = "double",
        ["any"] = "object?",
        ["object"] = "object?",
        ["symbol"] = "object?",
        ["bigint"] = "long"
    };

    private static readonly HashSet<string> ValueTypes = new(StringComparer.Ordinal)
    {
        "bool", "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double"
    };

    /// <summary>IDL 타입을 C# 타입 이름으로 변환한다.</summary>
    public static string Map(IdlType type)
    {
        var name = MapCore(type);
        if (type.Nullable && ValueTypes.Contains(name))
        {
            name += "?";
        }

        return name;
    }

    public static bool IsValueType(string csharpType) => ValueTypes.Contains(csharpType);

    private static string MapCore(IdlType type)
    {
        if (type.IsUnion)
        {
            return "object?";
        }

        switch (type.Name)
        {
            case "sequence":
            case "FrozenArray":
            case "ObservableArray":
                return type.Parameters.Count > 0
                    ? $"IReadOnlyList<{Map(type.Parameters[0])}>"
                    : "IReadOnlyList<object?>";

            case "Promise":
                return type.Parameters.Count > 0
                    ? $"Task<{Map(type.Parameters[0])}>"
                    : "Task";

            case "record":
                return type.Parameters.Count > 1
                    ? $"IReadOnlyDictionary<{Map(type.Parameters[0])}, {Map(type.Parameters[1])}>"
                    : "IReadOnlyDictionary<string, object?>";

            case "void":
                return "void";
        }

        return PrimitiveMap.TryGetValue(type.Name, out var mapped) ? mapped : "object?";
    }

    /// <summary>IDL 타입이 AngleSharp DOM 노드(래핑 대상)인지 여부.</summary>
    public static bool IsDomNodeType(IdlType type)
        => !type.IsUnion
            && type.Parameters.Count == 0
            && type.Name is not ("DOMString" or "ByteString" or "USVString" or "boolean" or "double" or "float" or "long" or "short" or "byte" or "octet" or "any" or "object" or "void")
            && !PrimitiveMap.ContainsKey(type.Name);
}
