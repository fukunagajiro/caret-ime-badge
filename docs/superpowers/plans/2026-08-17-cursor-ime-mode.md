# cursor-ime-mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** テキストキャレットの横に IME の状態バッジを一瞬だけ表示する、Windows 常駐トレイアプリを作る。

**Architecture:** Win32 依存部と純粋ロジックを分離する。純粋ロジック（モード判定、バッジ配置、表示状態機械、設定パース）は `--self-test` で自動テストし、Win32 依存部（IME 読み取り、キャレット取得、オーバーレイ描画）は手動回帰手順で確認する。120ms のポーリングで「フォーカスウィンドウ・キャレット矩形・IME モード」を 1 サンプルにまとめ、状態機械が表示/非表示/移動の指示を出す。

**Tech Stack:** C# 5（Windows 標準搭載の `csc.exe`、.NET SDK 不要）、WinForms、MSAA (`oleacc.dll`)、UI Automation、IMM32

**Spec:** `docs/superpowers/specs/2026-08-17-cursor-ime-mode-design.md`

## Global Constraints

- **コンパイラ**: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（C# 5 相当）。**文字列補間 `$""`、式形式メンバー `=>`、`nameof`、`out var`、auto-property 初期化子は使用不可。** プロパティは明示的な getter/setter かフィールドで書く。
- **外部依存ゼロ**: NuGet、.NET SDK、テストフレームワークを一切使わない。参照するのは Windows に同梱されているアセンブリのみ。
- **成果物**: `build\cursor-ime-mode.exe`（winexe）1 つ + `settings.ini`
- **参照アセンブリの絶対パス**（実機で確認済み）:
  - `C:\Windows\Microsoft.NET\assembly\GAC_MSIL\UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll`
  - `C:\Windows\Microsoft.NET\assembly\GAC_MSIL\UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll`
  - `C:\Windows\Microsoft.NET\assembly\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll`
  - `System.dll` / `System.Core.dll` / `System.Drawing.dll` / `System.Windows.Forms.dll` / `Accessibility.dll` は単純名で解決できる
- **テストの実行方法**: `build\selftest.exe --self-test` を使う（`/target:exe` のコンソール版）。終了コードが失敗件数。**winexe 版の `--self-test` は使わない** — 実機検証の結果、`AttachConsole` した出力は親コンソールに書かれるためパイプ経由では消える。winexe 側の `AttachConsole` は対話的に使う人向けに残すが、開発ループでは信頼しない。
- **バッチファイルは必ず先頭で `cd /d "%~dp0"` する** — Git Bash から `cmd.exe //c "<絶対パス>\build.cmd"` で呼ぶため、カレントディレクトリが一致しない。
- **IME ビット定義**: `NATIVE=0x1`, `KATAKANA=0x2`, `FULLSHAPE=0x8`, `ROMAN=0x10`。`ROMAN` はモード判定に無関係なので無視する。`open=0` のとき `conv` の値は無意味なので、**必ず `open` を先に見る。**

---

## File Structure

| ファイル | 責務 |
|---|---|
| `build.cmd` | 製品版 winexe をビルド |
| `build-test.cmd` | 自己テスト用のコンソール版 exe をビルド |
| `app.manifest` | PerMonitorV2 DPI 宣言 + supportedOS |
| `.gitignore` | `build/` を除外 |
| `src/Program.cs` | エントリポイント。`--self-test` 分岐、多重起動防止 |
| `src/ImeMode.cs` | `ImeMode` enum |
| `src/ImeDecoder.cs` | `open`/`conv` → `ImeMode`（純粋） |
| `src/BadgeStyle.cs` | `ImeMode` → 表示文字と色（純粋） |
| `src/BadgePlacer.cs` | キャレット矩形 → バッジ座標（純粋） |
| `src/Sample.cs` | 1 回のポーリング結果を表す構造体 |
| `src/BadgeStateMachine.cs` | 表示/非表示/移動を決める状態機械（純粋） |
| `src/Settings.cs` | ini のパースと読み込み（パースは純粋） |
| `src/NativeMethods.cs` | P/Invoke 宣言の集約 |
| `src/ImeReader.cs` | `WM_IME_CONTROL` による IME 状態取得 |
| `src/CaretLocator.cs` | MSAA 主・UIA 従のキャレット矩形取得 |
| `src/InputContextWatcher.cs` | ポーリングループ。状態機械を駆動しイベントを発火 |
| `src/BadgeWindow.cs` | `NOACTIVATE` レイヤード窓の表示とフェード |
| `src/TrayApp.cs` | NotifyIcon、メニュー、全体の配線 |
| `tests/TestRunner.cs` | アサーションと結果集計 |
| `tests/ImeDecoderTests.cs` | `ImeDecoder` のテスト |
| `tests/BadgePlacerTests.cs` | `BadgePlacer` のテスト |
| `tests/BadgeStateMachineTests.cs` | `BadgeStateMachine` のテスト |
| `tests/SettingsTests.cs` | ini パースのテスト |
| `README.md` | 使い方と既知の制約 |

---

### Task 1: ビルド基盤・自己テストランナー・ImeDecoder

最初のタスクは足場とテストサイクルを同時に立ち上げる。以降のタスクはこのサイクルに乗るだけになる。

**Files:**
- Create: `build.cmd`, `build-test.cmd`, `app.manifest`, `.gitignore`
- Create: `src/Program.cs`, `src/ImeMode.cs`, `src/ImeDecoder.cs`
- Test: `tests/TestRunner.cs`, `tests/ImeDecoderTests.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `public enum ImeMode { Off, Hiragana, FullKatakana, HalfKatakana, FullAlnum, HalfAlnum, Unknown }`
  - `public static ImeMode ImeDecoder.Decode(int open, int conv)`
  - `public static void TestRunner.AssertEqual(object expected, object actual, string label)`
  - `public static void TestRunner.AssertTrue(bool cond, string label)`
  - `public static int TestRunner.Failures { get; }`
  - `public static int TestRunner.RunAll()` — 全テストクラスを呼び失敗件数を返す

- [ ] **Step 1: `.gitignore` と `app.manifest` を作る**

`.gitignore`:

```
build/
settings.ini
*.user
```

`app.manifest`（`supportedOS` の GUID がないと PerMonitorV2 が効かない）:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="cursor-ime-mode" type="win32" />
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 2: ビルドスクリプトを 2 つ作る**

`build-test.cmd`:

```bat
@echo off
setlocal
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set GAC=C:\Windows\Microsoft.NET\assembly\GAC_MSIL
if not exist build mkdir build
"%CSC%" /nologo /target:exe /platform:x64 /out:build\selftest.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:Accessibility.dll ^
  /r:"%GAC%\UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll" ^
  /r:"%GAC%\UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll" ^
  /r:"%GAC%\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll" ^
  src\*.cs tests\*.cs
```

`build.cmd`（`/target:winexe` と `/win32manifest` が違うだけ）:

```bat
@echo off
setlocal
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set GAC=C:\Windows\Microsoft.NET\assembly\GAC_MSIL
if not exist build mkdir build
"%CSC%" /nologo /target:winexe /platform:x64 /out:build\cursor-ime-mode.exe ^
  /win32manifest:app.manifest ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:Accessibility.dll ^
  /r:"%GAC%\UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll" ^
  /r:"%GAC%\UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll" ^
  /r:"%GAC%\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll" ^
  src\*.cs tests\*.cs
```

- [ ] **Step 3: 失敗するテストを書く**

`tests/TestRunner.cs`:

```csharp
using System;

public static class TestRunner
{
    private static int _failures;
    private static int _total;

    public static int Failures { get { return _failures; } }

    public static void Reset()
    {
        _failures = 0;
        _total = 0;
    }

    public static void AssertEqual(object expected, object actual, string label)
    {
        _total++;
        bool ok = (expected == null) ? (actual == null) : expected.Equals(actual);
        if (!ok)
        {
            _failures++;
            Console.WriteLine("FAIL " + label + ": expected=" + expected + " actual=" + actual);
        }
    }

    public static void AssertTrue(bool cond, string label)
    {
        _total++;
        if (!cond)
        {
            _failures++;
            Console.WriteLine("FAIL " + label);
        }
    }

    public static int RunAll()
    {
        Reset();
        ImeDecoderTests.Run();
        Console.WriteLine("ran=" + _total + " failures=" + _failures);
        return _failures;
    }
}
```

`tests/ImeDecoderTests.cs`（実測値と、ビット定義から導いた推論値の両方を固定する）:

```csharp
public static class ImeDecoderTests
{
    public static void Run()
    {
        // open=0 のときは conv の値によらず Off（実測: Edge 0x9/0x0、explorer 0x19）
        TestRunner.AssertEqual(ImeMode.Off, ImeDecoder.Decode(0, 0x0), "off/0x0");
        TestRunner.AssertEqual(ImeMode.Off, ImeDecoder.Decode(0, 0x9), "off/0x9");
        TestRunner.AssertEqual(ImeMode.Off, ImeDecoder.Decode(0, 0x19), "off/0x19");

        // 実測値
        TestRunner.AssertEqual(ImeMode.Hiragana, ImeDecoder.Decode(1, 0x9), "hiragana/0x9");
        TestRunner.AssertEqual(ImeMode.HalfKatakana, ImeDecoder.Decode(1, 0x3), "halfkana/0x3");
        TestRunner.AssertEqual(ImeMode.FullAlnum, ImeDecoder.Decode(1, 0x8), "fullalnum/0x8");

        // ビット定義からの推論値
        TestRunner.AssertEqual(ImeMode.FullKatakana, ImeDecoder.Decode(1, 0xB), "fullkana/0xB");
        TestRunner.AssertEqual(ImeMode.HalfAlnum, ImeDecoder.Decode(1, 0x0), "halfalnum/0x0");

        // ROMAN ビット(0x10)はモード判定に影響しない
        TestRunner.AssertEqual(ImeMode.Hiragana, ImeDecoder.Decode(1, 0x19), "roman-ignored/0x19");
        TestRunner.AssertEqual(ImeMode.FullKatakana, ImeDecoder.Decode(1, 0x1B), "roman-ignored/0x1B");

        // 読み取り失敗は Unknown（例外を投げない）
        TestRunner.AssertEqual(ImeMode.Unknown, ImeDecoder.Decode(-1, -1), "unknown/read-failure");
    }
}
```

`src/Program.cs`（まずコンパイルを通すため最小限。GUI は Task 8 で入れる）:

```csharp
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
        Console.WriteLine("GUI not implemented yet");
        return 0;
    }
}
```

- [ ] **Step 4: テストが失敗することを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd"
```

Expected: `ImeMode` と `ImeDecoder` が存在しないためコンパイルエラー（`error CS0246`）。これがこの段階の「失敗」である。

- [ ] **Step 5: 最小限の実装を書く**

`src/ImeMode.cs`:

```csharp
public enum ImeMode
{
    Off,
    Hiragana,
    FullKatakana,
    HalfKatakana,
    FullAlnum,
    HalfAlnum,
    Unknown
}
```

`src/ImeDecoder.cs`:

```csharp
public static class ImeDecoder
{
    public const int NATIVE = 0x1;
    public const int KATAKANA = 0x2;
    public const int FULLSHAPE = 0x8;
    public const int ROMAN = 0x10;

    /// <summary>
    /// IMC_GETOPENSTATUS / IMC_GETCONVERSIONMODE の生値を ImeMode に変換する。
    /// open が 0/1 以外のときは読み取り失敗とみなし Unknown を返す（例外は投げない）。
    /// </summary>
    public static ImeMode Decode(int open, int conv)
    {
        if (open != 0 && open != 1)
        {
            return ImeMode.Unknown;
        }
        if (open == 0)
        {
            return ImeMode.Off;
        }
        bool native = (conv & NATIVE) != 0;
        bool katakana = (conv & KATAKANA) != 0;
        bool fullShape = (conv & FULLSHAPE) != 0;

        if (!native)
        {
            return fullShape ? ImeMode.FullAlnum : ImeMode.HalfAlnum;
        }
        if (katakana)
        {
            return fullShape ? ImeMode.FullKatakana : ImeMode.HalfKatakana;
        }
        return ImeMode.Hiragana;
    }
}
```

- [ ] **Step 6: テストが通ることを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: `ran=11 failures=0` と `EXIT=0`

- [ ] **Step 7: winexe もビルドできることを確認する**

```bash
cmd.exe //c "$(pwd -W)\build.cmd" && ls -l build/cursor-ime-mode.exe
```

Expected: エラーなくビルドされ、exe が存在する

- [ ] **Step 8: コミット**

```bash
git add .gitignore app.manifest build.cmd build-test.cmd src tests
git commit -m "feat: ビルド基盤と ImeDecoder を追加"
```

---

### Task 2: BadgeStyle — モードごとの表示文字と色

**Files:**
- Create: `src/BadgeStyle.cs`
- Modify: `tests/TestRunner.cs`（`RunAll` に 1 行追加）
- Test: `tests/BadgeStyleTests.cs`

**Interfaces:**
- Consumes: `ImeMode`（Task 1）
- Produces:
  - `public struct BadgeStyle { public string Glyph; public Color Fore; }`
  - `public static BadgeStyle BadgeStyles.For(ImeMode mode)`

- [ ] **Step 1: 失敗するテストを書く**

`tests/BadgeStyleTests.cs`:

```csharp
using System.Drawing;

public static class BadgeStyleTests
{
    public static void Run()
    {
        TestRunner.AssertEqual("あ", BadgeStyles.For(ImeMode.Hiragana).Glyph, "glyph/hiragana");
        TestRunner.AssertEqual("ア", BadgeStyles.For(ImeMode.FullKatakana).Glyph, "glyph/fullkana");
        TestRunner.AssertEqual("ｱ", BadgeStyles.For(ImeMode.HalfKatakana).Glyph, "glyph/halfkana");
        TestRunner.AssertEqual("Ａ", BadgeStyles.For(ImeMode.FullAlnum).Glyph, "glyph/fullalnum");
        TestRunner.AssertEqual("A", BadgeStyles.For(ImeMode.HalfAlnum).Glyph, "glyph/halfalnum");
        TestRunner.AssertEqual("A", BadgeStyles.For(ImeMode.Off).Glyph, "glyph/off");
        TestRunner.AssertEqual("■", BadgeStyles.For(ImeMode.Unknown).Glyph, "glyph/unknown");

        // HalfAlnum と Off は文字が同じなので、色で区別できなければならない
        Color halfAlnum = BadgeStyles.For(ImeMode.HalfAlnum).Fore;
        Color off = BadgeStyles.For(ImeMode.Off).Fore;
        TestRunner.AssertTrue(halfAlnum != off, "color/halfalnum-differs-from-off");

        // かな系は同色、英数系は同色
        TestRunner.AssertEqual(BadgeStyles.For(ImeMode.Hiragana).Fore,
                               BadgeStyles.For(ImeMode.FullKatakana).Fore, "color/kana-group");
        TestRunner.AssertEqual(BadgeStyles.For(ImeMode.FullAlnum).Fore,
                               BadgeStyles.For(ImeMode.HalfAlnum).Fore, "color/alnum-group");
    }
}
```

`tests/TestRunner.cs` の `RunAll` に追加:

```csharp
        ImeDecoderTests.Run();
        BadgeStyleTests.Run();
```

- [ ] **Step 2: テストが失敗することを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd"
```

Expected: `BadgeStyles` が存在せずコンパイルエラー

- [ ] **Step 3: 実装を書く**

`src/BadgeStyle.cs`:

```csharp
using System.Drawing;

public struct BadgeStyle
{
    public string Glyph;
    public Color Fore;

    public BadgeStyle(string glyph, Color fore)
    {
        Glyph = glyph;
        Fore = fore;
    }
}

public static class BadgeStyles
{
    private static readonly Color Kana = Color.FromArgb(126, 231, 135);   // 緑
    private static readonly Color Alnum = Color.FromArgb(121, 192, 255);  // 青
    private static readonly Color Off = Color.FromArgb(139, 148, 158);    // グレー

    public static BadgeStyle For(ImeMode mode)
    {
        switch (mode)
        {
            case ImeMode.Hiragana: return new BadgeStyle("あ", Kana);
            case ImeMode.FullKatakana: return new BadgeStyle("ア", Kana);
            case ImeMode.HalfKatakana: return new BadgeStyle("ｱ", Kana);
            case ImeMode.FullAlnum: return new BadgeStyle("Ａ", Alnum);
            case ImeMode.HalfAlnum: return new BadgeStyle("A", Alnum);
            case ImeMode.Off: return new BadgeStyle("A", Off);
            default: return new BadgeStyle("■", Kana);
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: `failures=0` と `EXIT=0`

- [ ] **Step 5: コミット**

```bash
git add src/BadgeStyle.cs tests/BadgeStyleTests.cs tests/TestRunner.cs
git commit -m "feat: モードごとの表示文字と色を追加"
```

---

### Task 3: BadgePlacer — 画面端で反転するバッジ配置

**Files:**
- Create: `src/BadgePlacer.cs`
- Modify: `tests/TestRunner.cs`
- Test: `tests/BadgePlacerTests.cs`

**Interfaces:**
- Consumes: なし
- Produces: `public static Point BadgePlacer.Place(Rectangle caret, Size badge, Rectangle workArea, int offsetX, int offsetY)`

配置規則:
- 既定はキャレットの右上。`x = caret.X + offsetX`, `y = caret.Y - badge.Height + offsetY`
- 右にはみ出す → キャレットの左へ。`x = caret.X - badge.Width - offsetX`
- 上にはみ出す → キャレットの下へ。`y = caret.Bottom - offsetY`
- 最後に作業領域内へクランプする

`offsetY` の既定は `-4`。`y = caret.Y - badge.Height - 4` となり、キャレット上端の 4px 上に置かれる。下へ回すときは `caret.Bottom + 4` になる。

- [ ] **Step 1: 失敗するテストを書く**

`tests/BadgePlacerTests.cs`:

```csharp
using System.Drawing;

public static class BadgePlacerTests
{
    public static void Run()
    {
        Rectangle work = new Rectangle(0, 0, 1920, 1040);
        Size badge = new Size(64, 22);

        // 通常: キャレットの右上
        Point p = BadgePlacer.Place(new Rectangle(500, 300, 1, 20), badge, work, 6, -4);
        TestRunner.AssertEqual(506, p.X, "place/normal-x");
        TestRunner.AssertEqual(274, p.Y, "place/normal-y");

        // 右端: 左へ反転
        Point r = BadgePlacer.Place(new Rectangle(1900, 300, 1, 20), badge, work, 6, -4);
        TestRunner.AssertEqual(1830, r.X, "place/flip-left-x");

        // 上端: 下へ反転
        Point t = BadgePlacer.Place(new Rectangle(500, 2, 1, 20), badge, work, 6, -4);
        TestRunner.AssertEqual(26, t.Y, "place/flip-down-y");

        // 反転してもなお収まらない場合は作業領域内にクランプする
        Point c = BadgePlacer.Place(new Rectangle(0, 0, 1, 20), badge, work, 6, -4);
        TestRunner.AssertTrue(c.X >= work.Left, "place/clamp-left");
        TestRunner.AssertTrue(c.Y >= work.Top, "place/clamp-top");
        TestRunner.AssertTrue(c.X + badge.Width <= work.Right, "place/clamp-right");
        TestRunner.AssertTrue(c.Y + badge.Height <= work.Bottom, "place/clamp-bottom");

        // 原点が (0,0) でないモニタ（マルチモニタの副画面）でも作業領域を尊重する
        Rectangle work2 = new Rectangle(1920, 0, 1280, 1024);
        Point m = BadgePlacer.Place(new Rectangle(3190, 500, 1, 20), badge, work2, 6, -4);
        TestRunner.AssertTrue(m.X >= work2.Left, "place/second-monitor-left");
        TestRunner.AssertTrue(m.X + badge.Width <= work2.Right, "place/second-monitor-right");
    }
}
```

`tests/TestRunner.cs` の `RunAll` に `BadgePlacerTests.Run();` を追加。

- [ ] **Step 2: テストが失敗することを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd"
```

Expected: `BadgePlacer` が存在せずコンパイルエラー

- [ ] **Step 3: 実装を書く**

`src/BadgePlacer.cs`:

```csharp
using System.Drawing;

public static class BadgePlacer
{
    /// <summary>
    /// キャレット矩形の右上を既定位置とし、作業領域からはみ出す場合は反転・クランプする。
    /// workArea は呼び出し側が Screen.FromPoint で選んだモニタの作業領域を渡すこと。
    /// </summary>
    public static Point Place(Rectangle caret, Size badge, Rectangle workArea, int offsetX, int offsetY)
    {
        int x = caret.X + offsetX;
        int y = caret.Y - badge.Height + offsetY;

        if (x + badge.Width > workArea.Right)
        {
            x = caret.X - badge.Width - offsetX;
        }
        if (y < workArea.Top)
        {
            y = caret.Bottom - offsetY;
        }

        if (x < workArea.Left) { x = workArea.Left; }
        if (y < workArea.Top) { y = workArea.Top; }
        if (x + badge.Width > workArea.Right) { x = workArea.Right - badge.Width; }
        if (y + badge.Height > workArea.Bottom) { y = workArea.Bottom - badge.Height; }

        return new Point(x, y);
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: `failures=0` と `EXIT=0`

- [ ] **Step 5: コミット**

```bash
git add src/BadgePlacer.cs tests/BadgePlacerTests.cs tests/TestRunner.cs
git commit -m "feat: 画面端で反転するバッジ配置ロジックを追加"
```

---

### Task 4: Sample と BadgeStateMachine — 表示規則の状態機械

仕様 §5.1 の表示規則をここに閉じ込める。Win32 に一切触れないので完全にテストできる。

**Files:**
- Create: `src/Sample.cs`, `src/BadgeStateMachine.cs`
- Modify: `tests/TestRunner.cs`
- Test: `tests/BadgeStateMachineTests.cs`

**Interfaces:**
- Consumes: `ImeMode`（Task 1）
- Produces:
  - `public struct Sample { public bool HasCaret; public Rectangle Caret; public ImeMode Mode; }`
  - `public enum BadgeAction { None, Show, Hide, Move }`
  - `public class BadgeStateMachine` — コンストラクタ `BadgeStateMachine(int moveThresholdPx)`、メソッド `public BadgeAction Next(Sample s)`、プロパティ `public bool IsShown { get; }`

規則（`Next` 内の判定順）:

1. `!s.HasCaret` → 表示中なら `Hide`、そうでなければ `None`
2. 直前が `HasCaret=false` → `Show`（入力可能になった）
3. モードが直前と異なる → `Show`（**モード変化が移動より優先** — 仕様 §5.1 の競合解決）
4. 表示中かつ、表示時アンカーからの移動量が閾値以上 → `Hide`
5. 表示中かつ移動量が閾値未満 → `Move`（バッジを追従させる）
6. それ以外 → `None`

`Show` を返すときは、そのサンプルのキャレット矩形をアンカーとして記録する。移動量は `Show` した時点の座標が基準であり、直前フレームとの差分ではない。

- [ ] **Step 1: 失敗するテストを書く**

`tests/BadgeStateMachineTests.cs`:

```csharp
using System.Drawing;

public static class BadgeStateMachineTests
{
    private static Sample S(bool hasCaret, int x, int y, ImeMode mode)
    {
        Sample s = new Sample();
        s.HasCaret = hasCaret;
        s.Caret = new Rectangle(x, y, 1, 20);
        s.Mode = mode;
        return s;
    }

    public static void Run()
    {
        // キャレットが現れたら表示する
        BadgeStateMachine m = new BadgeStateMachine(2);
        TestRunner.AssertEqual(BadgeAction.None, m.Next(S(false, 0, 0, ImeMode.Off)), "sm/no-caret-initial");
        TestRunner.AssertEqual(BadgeAction.Show, m.Next(S(true, 100, 100, ImeMode.Off)), "sm/caret-appeared");
        TestRunner.AssertTrue(m.IsShown, "sm/shown-after-show");

        // 閾値未満の移動は追従のみ
        TestRunner.AssertEqual(BadgeAction.Move, m.Next(S(true, 101, 100, ImeMode.Off)), "sm/sub-threshold-move");
        TestRunner.AssertTrue(m.IsShown, "sm/still-shown-after-move");

        // 閾値以上の移動で隠す（入力が始まった）
        TestRunner.AssertEqual(BadgeAction.Hide, m.Next(S(true, 110, 100, ImeMode.Off)), "sm/moved-hides");
        TestRunner.AssertTrue(!m.IsShown, "sm/hidden-after-move");

        // 隠れている間の移動では何も起きない
        TestRunner.AssertEqual(BadgeAction.None, m.Next(S(true, 200, 100, ImeMode.Off)), "sm/move-while-hidden");

        // モード変化で再表示する
        TestRunner.AssertEqual(BadgeAction.Show, m.Next(S(true, 200, 100, ImeMode.Hiragana)), "sm/mode-change-shows");

        // モード変化と移動が同時なら、モード変化が優先されて表示する
        TestRunner.AssertEqual(BadgeAction.Show, m.Next(S(true, 300, 100, ImeMode.FullAlnum)), "sm/mode-change-beats-move");
        TestRunner.AssertTrue(m.IsShown, "sm/shown-after-conflict");

        // 表示直後は、その位置がアンカーなので移動とみなされない
        TestRunner.AssertEqual(BadgeAction.Move, m.Next(S(true, 300, 100, ImeMode.FullAlnum)), "sm/anchor-reset-on-show");

        // キャレットが消えたら隠す
        TestRunner.AssertEqual(BadgeAction.Hide, m.Next(S(false, 0, 0, ImeMode.Off)), "sm/caret-gone-hides");
        TestRunner.AssertTrue(!m.IsShown, "sm/hidden-after-caret-gone");

        // 既に隠れている状態でキャレットが無いままなら何も起きない
        TestRunner.AssertEqual(BadgeAction.None, m.Next(S(false, 0, 0, ImeMode.Off)), "sm/no-caret-repeat");
    }
}
```

`tests/TestRunner.cs` の `RunAll` に `BadgeStateMachineTests.Run();` を追加。

- [ ] **Step 2: テストが失敗することを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd"
```

Expected: `Sample` と `BadgeStateMachine` が存在せずコンパイルエラー

- [ ] **Step 3: 実装を書く**

`src/Sample.cs`:

```csharp
using System.Drawing;

public struct Sample
{
    public bool HasCaret;
    public Rectangle Caret;
    public ImeMode Mode;
}
```

`src/BadgeStateMachine.cs`:

```csharp
using System;
using System.Drawing;

public enum BadgeAction
{
    None,
    Show,
    Hide,
    Move
}

/// <summary>
/// 仕様 §5.1 の表示規則。Win32 に依存しないため単体テストできる。
/// </summary>
public class BadgeStateMachine
{
    private readonly int _moveThresholdPx;
    private bool _hasPrev;
    private Sample _prev;
    private bool _shown;
    private Rectangle _anchor;

    public BadgeStateMachine(int moveThresholdPx)
    {
        _moveThresholdPx = moveThresholdPx;
        _hasPrev = false;
        _shown = false;
    }

    public bool IsShown { get { return _shown; } }

    public BadgeAction Next(Sample s)
    {
        BadgeAction action = Decide(s);
        if (action == BadgeAction.Show)
        {
            _shown = true;
            _anchor = s.Caret;
        }
        else if (action == BadgeAction.Hide)
        {
            _shown = false;
        }
        _prev = s;
        _hasPrev = true;
        return action;
    }

    private BadgeAction Decide(Sample s)
    {
        if (!s.HasCaret)
        {
            return _shown ? BadgeAction.Hide : BadgeAction.None;
        }
        if (!_hasPrev || !_prev.HasCaret)
        {
            return BadgeAction.Show;
        }
        // モード変化は移動より優先する（未確定文字の変換中に切り替えた場合の競合解決）
        if (s.Mode != _prev.Mode)
        {
            return BadgeAction.Show;
        }
        if (!_shown)
        {
            return BadgeAction.None;
        }
        int dx = Math.Abs(s.Caret.X - _anchor.X);
        int dy = Math.Abs(s.Caret.Y - _anchor.Y);
        if (dx >= _moveThresholdPx || dy >= _moveThresholdPx)
        {
            return BadgeAction.Hide;
        }
        return BadgeAction.Move;
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: `failures=0` と `EXIT=0`

- [ ] **Step 5: コミット**

```bash
git add src/Sample.cs src/BadgeStateMachine.cs tests/BadgeStateMachineTests.cs tests/TestRunner.cs
git commit -m "feat: バッジ表示規則の状態機械を追加"
```

---

### Task 5: Settings — ini のパースと読み込み

**Files:**
- Create: `src/Settings.cs`
- Modify: `tests/TestRunner.cs`
- Test: `tests/SettingsTests.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `public class Settings` — public フィールド `Opacity`(double), `ShowDurationMs`(int), `FadeDurationMs`(int), `CaretMoveThresholdPx`(int), `PollIntervalMs`(int), `OffsetX`(int), `OffsetY`(int), `FontSize`(float)
  - `public static Settings Settings.Parse(string[] lines)` — 純粋
  - `public static Settings Settings.Load(string path)` — ファイルが無ければ既定値
  - `public static void Settings.WriteDefault(string path)` — 既定の ini を書き出す

既定値は仕様 §10 のとおり: `Opacity=0.88`, `ShowDurationMs=800`, `FadeDurationMs=200`, `CaretMoveThresholdPx=2`, `PollIntervalMs=120`, `OffsetX=6`, `OffsetY=-4`, `FontSize=10`。

不正な値・未知のキー・空行・`#` や `;` で始まるコメント行は無視し、その項目は既定値のままにする。`Opacity` は 0.1〜1.0 にクランプする。

- [ ] **Step 1: 失敗するテストを書く**

`tests/SettingsTests.cs`:

```csharp
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
```

`tests/TestRunner.cs` の `RunAll` に `SettingsTests.Run();` を追加。

- [ ] **Step 2: テストが失敗することを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd"
```

Expected: `Settings` が存在せずコンパイルエラー

- [ ] **Step 3: 実装を書く**

`src/Settings.cs`:

```csharp
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
            "# cursor-ime-mode 設定ファイル",
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
```

- [ ] **Step 4: テストが通ることを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: `failures=0` と `EXIT=0`

- [ ] **Step 5: コミット**

```bash
git add src/Settings.cs tests/SettingsTests.cs tests/TestRunner.cs
git commit -m "feat: ini 設定のパースと読み込みを追加"
```

---

### Task 6: NativeMethods と ImeReader — IME 状態の取得

ここから Win32 依存部に入る。自動テストはできないので、手動確認を各ステップに置く。

**Files:**
- Create: `src/NativeMethods.cs`, `src/ImeReader.cs`
- Modify: `src/Program.cs`（`--probe-ime` の一時的な確認経路を追加）

**Interfaces:**
- Consumes: `ImeDecoder`, `ImeMode`（Task 1）
- Produces:
  - `public static IntPtr NativeMethods.GetForegroundWindow()`
  - `public static IntPtr NativeMethods.GetFocusWindow(IntPtr foreground)` — `GUITHREADINFO.hwndFocus`。取れなければ `foreground` をそのまま返す
  - `public static bool ImeReader.TryRead(IntPtr hwnd, out ImeMode mode)`

- [ ] **Step 1: `NativeMethods.cs` を書く**

```csharp
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class NativeMethods
{
    public const uint WM_IME_CONTROL = 0x0283;
    public const int IMC_GETCONVERSIONMODE = 0x0001;
    public const int IMC_GETOPENSTATUS = 0x0005;
    public const uint OBJID_CARET = 0xFFFFFFF8;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("user32.dll")]
    public static extern bool GetGUIThreadInfo(int idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder buf, int count);

    [DllImport("imm32.dll")]
    public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam,
        IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);

    [DllImport("oleacc.dll")]
    public static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint objectId,
        ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object ppvObject);

    [DllImport("kernel32.dll")]
    public static extern bool AttachConsole(int processId);

    /// <summary>
    /// フォアグラウンドウィンドウのスレッドでフォーカスを持つウィンドウを返す。
    /// 取得できない場合は引数をそのまま返す。
    /// </summary>
    public static IntPtr GetFocusWindow(IntPtr foreground)
    {
        if (foreground == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        int processId;
        int threadId = GetWindowThreadProcessId(foreground, out processId);
        GUITHREADINFO gti = new GUITHREADINFO();
        gti.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
        if (!GetGUIThreadInfo(threadId, ref gti))
        {
            return foreground;
        }
        return gti.hwndFocus != IntPtr.Zero ? gti.hwndFocus : foreground;
    }
}
```

- [ ] **Step 2: `ImeReader.cs` を書く**

```csharp
using System;

public static class ImeReader
{
    private const uint TimeoutMs = 200;

    /// <summary>
    /// 対象ウィンドウの IME 状態をクロスプロセスで読む。
    /// ImmGetContext / ImmGetOpenStatus はプロセス内専用なので使えない。
    /// </summary>
    public static bool TryRead(IntPtr hwnd, out ImeMode mode)
    {
        mode = ImeMode.Unknown;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        IntPtr imeWnd = NativeMethods.ImmGetDefaultIMEWnd(hwnd);
        if (imeWnd == IntPtr.Zero)
        {
            return false;
        }
        IntPtr result;
        IntPtr ret = NativeMethods.SendMessageTimeout(imeWnd, NativeMethods.WM_IME_CONTROL,
            new IntPtr(NativeMethods.IMC_GETOPENSTATUS), IntPtr.Zero,
            NativeMethods.SMTO_ABORTIFHUNG, TimeoutMs, out result);
        if (ret == IntPtr.Zero)
        {
            return false;
        }
        int open = result.ToInt32();

        ret = NativeMethods.SendMessageTimeout(imeWnd, NativeMethods.WM_IME_CONTROL,
            new IntPtr(NativeMethods.IMC_GETCONVERSIONMODE), IntPtr.Zero,
            NativeMethods.SMTO_ABORTIFHUNG, TimeoutMs, out result);
        if (ret == IntPtr.Zero)
        {
            return false;
        }
        int conv = result.ToInt32();

        mode = ImeDecoder.Decode(open, conv);
        return true;
    }
}
```

- [ ] **Step 3: 手動確認用の一時経路を `Program.cs` に追加する**

`Main` の `--self-test` 分岐の直後に追加:

```csharp
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
```

`Program.cs` の先頭に `using System.Threading;` は不要（完全修飾で書いている）。

- [ ] **Step 4: ビルドして手動確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --probe-ime
```

30 秒間、別のアプリの入力欄にフォーカスを移し、半角/全角キーで IME を切り替える。

Expected: `ok=True` が続き、`mode=` が `Off` ↔ `Hiragana` と切り替わる。無変換キーで `HalfKatakana` / `FullKatakana` にも変わること。

- [ ] **Step 5: 自己テストが壊れていないことを確認する**

```bash
./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: `failures=0` と `EXIT=0`

- [ ] **Step 6: コミット**

```bash
git add src/NativeMethods.cs src/ImeReader.cs src/Program.cs
git commit -m "feat: クロスプロセスで IME 状態を読む ImeReader を追加"
```

---

### Task 7: CaretLocator — MSAA 主・UIA 従のキャレット取得

**Files:**
- Create: `src/CaretLocator.cs`
- Modify: `src/Program.cs`（`--probe-caret` の一時的な確認経路を追加）、`tests/TestRunner.cs`
- Test: `tests/CaretLocatorTests.cs`

**Interfaces:**
- Consumes: `NativeMethods`（Task 6）
- Produces:
  - `public static bool CaretLocator.IsPlausibleCaret(Rectangle r)` — 純粋。テスト対象
  - `public static bool CaretLocator.TryGetCaret(IntPtr hwndFocus, out Rectangle rect)`

**妥当性ガードの根拠**（仕様 §6.2）: UIA は VS Code で `1663x19`（行全体）、Edge で `548x40`（要素全体）を返すことが実測されている。これをキャレットとして使うとバッジが行頭に飛ぶ。**幅が高さの 4 倍を超える矩形は棄却する。** この閾値は Windows Terminal の `9x19` とメモ帳の `1x31` を通し、上記 2 件を弾く。

MSAA の結果にはこのガードを適用しない（仕様 §6.1 のとおり、`hr != 0` / 例外 / `(0,0,0,0)` のみを失敗とする）。

- [ ] **Step 1: 失敗するテストを書く**

`tests/CaretLocatorTests.cs`:

```csharp
using System.Drawing;

public static class CaretLocatorTests
{
    public static void Run()
    {
        // 実測された本物のキャレット矩形は通す
        TestRunner.AssertTrue(CaretLocator.IsPlausibleCaret(new Rectangle(116, 150, 1, 19)), "caret/vscode-msaa");
        TestRunner.AssertTrue(CaretLocator.IsPlausibleCaret(new Rectangle(834, 560, 1, 21)), "caret/edge-msaa");
        TestRunner.AssertTrue(CaretLocator.IsPlausibleCaret(new Rectangle(805, 946, 9, 19)), "caret/terminal-uia");
        TestRunner.AssertTrue(CaretLocator.IsPlausibleCaret(new Rectangle(55, 83, 1, 31)), "caret/notepad-uia");

        // 実測された偽物（行全体・要素全体）は弾く
        TestRunner.AssertTrue(!CaretLocator.IsPlausibleCaret(new Rectangle(116, 150, 1663, 19)), "caret/vscode-uia-line");
        TestRunner.AssertTrue(!CaretLocator.IsPlausibleCaret(new Rectangle(710, 550, 548, 40)), "caret/edge-uia-element");

        // 大きさゼロは弾く
        TestRunner.AssertTrue(!CaretLocator.IsPlausibleCaret(new Rectangle(0, 0, 0, 0)), "caret/zero");
        TestRunner.AssertTrue(!CaretLocator.IsPlausibleCaret(new Rectangle(10, 10, 1, 0)), "caret/zero-height");

        // 境界: 幅がちょうど高さの 4 倍なら通す、超えたら弾く
        TestRunner.AssertTrue(CaretLocator.IsPlausibleCaret(new Rectangle(0, 0, 80, 20)), "caret/boundary-4x");
        TestRunner.AssertTrue(!CaretLocator.IsPlausibleCaret(new Rectangle(0, 0, 81, 20)), "caret/boundary-over-4x");
    }
}
```

`tests/TestRunner.cs` の `RunAll` に `CaretLocatorTests.Run();` を追加。

- [ ] **Step 2: テストが失敗することを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd"
```

Expected: `CaretLocator` が存在せずコンパイルエラー

- [ ] **Step 3: 実装を書く**

`src/CaretLocator.cs`:

```csharp
using System;
using System.Drawing;
using System.Windows.Automation;
using System.Windows.Automation.Text;

public static class CaretLocator
{
    private static readonly Guid IID_IAccessible =
        new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");

    /// <summary>
    /// UIA が返す矩形がキャレットとして妥当かを判定する。
    /// UIA は行全体や要素全体を返すことがあり(VS Code 1663x19、Edge 548x40)、
    /// そのまま使うとバッジが行頭に飛ぶ。
    /// </summary>
    public static bool IsPlausibleCaret(Rectangle r)
    {
        if (r.Width <= 0 || r.Height <= 0)
        {
            return false;
        }
        return r.Width <= r.Height * 4;
    }

    public static bool TryGetCaret(IntPtr hwndFocus, out Rectangle rect)
    {
        rect = Rectangle.Empty;
        if (TryMsaa(hwndFocus, out rect))
        {
            return true;
        }
        return TryUia(out rect);
    }

    private static bool TryMsaa(IntPtr hwnd, out Rectangle rect)
    {
        rect = Rectangle.Empty;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        try
        {
            Guid iid = IID_IAccessible;
            object acc;
            int hr = NativeMethods.AccessibleObjectFromWindow(hwnd, NativeMethods.OBJID_CARET, ref iid, out acc);
            if (hr != 0 || acc == null)
            {
                return false;
            }
            Accessibility.IAccessible ia = acc as Accessibility.IAccessible;
            if (ia == null)
            {
                return false;
            }
            int x, y, w, h;
            ia.accLocation(out x, out y, out w, out h, (object)0);
            if (x == 0 && y == 0 && w == 0 && h == 0)
            {
                return false;
            }
            rect = new Rectangle(x, y, w, h);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryUia(out Rectangle rect)
    {
        rect = Rectangle.Empty;
        try
        {
            AutomationElement el = AutomationElement.FocusedElement;
            if (el == null)
            {
                return false;
            }
            object pattern;
            if (!el.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
            {
                return false;
            }
            TextPattern tp = pattern as TextPattern;
            if (tp == null)
            {
                return false;
            }
            TextPatternRange[] sel = tp.GetSelection();
            if (sel == null || sel.Length == 0)
            {
                return false;
            }
            double[] rects = sel[0].GetBoundingRectangles();
            if (rects.Length < 4)
            {
                // 折りたたまれたキャレットは矩形を返さないので 1 文字分広げる
                TextPatternRange widened = sel[0].Clone();
                widened.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 1);
                rects = widened.GetBoundingRectangles();
            }
            if (rects.Length < 4)
            {
                return false;
            }
            Rectangle candidate = new Rectangle(
                (int)rects[0], (int)rects[1], (int)rects[2], (int)rects[3]);
            if (!IsPlausibleCaret(candidate))
            {
                return false;
            }
            rect = candidate;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: `failures=0` と `EXIT=0`

- [ ] **Step 5: 手動確認用の一時経路を追加してキャレット取得を確認する**

`Program.cs` の `--probe-ime` 分岐の隣に追加:

```csharp
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
```

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --probe-caret
```

30 秒間、メモ帳・Edge・VS Code・Windows Terminal の入力欄にフォーカスして数文字打ち、最後にデスクトップをクリックする。

Expected:
- 各入力欄で `caret=True` となり、打鍵に合わせて `rect` の X が増える
- VS Code で幅が 1663 のような値にならない（妥当性ガードが効いている）
- デスクトップで `caret=False` になる

- [ ] **Step 6: コミット**

```bash
git add src/CaretLocator.cs tests/CaretLocatorTests.cs tests/TestRunner.cs src/Program.cs
git commit -m "feat: MSAA 主・UIA 従のキャレット取得を追加"
```

---

### Task 8: BadgeWindow — フォーカスを奪わないオーバーレイ

**Files:**
- Create: `src/BadgeWindow.cs`
- Modify: `src/Program.cs`（`--probe-badge` の一時的な確認経路を追加）

**Interfaces:**
- Consumes: `BadgeStyle`, `BadgeStyles`（Task 2）、`Settings`（Task 5）
- Produces:
  - `public class BadgeWindow : Form`
  - `public void BadgeWindow.ShowBadge(Point location, ImeMode mode)` — 表示しフェードタイマーを初期化
  - `public void BadgeWindow.MoveBadge(Point location)` — 表示中に位置だけ更新
  - `public void BadgeWindow.HideBadge()` — 即座に隠す
  - `public Size BadgeWindow.BadgeSize { get; }` — 配置計算に使う

**必須のウィンドウスタイル**（仕様 §7.1、実測でフォーカス奪取ゼロを確認済み）:

```
WS_EX_NOACTIVATE  0x08000000
WS_EX_TOOLWINDOW  0x00000080
WS_EX_TRANSPARENT 0x00000020
WS_EX_LAYERED     0x00080000
```

`WS_EX_LAYERED` は明示的に立てる。`Opacity < 1.0` のとき WinForms が自動で立てるが、`Opacity` を 1.0 に設定された場合にクリック貫通が壊れるため、設定値によらず自前で立てる。

**`AutoScaleMode.None` を必ず設定する**（仕様 §7.3）。PerMonitorV2 を宣言すると WinForms がフォントとサイズを DPI に応じて自動スケールし、物理ピクセル座標に対して二重にスケールが掛かる。

- [ ] **Step 1: `BadgeWindow.cs` を書く**

```csharp
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
    private readonly Timer _timer;
    private BadgeStyle _style;
    private int _elapsedMs;
    private bool _fading;

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
        BackColor = Color.FromArgb(30, 30, 30);
        Font = new Font("Yu Gothic UI", _settings.FontSize, FontStyle.Bold, GraphicsUnit.Point);
        Size = new Size(38, 24);
        Opacity = _settings.Opacity;
        DoubleBuffered = true;

        _timer = new Timer();
        _timer.Interval = 30;
        _timer.Tick += OnTick;
    }

    public Size BadgeSize
    {
        get { return Size; }
    }

    public void ShowBadge(Point location, ImeMode mode)
    {
        _style = BadgeStyles.For(mode);
        Location = location;
        _elapsedMs = 0;
        _fading = false;
        Opacity = _settings.Opacity;
        Invalidate();
        if (!Visible)
        {
            Show();
        }
        _timer.Start();
    }

    public void MoveBadge(Point location)
    {
        if (Visible)
        {
            Location = location;
        }
    }

    public void HideBadge()
    {
        _timer.Stop();
        _fading = false;
        if (Visible)
        {
            Hide();
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        _elapsedMs += _timer.Interval;
        if (!_fading)
        {
            if (_elapsedMs >= _settings.ShowDurationMs)
            {
                _fading = true;
                _elapsedMs = 0;
            }
            return;
        }
        if (_settings.FadeDurationMs <= 0)
        {
            HideBadge();
            return;
        }
        double ratio = 1.0 - ((double)_elapsedMs / _settings.FadeDurationMs);
        if (ratio <= 0.0)
        {
            HideBadge();
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
        if (disposing && _timer != null)
        {
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

- [ ] **Step 2: 手動確認用の一時経路を追加する**

`Program.cs` に追加。**この確認が Task 8 の本題**であり、フォーカスを奪わないことを実際に確かめる。

```csharp
        if (args.Length > 0 && args[0] == "--probe-badge")
        {
            Application.EnableVisualStyles();
            Settings st = new Settings();
            BadgeWindow bw = new BadgeWindow(st);
            ImeMode[] modes = new ImeMode[] {
                ImeMode.Hiragana, ImeMode.FullKatakana, ImeMode.HalfKatakana,
                ImeMode.FullAlnum, ImeMode.HalfAlnum, ImeMode.Off, ImeMode.Unknown
            };
            for (int i = 0; i < modes.Length; i++)
            {
                bw.ShowBadge(new System.Drawing.Point(400, 400 + i * 30), modes[i]);
                for (int t = 0; t < 60; t++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(25);
                }
            }
            bw.Dispose();
            return 0;
        }
```

`Program.cs` の先頭に `using System.Windows.Forms;` を追加する。

- [ ] **Step 3: ビルドして手動確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --probe-badge
```

実行中はメモ帳などに文字を打ち続けること。

Expected:
- 画面の (400, 400) 付近に「あ」「ア」「ｱ」「Ａ」「A」「A」「■」が順に表示される
- かな系は緑、英数系は青、OFF はグレーで表示される
- **バッジが出ている間も入力が中断されない**（フォーカスが奪われない）
- バッジをクリックしても下のウィンドウにクリックが通る
- 1.5 秒ほどで各バッジがフェードアウトする

- [ ] **Step 4: 自己テストが壊れていないことを確認する**

```bash
./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: `failures=0` と `EXIT=0`

- [ ] **Step 5: コミット**

```bash
git add src/BadgeWindow.cs src/Program.cs
git commit -m "feat: フォーカスを奪わないバッジウィンドウを追加"
```

---

### Task 9: InputContextWatcher — ポーリングと配線

**Files:**
- Create: `src/InputContextWatcher.cs`

**Interfaces:**
- Consumes: `NativeMethods`, `ImeReader`（Task 6）、`CaretLocator`（Task 7）、`BadgeStateMachine`, `Sample`（Task 4）、`Settings`（Task 5）
- Produces:
  - `public class InputContextWatcher : IDisposable`
  - `public event EventHandler<BadgeEventArgs> ShowRequested`
  - `public event EventHandler<BadgeEventArgs> MoveRequested`
  - `public event EventHandler HideRequested`
  - `public void Start()` / `public void Stop()`
  - `public class BadgeEventArgs : EventArgs { public Rectangle Caret; public ImeMode Mode; }`

IME 読み取りが 3 回連続で失敗したら隠す（仕様 §8）。失敗が続いていない間は直前のモードを保持する。

- [ ] **Step 1: 実装を書く**

`src/InputContextWatcher.cs`:

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

public class BadgeEventArgs : EventArgs
{
    public Rectangle Caret;
    public ImeMode Mode;

    public BadgeEventArgs(Rectangle caret, ImeMode mode)
    {
        Caret = caret;
        Mode = mode;
    }
}

public class InputContextWatcher : IDisposable
{
    private const int MaxImeFailures = 3;

    private readonly Timer _timer;
    private readonly BadgeStateMachine _machine;
    private ImeMode _lastMode;
    private int _imeFailures;

    public event EventHandler<BadgeEventArgs> ShowRequested;
    public event EventHandler<BadgeEventArgs> MoveRequested;
    public event EventHandler HideRequested;

    public InputContextWatcher(Settings settings)
    {
        _machine = new BadgeStateMachine(settings.CaretMoveThresholdPx);
        _lastMode = ImeMode.Unknown;
        _imeFailures = 0;
        _timer = new Timer();
        _timer.Interval = settings.PollIntervalMs;
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        Raise(HideRequested);
    }

    private void OnTick(object sender, EventArgs e)
    {
        Sample s = Read();
        BadgeAction action = _machine.Next(s);
        switch (action)
        {
            case BadgeAction.Show:
                if (ShowRequested != null)
                {
                    ShowRequested(this, new BadgeEventArgs(s.Caret, s.Mode));
                }
                break;
            case BadgeAction.Move:
                if (MoveRequested != null)
                {
                    MoveRequested(this, new BadgeEventArgs(s.Caret, s.Mode));
                }
                break;
            case BadgeAction.Hide:
                Raise(HideRequested);
                break;
        }
    }

    private void Raise(EventHandler h)
    {
        if (h != null)
        {
            h(this, EventArgs.Empty);
        }
    }

    private Sample Read()
    {
        Sample s = new Sample();
        s.HasCaret = false;
        s.Mode = _lastMode;

        IntPtr fg = NativeMethods.GetForegroundWindow();
        IntPtr focus = NativeMethods.GetFocusWindow(fg);

        Rectangle caret;
        if (!CaretLocator.TryGetCaret(focus, out caret))
        {
            return s;
        }

        ImeMode mode;
        if (ImeReader.TryRead(focus, out mode))
        {
            _imeFailures = 0;
            _lastMode = mode;
        }
        else
        {
            // 読めなかった場合は直前の値を保持し、3 回続いたら隠す
            _imeFailures++;
            if (_imeFailures >= MaxImeFailures)
            {
                return s;
            }
        }

        s.HasCaret = true;
        s.Caret = caret;
        s.Mode = _lastMode;
        return s;
    }

    public void Dispose()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
```

- [ ] **Step 2: ビルドが通ることと自己テストが壊れていないことを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: ビルド成功、`failures=0`、`EXIT=0`

- [ ] **Step 3: コミット**

```bash
git add src/InputContextWatcher.cs
git commit -m "feat: ポーリングと状態機械を繋ぐ InputContextWatcher を追加"
```

---

### Task 10: TrayApp と Program — 常駐アプリとして完成させる

**Files:**
- Create: `src/TrayApp.cs`
- Modify: `src/Program.cs`（一時的な `--probe-*` 経路を削除し、本来の起動経路にする）

**Interfaces:**
- Consumes: `InputContextWatcher`（Task 9）、`BadgeWindow`（Task 8）、`BadgePlacer`（Task 3）、`Settings`（Task 5）
- Produces: `public class TrayApp : IDisposable`、`public void TrayApp.Run()`

トレイメニュー（仕様 §10）: 「一時停止 / 再開」「設定ファイルを開く」「設定を再読み込み」「終了」

多重起動は名前付き Mutex で防ぐ（仕様 §8）。

- [ ] **Step 1: `TrayApp.cs` を書く**

```csharp
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
        _watcher.HideRequested += OnHide;
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

    private void OnHide(object sender, EventArgs e)
    {
        _badge.HideBadge();
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
```

- [ ] **Step 2: `Program.cs` を最終形にする**

一時的な `--probe-ime` / `--probe-caret` / `--probe-badge` の分岐を**すべて削除**し、以下に置き換える:

```csharp
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
}
```

- [ ] **Step 3: 自己テストが通ることを確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: `failures=0` と `EXIT=0`

- [ ] **Step 4: 製品版をビルドして起動する**

```bash
cmd.exe //c "$(pwd -W)\build.cmd" && ls -l build/cursor-ime-mode.exe
```

その後、エクスプローラーから `build\cursor-ime-mode.exe` をダブルクリックして起動する。

Expected:
- タスクトレイにアイコンが出る
- `build\settings.ini` が自動生成される
- メモ帳の入力欄をクリックするとキャレット横にバッジが一瞬出て消える
- 半角/全角キーを押すとバッジが再表示され、表示が「あ」↔「A」と変わる
- 文字を打ち始めるとバッジが消える
- もう一度 exe を起動しても二重に常駐しない
- トレイの右クリックメニューから「終了」で終了できる

- [ ] **Step 5: コミット**

```bash
git add src/TrayApp.cs src/Program.cs
git commit -m "feat: トレイ常駐アプリとして完成させる"
```

---

### Task 11: README と手動回帰確認

**Files:**
- Create: `README.md`

- [ ] **Step 1: `README.md` を書く**

````markdown
# cursor-ime-mode

Windows で文字を入力する前に IME の状態を確認できるように、**テキストキャレットのすぐ横に IME の状態バッジを一瞬だけ表示する**常駐ツール。

タスクバーの通知領域は入力位置から遠く、視線移動が大きい。このツールは視線を入力位置から動かさずに状態を確認できるようにする。

## 表示

| 状態 | 表示 | 色 |
|---|---|---|
| ひらがな | あ | 緑 |
| 全角カタカナ | ア | 緑 |
| 半角カタカナ | ｱ | 緑 |
| 全角英数 | Ａ | 青 |
| 半角英数 | A | 青 |
| OFF（直接入力） | A | グレー |

半角英数と OFF は表示文字が同じなので、色で区別する。

## 動作

- 入力可能なコントロールにフォーカスした時と、IME モードが変化した時にバッジが出る
- 800ms 後にフェードアウトする
- **文字を打ち始めるとすぐ消える** — 入力前の確認が目的なので、入力中は邪魔をしない

## ビルド

.NET SDK も NuGet も不要。Windows 標準搭載の C# コンパイラだけを使う。

```
build.cmd
```

`build\cursor-ime-mode.exe` ができる。ダブルクリックで起動するとトレイに常駐する。

## テスト

```
build-test.cmd
build\selftest.exe --self-test
```

終了コードが失敗件数。

## 設定

exe と同じディレクトリの `settings.ini`（初回起動時に自動生成）。トレイメニューの「設定を再読み込み」で反映する。

| キー | 既定値 | 意味 |
|---|---|---|
| `Opacity` | `0.88` | バッジの不透明度 (0.1–1.0) |
| `ShowDurationMs` | `800` | 表示時間 |
| `FadeDurationMs` | `200` | フェードアウト時間 |
| `CaretMoveThresholdPx` | `2` | 入力開始とみなすキャレット移動量 |
| `PollIntervalMs` | `120` | ポーリング間隔 |
| `OffsetX` / `OffsetY` | `6` / `-4` | キャレットからの位置オフセット |
| `FontSize` | `10` | 文字サイズ |

## 既知の制約

- **管理者権限で動くアプリでは表示されない。** Windows の UIPI により、昇格していないプロセスは昇格プロセスのアクセシビリティ情報を読めない。本体を昇格させれば回避できるが、常駐アプリとして割に合わないため対応しない。
- **キャレット位置を公開しないアプリでは表示されない。** MSAA と UI Automation のどちらからもキャレット矩形が取れない場合、位置を推測せず何も表示しない。
- Microsoft IME 以外のサードパーティ IME は未検証。IMM32 互換経路を持つものは動作する見込み。

## 動作確認済み

メモ帳、エクスプローラーの検索欄、Microsoft Edge、Visual Studio Code、Windows Terminal

## 仕組み

- IME 状態: `ImmGetDefaultIMEWnd` で得た IME ウィンドウへクロスプロセスに `WM_IME_CONTROL` を送る（`ImmGetContext` はプロセス内専用のため使えない）
- キャレット位置: MSAA の `OBJID_CARET` を主手段とし、取れない場合のみ UI Automation の `TextPattern` にフォールバックする。Chromium / Electron 系アプリでは MSAA のほうが正確な矩形を返す
- オーバーレイ: `WS_EX_NOACTIVATE` によりフォーカスを奪わない。これがないとバッジを出した瞬間に対象アプリの IME 状態が壊れる
````

- [ ] **Step 2: 手動回帰確認を実施する**（仕様 §9.3）

`build\cursor-ime-mode.exe` を起動した状態で、以下を順に確認する。

1. メモ帳、エクスプローラー検索欄、Edge、Chrome、VS Code、Cursor、Windows Terminal でそれぞれ入力欄にフォーカスし、バッジがキャレット横に出ること
2. 各アプリで半角/全角キーを押し、バッジが再表示されモード表示が変わること
3. 文字を打ち始めるとバッジが消えること
4. デスクトップやボタンにフォーカスしてもバッジが出ないこと
5. バッジ表示中も入力が中断されないこと（フォーカスが奪われないこと）
6. 無変換キーでカタカナに切り替え、「ア」「ｱ」が正しく表示されること
7. 画面右端の入力欄でバッジが左側に反転すること
8. 画面上端の入力欄でバッジが下側に反転すること

結果を README の「動作確認済み」節に反映する（動かなかったアプリがあれば「既知の制約」に追記する）。

- [ ] **Step 3: 自己テストを最終確認する**

```bash
cmd.exe //c "$(pwd -W)\build-test.cmd" && ./build/selftest.exe --self-test; echo "EXIT=$?"
```

Expected: `failures=0` と `EXIT=0`

- [ ] **Step 4: コミット**

```bash
git add README.md
git commit -m "docs: README と手動回帰手順を追加"
```

---

## 仕様との対応表

| 仕様セクション | 対応タスク |
|---|---|
| §2.3 変換モードのビット値 | Task 1（`ImeDecoder` + 実測値をテストで固定） |
| §3 実装方式 | Task 1（`build.cmd` / `build-test.cmd`） |
| §4 アーキテクチャ | Task 1–10（コンポーネントごとに 1 タスク） |
| §5 データフロー | Task 9（`InputContextWatcher`） |
| §5.1 表示・非表示の規則 | Task 4（`BadgeStateMachine`） |
| §6.1 MSAA 主手段 | Task 7 |
| §6.2 UIA 補完 + 妥当性ガード | Task 7（`IsPlausibleCaret`） |
| §6.3 入力可能性の判定 | Task 7（`TryGetCaret` の失敗＝入力不可） |
| §7 バッジ表示内容 | Task 2（`BadgeStyles`） |
| §7.1 ウィンドウスタイル | Task 8 |
| §7.2 配置 | Task 3（`BadgePlacer`）+ Task 10（モニタ選択） |
| §7.3 DPI | Task 1（`app.manifest`）+ Task 8（`AutoScaleMode.None`） |
| §8 エラー処理 | Task 1（Unknown）、Task 9（3 回失敗）、Task 7（例外）、Task 10（Mutex） |
| §9 テスト方針 | Task 1（ランナー）、各タスクのテスト、Task 11（手動回帰） |
| §10 設定ファイル | Task 5（パース）+ Task 10（トレイメニュー） |
| §11 スコープ外 | README「既知の制約」に記載（Task 11） |
