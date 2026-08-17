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
    }
}
