using System;
using System.Runtime.InteropServices;

/// <summary>
/// EVENT_OBJECT_FOCUS を購読して「フォーカスが別の要素に移った」ことを記録する。
/// フォーカスウィンドウ(HWND)の比較では不十分である — 検証ログでは Edge の
/// アドレスバーもページ内の入力欄も focusCls=Chrome_WidgetWin_1 で同一だった。
/// アクセシビリティのフォーカスイベントは要素単位で発火するのでこれを使う。
/// </summary>
public class FocusHook : IDisposable
{
    private const uint EVENT_OBJECT_FOCUS = 0x8005;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    // デリゲートを GC させないためフィールドで保持する。
    // ローカル変数のまま渡すとコールバック時にプロセスが落ちる。
    private readonly WinEventProc _proc;
    private IntPtr _hook;
    private volatile bool _focusChanged;

    public FocusHook()
    {
        _proc = OnWinEvent;
        _hook = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, IntPtr.Zero,
            _proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    public bool IsInstalled
    {
        get { return _hook != IntPtr.Zero; }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        _focusChanged = true;
    }

    /// <summary>フォーカス移動が発生していれば true を返し、フラグをクリアする。</summary>
    public bool ConsumeFocusChanged()
    {
        if (!_focusChanged)
        {
            return false;
        }
        _focusChanged = false;
        return true;
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
