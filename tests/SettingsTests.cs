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
    }
}
