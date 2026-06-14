using System;
using System.Linq;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;

public static class Program
{
    public static int Main()
    {
        ForestGemCardsDoNotSynthesize();
        NormalCardsStillSynthesize();
        Console.WriteLine("SynthesisSystem tests passed.");
        return 0;
    }

    private static void ForestGemCardsDoNotSynthesize()
    {
        var run = new RunState();
        run.handCards.Add(new UnitCardState { unitId = "forest_gem", name = "密林宝钻" });
        run.handCards.Add(new UnitCardState { unitId = "forest_gem", name = "密林宝钻" });
        run.handCards.Add(new UnitCardState { unitId = "forest_gem", name = "密林宝钻" });

        var synthesized = new SynthesisSystem().TrySynthesizeAll(run);

        Assert(!synthesized, "Forest gems should not trigger synthesis.");
        Assert(run.handCards.Count == 3, "Forest gems should remain in hand.");
        Assert(run.handCards.All(card => card.unitId == "forest_gem" && !card.isGolden), "Forest gems should remain non-golden.");
    }

    private static void NormalCardsStillSynthesize()
    {
        ProphecyCentury.Core.ProphecyGameSession.Instance.Data.Units.Add(new ProphecyCentury.Data.UnitDefinition
        {
            id = "archer",
            name = "Archer",
            star = 1,
            hp = 10,
            attack = 2,
            power = 1,
            speed = 1,
            defaultCount = 2
        });

        var run = new RunState();
        run.handCards.Add(new UnitCardState { unitId = "archer", name = "Archer", baseCount = 2 });
        run.handCards.Add(new UnitCardState { unitId = "archer", name = "Archer", baseCount = 2 });
        run.handCards.Add(new UnitCardState { unitId = "archer", name = "Archer", baseCount = 2 });

        var synthesized = new SynthesisSystem().TrySynthesizeAll(run);

        Assert(synthesized, "Normal cards should still synthesize.");
        Assert(run.handCards.Count == 1, "Three normal cards should become one card.");
        Assert(run.handCards[0].unitId == "archer" && run.handCards[0].isGolden, "Synthesized normal card should be golden.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
