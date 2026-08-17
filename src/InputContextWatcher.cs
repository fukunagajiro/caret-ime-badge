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

public class InputContextWatcher : IDisposable
{
    private const int MaxImeFailures = 3;

    private readonly Timer _timer;
    private readonly BadgeStateMachine _machine;
    private readonly FocusHook _focusHook;
    private readonly Stopwatch _clock;
    private ImeMode _lastMode;
    private int _imeFailures;

    public event EventHandler<BadgeEventArgs> ShowRequested;
    public event EventHandler<BadgeEventArgs> MoveRequested;
    public event EventHandler FadeRequested;
    public event EventHandler HideNowRequested;

    public InputContextWatcher(Settings settings)
    {
        _machine = new BadgeStateMachine(settings.CaretMoveThresholdPx, settings.ShowDurationMs);
        _focusHook = new FocusHook();
        _clock = Stopwatch.StartNew();
        _lastMode = ImeMode.Unknown;
        _imeFailures = 0;
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

        Rectangle caret;
        if (!CaretLocator.TryGetCaret(focus, out caret))
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
