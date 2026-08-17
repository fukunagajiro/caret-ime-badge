using System;

public static class ImeReader
{
    private const uint TimeoutMs = 200;

    /// <summary>
    /// 対象ウィンドウの IME 状態をクロスプロセスで読む。
    /// ImmGetContext / ImmGetOpenStatus はプロセス内専用なので使えない。
    /// </summary>
    public static bool TryRead(IntPtr hwnd, out ImeMode mode)
    {
        mode = ImeMode.Unknown;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        IntPtr imeWnd = NativeMethods.ImmGetDefaultIMEWnd(hwnd);
        if (imeWnd == IntPtr.Zero)
        {
            return false;
        }
        IntPtr result;
        IntPtr ret = NativeMethods.SendMessageTimeout(imeWnd, NativeMethods.WM_IME_CONTROL,
            new IntPtr(NativeMethods.IMC_GETOPENSTATUS), IntPtr.Zero,
            NativeMethods.SMTO_ABORTIFHUNG, TimeoutMs, out result);
        if (ret == IntPtr.Zero)
        {
            return false;
        }
        int open = result.ToInt32();

        ret = NativeMethods.SendMessageTimeout(imeWnd, NativeMethods.WM_IME_CONTROL,
            new IntPtr(NativeMethods.IMC_GETCONVERSIONMODE), IntPtr.Zero,
            NativeMethods.SMTO_ABORTIFHUNG, TimeoutMs, out result);
        if (ret == IntPtr.Zero)
        {
            return false;
        }
        int conv = result.ToInt32();

        mode = ImeDecoder.Decode(open, conv);
        return true;
    }
}
