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
    // トレイアイコン（TrayIcon）もこの色を再利用する。数値をここ以外に複製しないこと。
    public static readonly Color Kana = Color.FromArgb(126, 231, 135);   // 緑
    public static readonly Color Alnum = Color.FromArgb(121, 192, 255);  // 青
    public static readonly Color Off = Color.FromArgb(139, 148, 158);    // グレー

    /// <summary>バッジ本体の背景色。BadgeWindow と TrayIcon の両方が参照する。</summary>
    public static readonly Color Background = Color.FromArgb(30, 30, 30); // 濃いグレー

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
