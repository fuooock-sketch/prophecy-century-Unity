using System;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public sealed class DayNightCycleController
    {
        public GamePhase CurrentPhase(RunState run)
        {
            if (run == null)
            {
                return GamePhase.NightManage;
            }

            return run.phase;
        }

        public bool CanEnterNight(RunState run)
        {
            return run != null
                && run.phase == GamePhase.DayExplore
                && run.state != "battle"
                && run.state != "victory"
                && run.state != "gameover";
        }

        public bool EnterNight(RunState run)
        {
            if (!CanEnterNight(run))
            {
                return false;
            }

            run.phase = GamePhase.NightManage;
            run.state = "manage";
            return true;
        }

        public bool EndNight(RunState run)
        {
            if (run == null || run.phase != GamePhase.NightManage)
            {
                return false;
            }

            run.dayCount = Math.Max(0, run.dayCount) + 1;
            StartNewDay(run);
            return true;
        }

        public bool StartNewDay(RunState run)
        {
            if (run == null)
            {
                return false;
            }

            if (run.maxMovePoints <= 0)
            {
                run.maxMovePoints = 4;
            }

            if (run.dayCount <= 0)
            {
                run.dayCount = 1;
            }

            run.remainingMovePoints = run.maxMovePoints;
            run.phase = GamePhase.DayExplore;
            run.state = "day";
            return true;
        }
    }
}
