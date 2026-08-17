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
