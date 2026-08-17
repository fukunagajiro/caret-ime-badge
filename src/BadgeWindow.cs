using System;
using System.Drawing;
using System.Windows.Forms;

public class BadgeWindow : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    private readonly Settings _settings;
    private readonly Timer _fadeTimer;
    private readonly Font _font;
    private BadgeStyle _style;
    private int _fadeElapsedMs;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            cp.ExStyle |= WS_EX_TRANSPARENT;
            cp.ExStyle |= WS_EX_LAYERED;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    public BadgeWindow(Settings settings)
    {
        _settings = settings;
        // PerMonitorV2 宣言下では WinForms の自動スケールが物理ピクセル座標と二重になる
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = BadgeStyles.Background;
        _font = new Font("Yu Gothic UI", _settings.FontSize, FontStyle.Bold, GraphicsUnit.Point);
        Font = _font;
        Size = new Size(38, 24);
        Opacity = _settings.Opacity;
        DoubleBuffered = true;

        _fadeTimer = new Timer();
        _fadeTimer.Interval = 30;
        _fadeTimer.Tick += OnFadeTick;
    }

    public Size BadgeSize
    {
        get { return Size; }
    }

    /// <summary>
    /// 表示する。いつ消すかは BadgeStateMachine が決めるので、ここでは時間を数えない。
    /// </summary>
    public void ShowBadge(Point location, ImeMode mode)
    {
        _fadeTimer.Stop();
        _fadeElapsedMs = 0;
        _style = BadgeStyles.For(mode);
        Location = location;
        Opacity = _settings.Opacity;
        Invalidate();
        if (!Visible)
        {
            Show();
        }
    }

    public void MoveBadge(Point location)
    {
        if (Visible)
        {
            Location = location;
        }
    }

    /// <summary>フェードアウトを開始する。既にフェード中なら何もしない。</summary>
    public void FadeOut()
    {
        if (!Visible)
        {
            return;
        }
        if (_settings.FadeDurationMs <= 0)
        {
            HideNow();
            return;
        }
        if (_fadeTimer.Enabled)
        {
            return;
        }
        _fadeElapsedMs = 0;
        _fadeTimer.Start();
    }

    public void HideNow()
    {
        _fadeTimer.Stop();
        _fadeElapsedMs = 0;
        if (Visible)
        {
            Hide();
        }
    }

    private void OnFadeTick(object sender, EventArgs e)
    {
        _fadeElapsedMs += _fadeTimer.Interval;
        double ratio = 1.0 - ((double)_fadeElapsedMs / _settings.FadeDurationMs);
        if (ratio <= 0.0)
        {
            HideNow();
            return;
        }
        Opacity = _settings.Opacity * ratio;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // ShowBadge が一度も呼ばれていないうちに描画要求が来ることがある
        if (string.IsNullOrEmpty(_style.Glyph))
        {
            return;
        }
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        SolidBrush brush = new SolidBrush(_style.Fore);
        try
        {
            SizeF sz = e.Graphics.MeasureString(_style.Glyph, Font);
            float x = (Width - sz.Width) / 2f;
            float y = (Height - sz.Height) / 2f;
            e.Graphics.DrawString(_style.Glyph, Font, brush, x, y);
        }
        finally
        {
            brush.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_fadeTimer != null) { _fadeTimer.Dispose(); }
            if (_font != null) { _font.Dispose(); }
        }
        base.Dispose(disposing);
    }
}
