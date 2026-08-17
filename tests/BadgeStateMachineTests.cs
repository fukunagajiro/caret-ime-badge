using System.Drawing;

public static class BadgeStateMachineTests
{
    private static Sample S(bool hasCaret, int x, int y, ImeMode mode)
    {
        return S(hasCaret, x, y, mode, false);
    }

    private static Sample S(bool hasCaret, int x, int y, ImeMode mode, bool focusChanged)
    {
        Sample s = new Sample();
        s.HasCaret = hasCaret;
        s.Caret = new Rectangle(x, y, 1, 20);
        s.Mode = mode;
        s.FocusChanged = focusChanged;
        return s;
    }

    /// <summary>組み立てた（要素矩形由来の）キャレット位置を持つサンプル。</summary>
    private static Sample Syn(bool hasCaret, int x, int y, ImeMode mode)
    {
        Sample s = S(hasCaret, x, y, mode);
        s.CaretIsSynthesized = true;
        return s;
    }

    public static void Run()
    {
        BadgeStateMachine m = new BadgeStateMachine(2, 800, 0);
        long t = 1000;

        // キャレットが無い間は何も起きない
        TestRunner.AssertEqual(BadgeAction.None, m.Next(S(false, 0, 0, ImeMode.Off), t), "sm/no-caret-initial");

        // キャレットが現れたら表示する
        TestRunner.AssertEqual(BadgeAction.Show, m.Next(S(true, 100, 100, ImeMode.Off), t), "sm/caret-appeared");
        TestRunner.AssertTrue(m.IsShown, "sm/shown-after-show");

        // 閾値未満の移動は追従のみ
        TestRunner.AssertEqual(BadgeAction.Move, m.Next(S(true, 101, 100, ImeMode.Off), t + 120), "sm/sub-threshold-move");
        TestRunner.AssertTrue(m.IsShown, "sm/still-shown-after-move");

        // 閾値以上の移動でフェードする（入力が始まった）
        TestRunner.AssertEqual(BadgeAction.Fade, m.Next(S(true, 110, 100, ImeMode.Off), t + 240), "sm/moved-fades");
        TestRunner.AssertTrue(!m.IsShown, "sm/hidden-after-move");

        // 隠れている間の移動では何も起きない
        TestRunner.AssertEqual(BadgeAction.None, m.Next(S(true, 200, 100, ImeMode.Off), t + 360), "sm/move-while-hidden");

        // モード変化で再表示する
        TestRunner.AssertEqual(BadgeAction.Show, m.Next(S(true, 200, 100, ImeMode.Hiragana), t + 480), "sm/mode-change-shows");

        // モード変化と移動が同時なら、モード変化が優先されて表示する
        TestRunner.AssertEqual(BadgeAction.Show, m.Next(S(true, 300, 100, ImeMode.FullAlnum), t + 600), "sm/mode-change-beats-move");
        TestRunner.AssertTrue(m.IsShown, "sm/shown-after-conflict");

        // 表示直後は、その位置がアンカーなので移動とみなされない
        TestRunner.AssertEqual(BadgeAction.Move, m.Next(S(true, 300, 100, ImeMode.FullAlnum), t + 720), "sm/anchor-reset-on-show");

        // 表示時間を過ぎたらフェードする（表示開始は t+600 なので t+1400 で 800ms 経過）
        TestRunner.AssertEqual(BadgeAction.Fade, m.Next(S(true, 300, 100, ImeMode.FullAlnum), t + 1400), "sm/show-duration-elapsed");
        TestRunner.AssertTrue(!m.IsShown, "sm/hidden-after-duration");

        // フェード完了後、同じアプリ内で別の入力欄に移った場合も表示する。
        // キャレットが一度も消えない経路なので、FocusChanged が無いと再表示できない。
        TestRunner.AssertEqual(BadgeAction.Show,
            m.Next(S(true, 700, 400, ImeMode.FullAlnum, true), t + 2000), "sm/focus-change-shows");
        TestRunner.AssertTrue(m.IsShown, "sm/shown-after-focus-change");

        // キャレットが消えたら即座に隠す（フェードなし）
        TestRunner.AssertEqual(BadgeAction.HideNow, m.Next(S(false, 0, 0, ImeMode.Off), t + 2100), "sm/caret-gone-hides-now");
        TestRunner.AssertTrue(!m.IsShown, "sm/hidden-after-caret-gone");

        // 既に隠れている状態でキャレットが無いままなら何も起きない
        TestRunner.AssertEqual(BadgeAction.None, m.Next(S(false, 0, 0, ImeMode.Off), t + 2200), "sm/no-caret-repeat");

        // フォーカス移動が無ければ、隠れたままキャレットが飛んでも表示しない
        BadgeStateMachine m2 = new BadgeStateMachine(2, 800, 0);
        TestRunner.AssertEqual(BadgeAction.Show, m2.Next(S(true, 10, 10, ImeMode.Off), 0), "sm2/initial-show");
        TestRunner.AssertEqual(BadgeAction.Fade, m2.Next(S(true, 90, 10, ImeMode.Off), 100), "sm2/typing-fades");
        TestRunner.AssertEqual(BadgeAction.None, m2.Next(S(true, 200, 10, ImeMode.Off), 200), "sm2/no-focus-no-show");

        // 累積する微小移動はアンカー基準で測る。直前フレーム基準の実装だと dx が毎回 1 のままで
        // 永久に Move となり、入力してもバッジが消えないという実使用の不具合になる。
        // ここは移動量の閾値 (2px) のちょうど境界も兼ねている。
        BadgeStateMachine m3 = new BadgeStateMachine(2, 800, 0);
        TestRunner.AssertEqual(BadgeAction.Show, m3.Next(S(true, 100, 100, ImeMode.Off), 0), "sm3/initial-show");
        TestRunner.AssertEqual(BadgeAction.Move, m3.Next(S(true, 101, 100, ImeMode.Off), 100), "sm3/drift-still-move");
        TestRunner.AssertEqual(BadgeAction.Fade, m3.Next(S(true, 102, 100, ImeMode.Off), 200), "sm3/drift-from-anchor-fades");

        // 縦方向の移動でも同じ規則が働く（エディタでの行移動が該当する）
        BadgeStateMachine m4 = new BadgeStateMachine(2, 800, 0);
        TestRunner.AssertEqual(BadgeAction.Show, m4.Next(S(true, 100, 100, ImeMode.Off), 0), "sm4/initial-show");
        TestRunner.AssertEqual(BadgeAction.Move, m4.Next(S(true, 100, 101, ImeMode.Off), 100), "sm4/vertical-sub-threshold");
        TestRunner.AssertEqual(BadgeAction.Fade, m4.Next(S(true, 100, 102, ImeMode.Off), 200), "sm4/vertical-drift-fades");

        // 表示中に別の入力欄へフォーカスが移った場合、アンカーと表示開始時刻が引き直される
        BadgeStateMachine m5 = new BadgeStateMachine(2, 800, 0);
        TestRunner.AssertEqual(BadgeAction.Show, m5.Next(S(true, 10, 10, ImeMode.Off), 0), "sm5/initial-show");
        TestRunner.AssertEqual(BadgeAction.Show, m5.Next(S(true, 500, 300, ImeMode.Off, true), 100), "sm5/focus-change-while-shown");
        TestRunner.AssertTrue(m5.IsShown, "sm5/still-shown-after-refocus");
        TestRunner.AssertEqual(BadgeAction.Move, m5.Next(S(true, 500, 300, ImeMode.Off), 200), "sm5/anchor-and-timer-reset");

        // 組み立てた位置は要素のレイアウトに追従して揺れるため、移動判定を適用しない。
        // 実測: メモ帳の検索バーは 120ms ごとに最大 95px 揺れる。
        BadgeStateMachine m6 = new BadgeStateMachine(2, 800, 0);
        TestRunner.AssertEqual(BadgeAction.Show, m6.Next(Syn(true, 196, 104, ImeMode.Off), 0), "sm6/synth-initial-show");
        TestRunner.AssertEqual(BadgeAction.None, m6.Next(Syn(true, 225, 105, ImeMode.Off), 120), "sm6/synth-jitter-ignored");
        TestRunner.AssertEqual(BadgeAction.None, m6.Next(Syn(true, 291, 109, ImeMode.Off), 240), "sm6/synth-large-jitter-ignored");
        TestRunner.AssertTrue(m6.IsShown, "sm6/still-shown-through-jitter");

        // 表示時間の経過ではフェードする
        TestRunner.AssertEqual(BadgeAction.Fade, m6.Next(Syn(true, 196, 104, ImeMode.Off), 800), "sm6/synth-duration-fades");

        // 組み立てでない位置には従来通り移動判定が効く（回帰防止）
        BadgeStateMachine m7 = new BadgeStateMachine(2, 800, 0);
        TestRunner.AssertEqual(BadgeAction.Show, m7.Next(S(true, 196, 104, ImeMode.Off), 0), "sm7/real-initial-show");
        TestRunner.AssertEqual(BadgeAction.Fade, m7.Next(S(true, 225, 105, ImeMode.Off), 120), "sm7/real-jitter-fades");

        // 組み立て → 本物 への遷移（利用者が入力を始めた）ではフェードする
        BadgeStateMachine m8 = new BadgeStateMachine(2, 800, 0);
        TestRunner.AssertEqual(BadgeAction.Show, m8.Next(Syn(true, 207, 100, ImeMode.Off), 0), "sm8/synth-show");
        TestRunner.AssertEqual(BadgeAction.Fade, m8.Next(S(true, 207, 110, ImeMode.Off), 120), "sm8/synth-to-real-fades");

        // 組み立てた位置でもモード変化では再表示する
        BadgeStateMachine m9 = new BadgeStateMachine(2, 800, 0);
        TestRunner.AssertEqual(BadgeAction.Show, m9.Next(Syn(true, 196, 104, ImeMode.Off), 0), "sm9/synth-show");
        TestRunner.AssertEqual(BadgeAction.Fade, m9.Next(Syn(true, 196, 104, ImeMode.Off), 800), "sm9/synth-fade");
        TestRunner.AssertEqual(BadgeAction.Show, m9.Next(Syn(true, 196, 104, ImeMode.Hiragana), 900), "sm9/synth-mode-change-reshows");

        // 猶予期間中は大きな移動でもフェードしない（コンソールの出力等）
        BadgeStateMachine m10 = new BadgeStateMachine(2, 800, 500);
        TestRunner.AssertEqual(BadgeAction.Show, m10.Next(S(true, 34, 75, ImeMode.Off), 0), "sm10/show");
        TestRunner.AssertEqual(BadgeAction.Move, m10.Next(S(true, 34, 132, ImeMode.Off), 250), "sm10/grace-absorbs-jump");
        TestRunner.AssertTrue(m10.IsShown, "sm10/still-shown-in-grace");

        // 猶予が明ければ従来どおり移動でフェードする
        BadgeStateMachine m11 = new BadgeStateMachine(2, 800, 500);
        TestRunner.AssertEqual(BadgeAction.Show, m11.Next(S(true, 100, 100, ImeMode.Off), 0), "sm11/show");
        TestRunner.AssertEqual(BadgeAction.Fade, m11.Next(S(true, 120, 100, ImeMode.Off), 600), "sm11/after-grace-fades");

        // 猶予中にアンカーが更新される。更新されないと猶予明けに即フェードしてしまう。
        BadgeStateMachine m12 = new BadgeStateMachine(2, 800, 500);
        TestRunner.AssertEqual(BadgeAction.Show, m12.Next(S(true, 100, 100, ImeMode.Off), 0), "sm12/show");
        TestRunner.AssertEqual(BadgeAction.Move, m12.Next(S(true, 300, 100, ImeMode.Off), 250), "sm12/grace-move");
        TestRunner.AssertEqual(BadgeAction.Move, m12.Next(S(true, 300, 100, ImeMode.Off), 600), "sm12/anchor-followed-no-fade");
        TestRunner.AssertTrue(m12.IsShown, "sm12/still-shown-after-grace");

        // 組み立てた位置は猶予に関係なく固定される（None を返す）
        BadgeStateMachine m13 = new BadgeStateMachine(2, 800, 500);
        TestRunner.AssertEqual(BadgeAction.Show, m13.Next(Syn(true, 196, 104, ImeMode.Off), 0), "sm13/synth-show");
        TestRunner.AssertEqual(BadgeAction.None, m13.Next(Syn(true, 291, 109, ImeMode.Off), 250), "sm13/synth-still-pinned-in-grace");
    }
}
