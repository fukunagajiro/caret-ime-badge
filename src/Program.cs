using System;
using System.IO;
using System.Runtime.InteropServices;

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
                System.Drawing.Rectangle r;
                bool ok = CaretLocator.TryGetCaret(focus, out r);
                Console.WriteLine(i + " caret=" + ok + " rect=" + r);
                System.Threading.Thread.Sleep(300);
            }
            return 0;
        }
        Console.WriteLine("GUI not implemented yet");
        return 0;
    }
}
