using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
        {
            // winexe 版から対話的に実行された場合に備えて親コンソールへ接続する。
            // 開発ループでは build-test.cmd が作るコンソール版を使うこと。
            AttachConsole(-1);
            StreamWriter sw = new StreamWriter(Console.OpenStandardOutput());
            sw.AutoFlush = true;
            Console.SetOut(sw);
            return TestRunner.RunAll();
        }
        if (args.Length > 0 && args[0] == "--probe-ime")
        {
            AttachConsole(-1);
            StreamWriter pw = new StreamWriter(Console.OpenStandardOutput());
            pw.AutoFlush = true;
            Console.SetOut(pw);
            for (int i = 0; i < 100; i++)
            {
                IntPtr fg = NativeMethods.GetForegroundWindow();
                IntPtr focus = NativeMethods.GetFocusWindow(fg);
                ImeMode m;
                bool ok = ImeReader.TryRead(focus, out m);
                Console.WriteLine(i + " ok=" + ok + " mode=" + m);
                System.Threading.Thread.Sleep(300);
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "--probe-caret")
        {
            AttachConsole(-1);
            StreamWriter cw = new StreamWriter(Console.OpenStandardOutput());
            cw.AutoFlush = true;
            Console.SetOut(cw);
            for (int i = 0; i < 100; i++)
            {
                IntPtr fg = NativeMethods.GetForegroundWindow();
                IntPtr focus = NativeMethods.GetFocusWindow(fg);
                System.Drawing.Rectangle msaa;
                bool okMsaa = CaretLocator.TryMsaaCaret(focus, out msaa);
                System.Drawing.Rectangle uia;
                bool okUia = CaretLocator.TryUiaCaret(out uia);
                System.Drawing.Rectangle chosen;
                bool okAny = CaretLocator.TryGetCaret(focus, out chosen);
                string src = okMsaa ? "msaa" : (okAny ? "uia" : "none");
                Console.WriteLine(i
                    + " msaa=" + okMsaa + (okMsaa ? " " + msaa.ToString() : "")
                    + " | uia=" + okUia + (okUia ? " " + uia.ToString() + " plausible=" + CaretLocator.IsPlausibleCaret(uia) : "")
                    + " | chosen=" + okAny + (okAny ? " " + chosen.ToString() : "") + " src=" + src);
                System.Threading.Thread.Sleep(300);
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "--probe-focus")
        {
            AttachConsole(-1);
            StreamWriter fw = new StreamWriter(Console.OpenStandardOutput());
            fw.AutoFlush = true;
            Console.SetOut(fw);
            FocusHook hook = new FocusHook();
            Console.WriteLine("installed=" + hook.IsInstalled);
            for (int i = 0; i < 400; i++)
            {
                Application.DoEvents();
                if (hook.ConsumeFocusChanged())
                {
                    IntPtr fg = NativeMethods.GetForegroundWindow();
                    IntPtr focus = NativeMethods.GetFocusWindow(fg);
                    System.Drawing.Rectangle r;
                    bool ok = CaretLocator.TryGetCaret(focus, out r);
                    Console.WriteLine(i + " FOCUS-CHANGED fg=[" + DescribeWindow(fg)
                        + "] focus=[" + DescribeWindow(focus) + "] caret=" + ok + " rect=" + r);
                }
                System.Threading.Thread.Sleep(100);
            }
            hook.Dispose();
            return 0;
        }
        Console.WriteLine("GUI not implemented yet");
        return 0;
    }

    /// <summary>デバッグ用: ウィンドウのクラス名とプロセス名を "class=X proc=Y" の形で返す。</summary>
    private static string DescribeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return "class=(none) proc=(none)";
        }
        System.Text.StringBuilder buf = new System.Text.StringBuilder(256);
        NativeMethods.GetClassName(hwnd, buf, buf.Capacity);
        int pid;
        NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
        string proc;
        try
        {
            proc = System.Diagnostics.Process.GetProcessById(pid).ProcessName;
        }
        catch (Exception)
        {
            proc = "pid" + pid;
        }
        return "class=" + buf.ToString() + " proc=" + proc;
    }
}
