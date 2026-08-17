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
