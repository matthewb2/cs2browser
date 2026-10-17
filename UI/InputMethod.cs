using System.Runtime.InteropServices;
using CS2Browser.Layout;

namespace CS2Browser.UI;

/// <summary>
/// IME(한글 등) 조합을 처리하기 위한 Win32 P/Invoke 헬퍼.
/// 조합 중 문자열/확정 문자열을 읽어 텍스트 입력 상자에 반영하고,
/// 조합 윈도우(후보 창)를 캐럿 위치로 옮긴다.
/// </summary>
internal static class InputMethod
{
    public const int WmImeStartComposition = 0x010D;
    public const int WmImeComposition = 0x010F;

    public const int GcsCompStr = 0x0008;
    public const int GcsResultStr = 0x0800;

    private const int CfsPoint = 0x0002;

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hImc);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    private static extern int ImmGetCompositionString(IntPtr hImc, int dwIndex, IntPtr lpBuf, int dwBufLen);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    private static extern int ImmGetCompositionString(IntPtr hImc, int dwIndex, [Out] char[] lpBuf, int dwBufLen);

    [DllImport("imm32.dll")]
    private static extern bool ImmSetCompositionWindow(IntPtr hImc, ref CompositionForm lpCompForm);

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionForm
    {
        public int Style;
        public NativePoint CurrentPos;
        public NativeRect Area;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>확정된 조합 결과 문자열을 상자 커서 위치에 삽입한다.</summary>
    public static void CommitResult(IntPtr hwnd, LayoutBox box)
    {
        var hImc = ImmGetContext(hwnd);
        if (hImc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var result = ReadString(hImc, GcsResultStr);
            if (!string.IsNullOrEmpty(result))
            {
                Insert(box, result);
            }

            box.CompositionText = string.Empty;
        }
        finally
        {
            ImmReleaseContext(hwnd, hImc);
        }
    }

    /// <summary>조합 중인 문자열을 갱신한다(빈 문자열이면 조합 종료).</summary>
    public static void UpdateComposition(IntPtr hwnd, LayoutBox box)
    {
        var hImc = ImmGetContext(hwnd);
        if (hImc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            box.CompositionText = ReadString(hImc, GcsCompStr) ?? string.Empty;
        }
        finally
        {
            ImmReleaseContext(hwnd, hImc);
        }
    }

    /// <summary>IME 조합 윈도우를 지정한 클라이언트 좌표로 옮긴다.</summary>
    public static void SetCompositionWindow(IntPtr hwnd, int x, int y)
    {
        var hImc = ImmGetContext(hwnd);
        if (hImc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var form = new CompositionForm
            {
                Style = CfsPoint,
                CurrentPos = new NativePoint { X = x, Y = y }
            };
            ImmSetCompositionWindow(hImc, ref form);
        }
        finally
        {
            ImmReleaseContext(hwnd, hImc);
        }
    }

    private static string? ReadString(IntPtr hImc, int index)
    {
        var size = ImmGetCompositionString(hImc, index, IntPtr.Zero, 0);
        if (size <= 0)
        {
            return null;
        }

        var buffer = new char[size / 2];
        ImmGetCompositionString(hImc, index, buffer, size);
        return new string(buffer);
    }

    private static void Insert(LayoutBox box, string value)
    {
        var text = box.InputText ?? string.Empty;
        var index = Math.Clamp(box.CaretIndex, 0, text.Length);
        box.InputText = text.Insert(index, value);
        box.CaretIndex = index + value.Length;
    }
}
