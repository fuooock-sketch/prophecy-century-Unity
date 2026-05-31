using System;
using ProphecyCentury.UI;

public static class BattleUnitBarPresenterTest
{
    public static int Main()
    {
        var normal = BattleUnitBarPresenter.CalculateCount(7, 10);
        AssertClose(0.7f, normal.Amount, "count bar should use current count over max count");
        AssertEqual("7/10", normal.Text, "count text should show current and max count");

        var clamped = BattleUnitBarPresenter.CalculateCount(-3, 0);
        AssertClose(0f, clamped.Amount, "negative current count should clamp to empty");
        AssertEqual("0/1", clamped.Text, "invalid max count should clamp to one");

        var overflow = BattleUnitBarPresenter.CalculateCount(12, 10);
        AssertClose(1f, overflow.Amount, "count bar should not overflow");
        AssertEqual("12/10", overflow.Text, "text should preserve the true current count");

        return 0;
    }

    private static void AssertClose(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.0001f)
        {
            throw new Exception($"{message}: expected {expected}, got {actual}");
        }
    }

    private static void AssertEqual(string expected, string actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new Exception($"{message}: expected {expected}, got {actual}");
        }
    }
}
