using System;
using System.Globalization;
using System.IO;

public class Settings
{
    public double Opacity = 0.88;
    public int ShowDurationMs = 800;
    public int FadeDurationMs = 200;
    public int CaretMoveThresholdPx = 2;
    public int PollIntervalMs = 120;
    public int OffsetX = 6;
    public int OffsetY = -4;
    public float FontSize = 10f;

    public static Settings Parse(string[] lines)
    {
        Settings s = new Settings();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[')
            {
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }
            string key = line.Substring(0, eq).Trim();
            string val = line.Substring(eq + 1).Trim();
            if (val.Length == 0)
            {
                continue;
            }
            Apply(s, key, val);
        }
        if (s.Opacity > 1.0) { s.Opacity = 1.0; }
        if (s.Opacity < 0.1) { s.Opacity = 0.1; }
        if (s.PollIntervalMs < 16) { s.PollIntervalMs = 16; }
        if (s.PollIntervalMs > 5000) { s.PollIntervalMs = 5000; }
        if (s.FontSize < 4f) { s.FontSize = 4f; }
        if (s.FontSize > 72f) { s.FontSize = 72f; }
        if (s.ShowDurationMs < 0) { s.ShowDurationMs = 0; }
        if (s.FadeDurationMs < 0) { s.FadeDurationMs = 0; }
        if (s.CaretMoveThresholdPx < 0) { s.CaretMoveThresholdPx = 0; }
        return s;
    }

    private static void Apply(Settings s, string key, string val)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        int iv;
        double dv;
        float fv;
        switch (key)
        {
            case "Opacity":
                if (double.TryParse(val, NumberStyles.Float, inv, out dv)) { s.Opacity = dv; }
                break;
            case "ShowDurationMs":
                if (int.TryParse(val, NumberStyles.Integer, inv, out iv)) { s.ShowDurationMs = iv; }
                break;
            case "FadeDurationMs":
                if (int.TryParse(val, NumberStyles.Integer, inv, out iv)) { s.FadeDurationMs = iv; }
                break;
            case "CaretMoveThresholdPx":
                if (int.TryParse(val, NumberStyles.Integer, inv, out iv)) { s.CaretMoveThresholdPx = iv; }
                break;
            case "PollIntervalMs":
                if (int.TryParse(val, NumberStyles.Integer, inv, out iv)) { s.PollIntervalMs = iv; }
                break;
            case "OffsetX":
                if (int.TryParse(val, NumberStyles.Integer, inv, out iv)) { s.OffsetX = iv; }
                break;
            case "OffsetY":
                if (int.TryParse(val, NumberStyles.Integer, inv, out iv)) { s.OffsetY = iv; }
                break;
            case "FontSize":
                if (float.TryParse(val, NumberStyles.Float, inv, out fv)) { s.FontSize = fv; }
                break;
        }
    }

    public static Settings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new Settings();
            }
            return Parse(File.ReadAllLines(path));
        }
        catch (Exception)
        {
            return new Settings();
        }
    }

    public static void WriteDefault(string path)
    {
        string[] lines = new string[] {
            "# caret-ime-badge 設定ファイル",
            "# 値を変更したらトレイメニューの「設定を再読み込み」を実行してください。",
            "",
            "# バッジの不透明度 (0.1 - 1.0)",
            "Opacity=0.88",
            "# バッジを表示し続ける時間 (ミリ秒)",
            "ShowDurationMs=800",
            "# フェードアウトにかける時間 (ミリ秒)",
            "FadeDurationMs=200",
            "# 入力開始とみなすキャレット移動量 (ピクセル)",
            "CaretMoveThresholdPx=2",
            "# ポーリング間隔 (ミリ秒)",
            "PollIntervalMs=120",
            "# キャレットからのバッジ位置オフセット",
            "OffsetX=6",
            "OffsetY=-4",
            "# バッジの文字サイズ",
            "FontSize=10"
        };
        File.WriteAllLines(path, lines);
    }
}
