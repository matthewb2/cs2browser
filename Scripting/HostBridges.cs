using System.Diagnostics;
using Microsoft.ClearScript;

namespace CS2Browser.Scripting;

/// <summary>JS <c>console</c> 객체. 디버그 출력으로 연결한다.</summary>
[NoDefaultScriptAccess]
public sealed class ConsoleBridge
{
    [ScriptMember]
    public void log(object? message) => Write("log", message);

    [ScriptMember]
    public void info(object? message) => Write("info", message);

    [ScriptMember]
    public void warn(object? message) => Write("warn", message);

    [ScriptMember]
    public void error(object? message) => Write("error", message);

    [ScriptMember]
    public void debug(object? message) => Write("debug", message);

    private static void Write(string level, object? message)
        => Debug.WriteLine($"[JS:{level}] {message}");
}

/// <summary>JS <c>navigator</c> 객체(최소 구현).</summary>
[NoDefaultScriptAccess]
public sealed class NavigatorBridge
{
    [ScriptMember]
    public string userAgent => "Mozilla/5.0 (Windows NT 10.0; Win64; x64) CS2Browser/0.1";

    [ScriptMember]
    public string platform => "Win32";

    [ScriptMember]
    public string language => "en-US";

    [ScriptMember]
    public int hardwareConcurrency => Environment.ProcessorCount;
}
