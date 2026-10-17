using System.Diagnostics;

namespace CS2Browser.Diagnostics;

/// <summary>
/// 처리되지 않은 예외를 파일로 기록한다. 앱이 죽더라도 원인(스택)을 남겨
/// 다음 실행에서 분석할 수 있게 하는 최후의 방어선이다.
/// </summary>
public static class CrashLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CS2Browser",
        "crash.log");

    /// <summary>예외를 로그 파일과 디버그 출력에 기록한다. 로깅 자체는 예외를 던지지 않는다.</summary>
    public static void Log(Exception? exception, string source)
    {
        if (exception is null)
        {
            return;
        }

        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ({source}) {exception}{Environment.NewLine}{Environment.NewLine}";
        Debug.WriteLine(entry);

        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(LogPath, entry);
        }
        catch
        {
            // 로깅 실패는 무시한다(디스크 권한 등).
        }
    }
}
