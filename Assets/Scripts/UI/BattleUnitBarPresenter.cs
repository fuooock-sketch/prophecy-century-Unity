using System;

namespace ProphecyCentury.UI
{
    internal static class BattleUnitBarPresenter
    {
        internal readonly struct BarState
        {
            public BarState(float amount, string text)
            {
                Amount = amount;
                Text = text;
            }

            public float Amount { get; }
            public string Text { get; }
        }

        internal static BarState CalculateCount(int currentCount, int maxCount)
        {
            var safeCurrent = Math.Max(0, currentCount);
            var safeMax = Math.Max(1, maxCount);
            var amount = Math.Min(1f, safeCurrent / (float)safeMax);
            return new BarState(amount, $"{safeCurrent}/{safeMax}");
        }
    }
}
