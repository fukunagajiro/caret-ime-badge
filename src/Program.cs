using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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

        bool createdNew;
        Mutex mutex = new Mutex(true, "Local\\caret-ime-badge", out createdNew);
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
}
