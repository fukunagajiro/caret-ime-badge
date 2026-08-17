using System;
using System.Runtime.InteropServices;

public static class NativeMethods
{
    public const uint WM_IME_CONTROL = 0x0283;
    public const int IMC_GETCONVERSIONMODE = 0x0001;
    public const int IMC_GETOPENSTATUS = 0x0005;
    public const uint OBJID_CARET = 0xFFFFFFF8;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("user32.dll")]
    public static extern bool GetGUIThreadInfo(int idThread, ref GUITHREADINFO lpgui);

    [DllImport("imm32.dll")]
    public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam,
        IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);

    [DllImport("oleacc.dll")]
    public static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint objectId,
        ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object ppvObject);

    /// <summary>
    /// フォアグラウンドウィンドウのスレッドでフォーカスを持つウィンドウを返す。
    /// 取得できない場合は引数をそのまま返す。
    /// </summary>
    public static IntPtr GetFocusWindow(IntPtr foreground)
    {
        if (foreground == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        int processId;
        int threadId = GetWindowThreadProcessId(foreground, out processId);
        GUITHREADINFO gti = new GUITHREADINFO();
        gti.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
        if (!GetGUIThreadInfo(threadId, ref gti))
        {
            return foreground;
        }
        return gti.hwndFocus != IntPtr.Zero ? gti.hwndFocus : foreground;
    }
}
