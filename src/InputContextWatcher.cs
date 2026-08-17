using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

public class BadgeEventArgs : EventArgs
{
    public Rectangle Caret;
    public ImeMode Mode;

    public BadgeEventArgs(Rectangle caret, ImeMode mode)
    {
        Caret = caret;
        Mode = mode;
    }
}

/// <summary>
/// --probe-trigger の一時計測用。1 ティックぶんの内部状態をそのまま外へ運ぶだけで、
/// 判定ロジックは一切含まない。計測後にこのクラスごと削除する想定。
/// </summary>
public class DiagnosticTickEventArgs : EventArgs
{
    public readonly IntPtr Foreground;
    public readonly IntPtr Focus;
    public readonly Sample Sample;
    public readonly string CaretSource;
    public readonly BadgeAction Action;
    public readonly bool IsShown;

    public DiagnosticTickEventArgs(IntPtr foreground, IntPtr focus, Sample sample,
        string caretSource, BadgeAction action, bool isShown)
    {
        Foreground = foreground;
        Focus = focus;
        Sample = sample;
        CaretSource = caretSource;
        Action = action;
        IsShown = isShown;
    }
}

public class InputContextWatcher : IDisposable
{
    private const int MaxImeFailures = 3;

    private readonly Timer _timer;
    private readonly BadgeStateMachine _machine;
    private readonly FocusHook _focusHook;
    private readonly Stopwatch _clock;
    private ImeMode _lastMode;
    private int _imeFailures;

    // --probe-trigger 診断用。直近の Read() が観測した値を保持するだけで、
    // 判定には一切使わない(バッジ表示ロジックからは読まれない)。
    private IntPtr _lastForeground;
    private IntPtr _lastFocus;
    private string _lastCaretSource;

    public event EventHandler<BadgeEventArgs> ShowRequested;
    public event EventHandler<BadgeEventArgs> MoveRequested;
    public event EventHandler FadeRequested;
    public event EventHandler HideNowRequested;

    /// <summary>--probe-trigger の一時計測用。毎ティック、判定結果とともに発火する。</summary>
    public event EventHandler<DiagnosticTickEventArgs> DiagnosticTick;

    public InputContextWatcher(Settings settings)
    {
        _machine = new BadgeStateMachine(settings.CaretMoveThresholdPx, settings.ShowDurationMs, settings.MovementGraceMs);
        _focusHook = new FocusHook();
        _clock = Stopwatch.StartNew();
        _lastMode = ImeMode.Unknown;
        _imeFailures = 0;
        _lastForeground = IntPtr.Zero;
        _lastFocus = IntPtr.Zero;
        _lastCaretSource = "none";
        _timer = new Timer();
        _timer.Interval = settings.PollIntervalMs;
        _timer.Tick += OnTick;
    }

    /// <summary>
    /// フォーカス移動検知(EVENT_OBJECT_FOCUS フック)が有効かどうか。
    /// false の場合、キャレット移動を伴わないフォーカス変更ではバッジが再表示されない。
    /// </summary>
    public bool HookInstalled
    {
        get { return _focusHook.IsInstalled; }
    }

    public void Start()
    {
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        Raise(HideNowRequested);
    }

    private void OnTick(object sender, EventArgs e)
    {
        Sample s = Read();
        BadgeAction action = _machine.Next(s, _clock.ElapsedMilliseconds);
        if (DiagnosticTick != null)
        {
            DiagnosticTick(this, new DiagnosticTickEventArgs(
                _lastForeground, _lastFocus, s, _lastCaretSource, action, _machine.IsShown));
        }
        switch (action)
        {
            case BadgeAction.Show:
                if (ShowRequested != null)
                {
                    ShowRequested(this, new BadgeEventArgs(s.Caret, s.Mode));
                }
                break;
            case BadgeAction.Move:
                if (MoveRequested != null)
                {
                    MoveRequested(this, new BadgeEventArgs(s.Caret, s.Mode));
                }
                break;
            case BadgeAction.Fade:
                Raise(FadeRequested);
                break;
            case BadgeAction.HideNow:
                Raise(HideNowRequested);
                break;
        }
    }

    private void Raise(EventHandler h)
    {
        if (h != null)
        {
            h(this, EventArgs.Empty);
        }
    }

    private Sample Read()
    {
        Sample s = new Sample();
        s.HasCaret = false;
        s.Mode = _lastMode;
        // フラグはキャレットの有無によらず毎ティック消費する。
        // 溜めたままにすると、無関係なフォーカス移動が後で誤って表示を起こす。
        s.FocusChanged = _focusHook.ConsumeFocusChanged();

        IntPtr fg = NativeMethods.GetForegroundWindow();
        IntPtr focus = NativeMethods.GetFocusWindow(fg);
        _lastForeground = fg;
        _lastFocus = focus;

        Rectangle caret;
        bool caretIsSynthesized;
        string caretSource;
        bool caretOk = CaretLocator.TryGetCaret(focus, out caret, out caretIsSynthesized, out caretSource);
        _lastCaretSource = caretSource;
        if (!caretOk)
        {
            return s;
        }

        ImeMode mode;
        if (ImeReader.TryRead(focus, out mode))
        {
            _imeFailures = 0;
            _lastMode = mode;
        }
        else
        {
            // 読めなかった場合は直前の値を保持し、3 回続いたら隠す
            _imeFailures++;
            if (_imeFailures >= MaxImeFailures)
            {
                return s;
            }
        }

        s.HasCaret = true;
        s.Caret = caret;
        s.Mode = _lastMode;
        s.CaretIsSynthesized = caretIsSynthesized;
        return s;
    }

    public void Dispose()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Dispose();
        }
        if (_focusHook != null)
        {
            _focusHook.Dispose();
        }
    }
}
