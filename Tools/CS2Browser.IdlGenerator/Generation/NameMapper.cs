using System.Text;

namespace CS2Browser.IdlGenerator.Generation;

/// <summary>IDL 식별자를 C# 식별자로 변환하는 유틸리티.</summary>
public static class NameMapper
{
    /// <summary>IDL 멤버 이름을 PascalCase C# 속성/메서드 이름으로 변환한다(첫 글자만 대문자).</summary>
    public static string ToPascal(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        // 하이픈은 제거하고 다음 글자를 대문자로 한다.
        var sb = new StringBuilder(name.Length);
        var upperNext = true;
        foreach (var c in name)
        {
            if (c == '-')
            {
                upperNext = true;
                continue;
            }

            sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }

        return sb.ToString();
    }

    /// <summary>C# 예약어와 충돌하면 이름 앞에 @를 붙인다.</summary>
    public static string Escape(string name) => IsKeyword(name) ? "@" + name : name;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };

    private static bool IsKeyword(string name) => Keywords.Contains(name);
}
