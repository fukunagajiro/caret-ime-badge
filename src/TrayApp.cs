using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class TrayApp : IDisposable
{
    private readonly string _settingsPath;
    private NotifyIcon _icon;
    private ToolStripMenuItem _pauseItem;
    private Settings _settings;
    private BadgeWindow _badge;
    private InputContextWatcher _watcher;
    private bool _paused;

    public TrayApp(string settingsPath)
    {
        _settingsPath = settingsPath;
        _paused = false;
    }

    public void Run()
    {
        if (!File.Exists(_settingsPath))
        {
            try { Settings.WriteDefault(_settingsPath); }
            catch (Exception) { }
        }
        _settings = Settings.Load(_settingsPath);

        _icon = new NotifyIcon();
        _icon.Icon = SystemIcons.Application;
        _icon.Text = "cursor-ime-mode";
        _icon.Visible = true;

        ContextMenuStrip menu = new ContextMenuStrip();
        _pauseItem = new ToolStripMenuItem("一時停止");
        _pauseItem.Click += OnTogglePause;
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem openItem = new ToolStripMenuItem("設定ファイルを開く");
        openItem.Click += OnOpenSettings;
        menu.Items.Add(openItem);

        ToolStripMenuItem reloadItem = new ToolStripMenuItem("設定を再読み込み");
        reloadItem.Click += OnReloadSettings;
        menu.Items.Add(reloadItem);

        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += OnExit;
        menu.Items.Add(exitItem);

        _icon.ContextMenuStrip = menu;

        BuildPipeline();
        Application.Run();
    }

    private void BuildPipeline()
    {
        _badge = new BadgeWindow(_settings);
        _watcher = new InputContextWatcher(_settings);
        _watcher.ShowRequested += OnShow;
        _watcher.MoveRequested += OnMove;
        _watcher.FadeRequested += OnFade;
        _watcher.HideNowRequested += OnHideNow;
        if (!_paused)
        {
            _watcher.Start();
        }
    }

    private void TearDownPipeline()
    {
        if (_watcher != null)
        {
            _watcher.Stop();
            _watcher.Dispose();
            _watcher = null;
        }
        if (_badge != null)
        {
            _badge.Dispose();
            _badge = null;
        }
    }

    private Point PlaceFor(Rectangle caret)
    {
        Screen screen = Screen.FromPoint(new Point(caret.X, caret.Y));
        return BadgePlacer.Place(caret, _badge.BadgeSize, screen.WorkingArea,
            _settings.OffsetX, _settings.OffsetY);
    }

    private void OnShow(object sender, BadgeEventArgs e)
    {
        _badge.ShowBadge(PlaceFor(e.Caret), e.Mode);
    }

    private void OnMove(object sender, BadgeEventArgs e)
    {
        _badge.MoveBadge(PlaceFor(e.Caret));
    }

    private void OnFade(object sender, EventArgs e)
    {
        _badge.FadeOut();
    }

    private void OnHideNow(object sender, EventArgs e)
    {
        _badge.HideNow();
    }

    private void OnTogglePause(object sender, EventArgs e)
    {
        _paused = !_paused;
        _pauseItem.Text = _paused ? "再開" : "一時停止";
        if (_paused)
        {
            _watcher.Stop();
        }
        else
        {
            _watcher.Start();
        }
    }

    private void OnOpenSettings(object sender, EventArgs e)
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                Settings.WriteDefault(_settingsPath);
            }
            System.Diagnostics.Process.Start("notepad.exe", _settingsPath);
        }
        catch (Exception) { }
    }

    private void OnReloadSettings(object sender, EventArgs e)
    {
        _settings = Settings.Load(_settingsPath);
        TearDownPipeline();
        BuildPipeline();
    }

    private void OnExit(object sender, EventArgs e)
    {
        _icon.Visible = false;
        Application.Exit();
    }

    public void Dispose()
    {
        TearDownPipeline();
        if (_icon != null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
