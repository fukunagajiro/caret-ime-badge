public static class SettingsTests
{
    public static void Run()
    {
        // 空入力なら全て既定値
        Settings d = Settings.Parse(new string[0]);
        TestRunner.AssertEqual(0.88, d.Opacity, "settings/default-opacity");
        TestRunner.AssertEqual(800, d.ShowDurationMs, "settings/default-show");
        TestRunner.AssertEqual(200, d.FadeDurationMs, "settings/default-fade");
        TestRunner.AssertEqual(2, d.CaretMoveThresholdPx, "settings/default-threshold");
        TestRunner.AssertEqual(500, d.MovementGraceMs, "settings/default-grace");
        TestRunner.AssertEqual(120, d.PollIntervalMs, "settings/default-poll");
        TestRunner.AssertEqual(6, d.OffsetX, "settings/default-offsetx");
        TestRunner.AssertEqual(-4, d.OffsetY, "settings/default-offsety");

        // 値の上書き
        Settings s = Settings.Parse(new string[] {
            "# コメント行",
            "; これもコメント",
            "",
            "Opacity=0.5",
            "ShowDurationMs = 1500",
            "OffsetY=-10",
            "UnknownKey=whatever"
        });
        TestRunner.AssertEqual(0.5, s.Opacity, "settings/parsed-opacity");
        TestRunner.AssertEqual(1500, s.ShowDurationMs, "settings/parsed-show-with-spaces");
        TestRunner.AssertEqual(-10, s.OffsetY, "settings/parsed-negative");
        TestRunner.AssertEqual(200, s.FadeDurationMs, "settings/unspecified-stays-default");

        // 不正な値は既定値のまま
        Settings bad = Settings.Parse(new string[] { "ShowDurationMs=abc", "Opacity=" });
        TestRunner.AssertEqual(800, bad.ShowDurationMs, "settings/invalid-int-ignored");
        TestRunner.AssertEqual(0.88, bad.Opacity, "settings/empty-value-ignored");

        // Opacity は 0.1〜1.0 にクランプする
        TestRunner.AssertEqual(1.0, Settings.Parse(new string[] { "Opacity=5" }).Opacity, "settings/clamp-high");
        TestRunner.AssertEqual(0.1, Settings.Parse(new string[] { "Opacity=0" }).Opacity, "settings/clamp-low");

        // 範囲外の値はクランプする。ini は利用者が編集する前提で自動生成されるため、
        // 設定ミスがそのまま起動時例外になってはならない。
        TestRunner.AssertEqual(16, Settings.Parse(new string[] { "PollIntervalMs=0" }).PollIntervalMs, "settings/clamp-poll-low");
        TestRunner.AssertEqual(5000, Settings.Parse(new string[] { "PollIntervalMs=999999" }).PollIntervalMs, "settings/clamp-poll-high");
        TestRunner.AssertEqual(4f, Settings.Parse(new string[] { "FontSize=0" }).FontSize, "settings/clamp-font-low");
        TestRunner.AssertEqual(72f, Settings.Parse(new string[] { "FontSize=500" }).FontSize, "settings/clamp-font-high");
        TestRunner.AssertEqual(0, Settings.Parse(new string[] { "ShowDurationMs=-1" }).ShowDurationMs, "settings/clamp-show-negative");
        TestRunner.AssertEqual(0, Settings.Parse(new string[] { "CaretMoveThresholdPx=-5" }).CaretMoveThresholdPx, "settings/clamp-threshold-negative");
        TestRunner.AssertEqual(0, Settings.Parse(new string[] { "MovementGraceMs=-1" }).MovementGraceMs, "settings/clamp-grace-negative");
    }
}
