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
