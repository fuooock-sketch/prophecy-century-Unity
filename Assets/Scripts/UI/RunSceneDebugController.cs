using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Systems;
using UnityEngine;

namespace ProphecyCentury.UI
{
    public sealed class RunSceneDebugController : MonoBehaviour
    {
        private readonly RunFlowController _flow = new RunFlowController();

        [SerializeField] private bool initializeShopOnStart = true;

        private void Start()
        {
            if (ProphecyGameSession.Instance == null)
            {
                Debug.LogError("ProphecyGameSession is missing. Add BootstrapInstaller to the scene.");
                return;
            }

            if (initializeShopOnStart && ProphecyGameSession.Instance.CurrentRun.shopCards.Count == 0)
            {
                _flow.ShopSystem.RefreshShop(ProphecyGameSession.Instance.CurrentRun);
            }

            PrintSnapshot("Run scene initialized");
        }

        [ContextMenu("Refresh Shop")]
        public void RefreshShop()
        {
            _flow.ShopSystem.RefreshShop(ProphecyGameSession.Instance.CurrentRun);
            PrintSnapshot("Shop refreshed");
        }

        [ContextMenu("Buy First Card")]
        public void BuyFirstCard()
        {
            _flow.ShopSystem.BuyFromShop(ProphecyGameSession.Instance.CurrentRun, 0);
            PrintSnapshot("Bought first card");
        }

        [ContextMenu("Deploy First Card")]
        public void DeployFirstCard()
        {
            _flow.BoardSystem.DeployFromHand(ProphecyGameSession.Instance.CurrentRun, 0);
            PrintSnapshot("Deployed first card");
        }

        [ContextMenu("Start Battle")]
        public void StartBattle()
        {
            _flow.EnterBattlePhase();
            PrintSnapshot("Battle phase started");
        }

        [ContextMenu("Next Round")]
        public void NextRound()
        {
            _flow.NextRound();
            PrintSnapshot("Advanced round");
        }

        private static void PrintSnapshot(string reason)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var summary = string.Join(", ", run.boardUnits.Select(unit => $"{unit.boardSlotId}:{unit.name}"));
            Debug.Log(
                $"[ProphecyCentury] {reason} | State={run.state} Round={run.round} Gold={run.gold} Shop={run.shopCards.Count} Hand={run.handCards.Count} Board={run.boardUnits.Count} [{summary}]");
        }
    }
}
