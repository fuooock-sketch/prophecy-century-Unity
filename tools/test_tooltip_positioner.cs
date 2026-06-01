using System;
using ProphecyCentury.UI;

public static class TooltipPositionerTest
{
    public static int Main()
    {
        TooltipPositioner.Calculate(
            pointerX: 1000f,
            pointerY: 380f,
            panelWidth: 668f,
            panelHeight: 900f,
            screenWidth: 1280f,
            screenHeight: 720f,
            out var x,
            out var y,
            out var visualWidth,
            out var visualHeight,
            out var scale);

        AssertClose(708f, y, "top edge should stay inside the screen with margin");
        AssertClose(696f, visualHeight, "oversized tooltip should fit available screen height");
        AssertClose(12f, y - visualHeight, "bottom edge should keep the screen margin");
        AssertClose(0.7733333f, scale, "oversized tooltip should be scaled to fit");
        AssertTrue(x >= 12f, "left edge should stay inside screen");
        AssertTrue(x + visualWidth <= 1268.001f, "right edge should stay inside screen");

        return 0;
    }

    private static void AssertClose(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.01f)
        {
            throw new Exception($"{message}: expected {expected}, got {actual}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
