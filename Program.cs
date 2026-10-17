using CS2Browser.Diagnostics;
using CS2Browser.UI;

namespace CS2Browser;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        // 방어 코드: 어떤 경로에서든 처리되지 않은 예외가 발생해도 앱이 조용히 죽지 않도록
        // 기록하고, 복구 가능한 경우에는 계속 실행한다.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += OnThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Application.Run(new MainForm());
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        CrashLogger.Log(e.Exception, "UI thread");
        ShowError(e.Exception, recoverable: true);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        CrashLogger.Log(exception, "AppDomain");
        ShowError(exception, recoverable: false);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashLogger.Log(e.Exception, "Task");
        e.SetObserved();
    }

    private static void ShowError(Exception? exception, bool recoverable)
    {
        var detail = exception?.Message ?? "알 수 없는 오류";
        var message = recoverable
            ? "오류가 발생했지만 계속 실행할 수 있습니다.\n\n" + detail
            : "복구할 수 없는 오류가 발생해 프로그램을 종료합니다.\n\n" + detail;

        try
        {
            MessageBox.Show(message, "CS2Browser 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            // 메시지 표시 실패는 무시한다.
        }
    }
}
