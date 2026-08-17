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

        // 一時的な目視確認用。アイコンを PNG に書き出して終了する。
        // 実装者はアイコンを見られないので、描いたものを人が判断できるようにする。
        if (args.Length > 1 && args[0] == "--dump-icon")
        {
            return DumpIcon(args[1]);
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

    /// <summary>
    /// トレイアイコンを PNG に書き出す（一時的な目視確認用）。
    /// 透明のままだと見づらいので、濃い背景・明るい背景に重ねた版も出す。
    /// Windows 11 のタスクバーはライト／ダークの両方があり、
    /// 片方でしか見えないアイコンは失敗なので、両方で確認できるようにする。
    /// </summary>
    private static int DumpIcon(string outDir)
    {
        try
        {
            Directory.CreateDirectory(outDir);
            using (System.Drawing.Icon icon = TrayIcon.Create())
            using (System.Drawing.Bitmap src = icon.ToBitmap())
            {
                Save(src, 32, outDir);
                Save(src, 16, outDir);
            }
            return 0;
        }
        catch (Exception)
        {
            return 1;
        }
    }

    private static void Save(System.Drawing.Bitmap src, int size, string outDir)
    {
        using (System.Drawing.Bitmap scaled = new System.Drawing.Bitmap(size, size,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        {
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(scaled))
            {
                // 単純な縮小だと実際の表示より汚く見え、判断を誤る。
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.Transparent);
                g.DrawImage(src, 0, 0, size, size);
            }
            string stem = Path.Combine(outDir, "icon-" + size);
            scaled.Save(stem + ".png", System.Drawing.Imaging.ImageFormat.Png);
            SaveOver(scaled, System.Drawing.Color.FromArgb(32, 32, 32), stem + "-dark.png");
            SaveOver(scaled, System.Drawing.Color.FromArgb(243, 243, 243), stem + "-light.png");
        }
    }

    private static void SaveOver(System.Drawing.Bitmap src, System.Drawing.Color back, string path)
    {
        using (System.Drawing.Bitmap composed = new System.Drawing.Bitmap(src.Width, src.Height))
        {
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(composed))
            {
                g.Clear(back);
                g.DrawImageUnscaled(src, 0, 0);
            }
            composed.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
