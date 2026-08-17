using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
        {
            // winexe 版から対話的に実行された場合に備えて親コンソールへ接続する。
            // 開発ループでは build-test.cmd が作るコンソール版 selftest.exe を使うこと。
            AttachConsole(-1);
            StreamWriter sw = new StreamWriter(Console.OpenStandardOutput());
            sw.AutoFlush = true;
            Console.SetOut(sw);
            return TestRunner.RunAll();
        }

        if (args.Length > 0 && args[0] == "--probe-trigger")
        {
            // 一時的な診断モード。計測後に削除する。詳細は RunProbeTrigger を参照。
            return RunProbeTrigger();
        }

        bool createdNew;
        Mutex mutex = new Mutex(true, "Local\\cursor-ime-mode", out createdNew);
        if (!createdNew)
        {
            return 0;
        }

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string settingsPath = Path.Combine(exeDir, "settings.ini");

            TrayApp app = new TrayApp(settingsPath);
            try
            {
                app.Run();
            }
            finally
            {
                app.Dispose();
            }
            return 0;
        }
        finally
        {
            mutex.ReleaseMutex();
            mutex.Close();
        }
    }

    // ==================== --probe-trigger (一時的な診断モード) ====================
    //
    // 目的: 「新しいタブでバッジが出ない」報告の原因を、シップ済みのロジックをそのまま
    // 動かして観測する。CaretLocator / ImeReader / FocusHook / BadgeStateMachine の
    // ロジックはここでは一切書き直さない — InputContextWatcher が毎ティック実際に計算した
    // 値を DiagnosticTick 経由でそのまま受け取り、ファイルへ書き出すだけ。
    //
    // 計測後にこのモードごと削除する想定 (RunProbeTrigger, ProbeLogger, および
    // Program.cs 冒頭の --probe-trigger 分岐)。

    private const int ProbeDurationMs = 90000;

    private static int RunProbeTrigger()
    {
        string logPath = Path.Combine(Path.GetTempPath(), "cursor-ime-mode-trigger.log");
        Settings settings = new Settings();
        InputContextWatcher watcher = new InputContextWatcher(settings);
        ProbeLogger logger = new ProbeLogger(logPath);
        watcher.DiagnosticTick += logger.OnDiagnosticTick;

        System.Windows.Forms.Timer stopTimer = new System.Windows.Forms.Timer();
        stopTimer.Interval = ProbeDurationMs;
        stopTimer.Tick += OnProbeTimeout;

        try
        {
            watcher.Start();
            stopTimer.Start();
            Application.Run();
        }
        finally
        {
            stopTimer.Stop();
            stopTimer.Dispose();
            watcher.Stop();
            watcher.Dispose();
            logger.Dispose();
        }
        return 0;
    }

    private static void OnProbeTimeout(object sender, EventArgs e)
    {
        System.Windows.Forms.Timer t = sender as System.Windows.Forms.Timer;
        if (t != null)
        {
            t.Stop();
        }
        Application.Exit();
    }
}

/// <summary>
/// --probe-trigger の一時計測用ロガー。InputContextWatcher.DiagnosticTick が運んできた値を
/// 整形して書き出すだけで、判定は一切行わない。
///
/// 「変化があったティックのみ記録する」の判定は、直前に書き出した内容(タイムスタンプを
/// 除いた本文)と単純比較する。書き出していないティックは直前の書き出し内容と同じである
/// ことが不変条件として保たれるため、これは「直前のティックと比較する」ことと等価になる。
/// </summary>
internal sealed class ProbeLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private string _lastContent;

    public ProbeLogger(string path)
    {
        _writer = new StreamWriter(path, true, new UTF8Encoding(false));
        _writer.AutoFlush = true;
        _lastContent = null;
    }

    public void OnDiagnosticTick(object sender, DiagnosticTickEventArgs e)
    {
        string content = BuildContent(e);
        bool changed = _lastContent == null || content != _lastContent;
        bool actionable = e.Action != BadgeAction.None;
        if (!changed && !actionable)
        {
            return;
        }
        _lastContent = content;
        string ts = DateTime.Now.ToString("HH:mm:ss.fff");
        _writer.WriteLine(ts + " " + content);
    }

    private static string BuildContent(DiagnosticTickEventArgs e)
    {
        Sample s = e.Sample;
        string rect = s.HasCaret
            ? (s.Caret.X + "," + s.Caret.Y + "," + s.Caret.Width + "," + s.Caret.Height)
            : "-";
        return "fg=" + ProcessNameOf(e.Foreground) +
            " cls=" + ClassNameOf(e.Focus) +
            " caret=" + (s.HasCaret ? "yes" : "no") +
            " rect=" + rect +
            " src=" + e.CaretSource +
            " mode=" + s.Mode +
            " focusEvent=" + (s.FocusChanged ? "1" : "0") +
            " action=" + e.Action +
            " shown=" + (e.IsShown ? "1" : "0");
    }

    private static string ProcessNameOf(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return "?";
        }
        try
        {
            int pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0)
            {
                return "?";
            }
            using (Process p = Process.GetProcessById(pid))
            {
                return p.ProcessName;
            }
        }
        catch (Exception)
        {
            return "?";
        }
    }

    private static string ClassNameOf(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return "?";
        }
        try
        {
            StringBuilder sb = new StringBuilder(256);
            int len = NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
            if (len <= 0)
            {
                return "?";
            }
            return sb.ToString();
        }
        catch (Exception)
        {
            return "?";
        }
    }

    public void Dispose()
    {
        if (_writer != null)
        {
            _writer.Dispose();
        }
    }
}
