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
