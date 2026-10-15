using System.Runtime.ExceptionServices;

namespace CS2Browser.Tests;

/// <summary>WinForms/Direct2D 처럼 STA 스레드가 필요한 코드를 실행하기 위한 도우미.</summary>
internal static class StaRunner
{
    public static void Run(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }
}
