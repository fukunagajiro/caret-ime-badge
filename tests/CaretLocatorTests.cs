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
        TestRunner.AssertTrue(!CaretLocator.IsPlausibleCaret(new Rectangle(10, 10, 0, 19)), "caret/zero-width");

        // 境界: 幅がちょうど高さの 4 倍なら通す、超えたら弾く
        TestRunner.AssertTrue(CaretLocator.IsPlausibleCaret(new Rectangle(0, 0, 80, 20)), "caret/boundary-4x");
        TestRunner.AssertTrue(!CaretLocator.IsPlausibleCaret(new Rectangle(0, 0, 81, 20)), "caret/boundary-over-4x");

        // --- ChooseCaret: 合成ロジック ---
        Rectangle got;

        // MSAA が妥当なら採用し、UIA は見ない
        TestRunner.AssertTrue(
            CaretLocator.ChooseCaret(true, new Rectangle(55, 99, 1, 15), true, new Rectangle(55, 83, 1, 31), out got),
            "choose/msaa-wins");
        TestRunner.AssertEqual(new Rectangle(55, 99, 1, 15), got, "choose/msaa-wins-rect");

        // 回帰テスト: Edge でボタンにフォーカスが移ったときの古いキャレット。
        // 位置はあるが幅0。UIA が妥当な矩形を持っていても採用してはならない。
        TestRunner.AssertTrue(
            !CaretLocator.ChooseCaret(true, new Rectangle(235, 49, 0, 20), true, new Rectangle(108, 52, 1, 16), out got),
            "choose/msaa-degenerate-rejects-without-consulting-uia");
        TestRunner.AssertEqual(Rectangle.Empty, got, "choose/msaa-degenerate-rect-empty");

        // MSAA が答えなかった場合は UIA を採用する(Windows Terminal)
        TestRunner.AssertTrue(
            CaretLocator.ChooseCaret(false, Rectangle.Empty, true, new Rectangle(805, 946, 9, 19), out got),
            "choose/uia-fallback");
        TestRunner.AssertEqual(new Rectangle(805, 946, 9, 19), got, "choose/uia-fallback-rect");

        // MSAA が答えず、UIA が行全体を返した場合(VS Code)は却下
        TestRunner.AssertTrue(
            !CaretLocator.ChooseCaret(false, Rectangle.Empty, true, new Rectangle(116, 150, 1663, 19), out got),
            "choose/uia-line-rejected");
        TestRunner.AssertEqual(Rectangle.Empty, got, "choose/uia-line-rect-empty");

        // どちらも答えなかった場合
        TestRunner.AssertTrue(
            !CaretLocator.ChooseCaret(false, Rectangle.Empty, false, Rectangle.Empty, out got),
            "choose/both-absent");
        TestRunner.AssertEqual(Rectangle.Empty, got, "choose/both-absent-rect-empty");
    }
}
