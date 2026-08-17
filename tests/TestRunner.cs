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
        BadgeStyleTests.Run();
        BadgePlacerTests.Run();
        SettingsTests.Run();
        BadgeStateMachineTests.Run();
        CaretLocatorTests.Run();
        Console.WriteLine("ran=" + _total + " failures=" + _failures);
        return _failures;
    }
}
