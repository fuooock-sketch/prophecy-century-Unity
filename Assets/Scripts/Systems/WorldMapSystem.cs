using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Data;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public sealed class WorldMapSystem
    {
        private const int MoveCost = 1;
        private static readonly HashSet<string> BattleNodeTypes = new HashSet<string>
        {
            "battle",
            "safe_battle",
            "normal_battle",
            "pressure_battle",
            "hard_battle",
            "elite_battle",
            "guard_battle",
            "boss_guard",
            "boss"
        };

        private static readonly HashSet<string> NonBattleNodeTypes = new HashSet<string>
        {
            "resource",
            "event",
            "rest"
        };

        public static bool IsBattleNodeType(string nodeType)
        {
            return !string.IsNullOrWhiteSpace(nodeType) && BattleNodeTypes.Contains(nodeType);
        }

        public static bool IsBossNodeType(string nodeType)
        {
            return nodeType == "boss";
        }

        public static bool IsNonBattleNodeType(string nodeType)
        {
            return !string.IsNullOrWhiteSpace(nodeType) && NonBattleNodeTypes.Contains(nodeType);
        }

        public static int GetBattleRewardRerolls(string nodeType)
        {
            switch (nodeType)
            {
                case "pressure_battle":
                case "hard_battle":
                case "guard_battle":
                    return 2;
                case "elite_battle":
                case "boss_guard":
                    return 3;
                case "boss":
                    return 0;
                default:
                    return 1;
            }
        }

        public static int GetBaseFateDamage(string nodeType)
        {
            switch (nodeType)
            {
                case "pressure_battle":
                    return 5;
                case "hard_battle":
                    return 7;
                case "elite_battle":
                    return 9;
                case "guard_battle":
                    return 8;
                case "boss_guard":
                    return 10;
                default:
                    return 3;
            }
        }

        public IReadOnlyList<WorldMapNodeDefinition> GetAvailableDestinations(RunState run, WorldMapDefinition map)
        {
            if (run == null || map?.nodes == null || map.connections == null || string.IsNullOrWhiteSpace(run.currentNodeId))
            {
                return Array.Empty<WorldMapNodeDefinition>();
            }

            return map.connections
                .Where(connection => connection != null && connection.fromNodeId == run.currentNodeId)
                .Select(connection => FindNode(map, connection.toNodeId))
                .Where(node => node != null && CanMoveToNode(run, map, node.id))
                .ToList();
        }

        public bool CanMoveToNode(RunState run, WorldMapDefinition map, string targetNodeId)
        {
            if (run == null || map == null || string.IsNullOrWhiteSpace(targetNodeId))
            {
                return false;
            }

            if (run.remainingMovePoints < MoveCost || string.IsNullOrWhiteSpace(run.currentNodeId))
            {
                return false;
            }

            var currentNode = FindNode(map, run.currentNodeId);
            var targetNode = FindNode(map, targetNodeId);
            if (currentNode == null || targetNode == null)
            {
                return false;
            }

            var targetState = FindNodeState(run, targetNodeId);
            if (targetState == null || !targetState.isVisible)
            {
                return false;
            }

            return targetNode.layer == currentNode.layer + 1 && HasConnection(map, run.currentNodeId, targetNodeId);
        }

        public bool MoveToNode(RunState run, WorldMapDefinition map, string targetNodeId)
        {
            if (!CanMoveToNode(run, map, targetNodeId))
            {
                return false;
            }

            run.remainingMovePoints = Math.Max(0, run.remainingMovePoints - MoveCost);
            run.currentNodeId = targetNodeId;

            var targetState = FindNodeState(run, targetNodeId);
            if (targetState != null)
            {
                targetState.isVisible = true;
                targetState.isVisited = true;
            }

            RevealFutureLayers(run, map, targetNodeId);
            return true;
        }

        public NodeEventResult ResolveNode(RunState run, WorldMapDefinition map)
        {
            if (run == null || map == null || string.IsNullOrWhiteSpace(run.currentNodeId))
            {
                return NodeEventResult.Empty();
            }

            return ResolveNode(run, map, run.currentNodeId);
        }

        public NodeEventResult ResolveNode(RunState run, WorldMapDefinition map, string nodeId)
        {
            var node = FindNode(map, nodeId);
            if (run == null || node == null)
            {
                return NodeEventResult.Empty();
            }

            var state = FindNodeState(run, nodeId);
            if (state != null && state.isCleared && node.type != "start")
            {
                return NodeEventResult.Cleared(node);
            }

            // ── V1 node type dispatch (supports 11 types via 7 battle aliases + 4 non-battle) ──
            switch (node.type)
            {
                // Battle family (all require enemyPresetId, trigger combat)
                case "safe_battle":
                case "normal_battle":
                case "pressure_battle":
                case "hard_battle":
                case "elite_battle":
                case "guard_battle":
                case "boss_guard":
                case "battle":   // legacy alias
                    return NodeEventResult.Battle(node);

                // Boss
                case "boss":
                    return NodeEventResult.Boss(node);

                // Non-battle family
                case "resource":
                    return NodeEventResult.Resource(node);
                case "treasure":
                    return NodeEventResult.Treasure(node);
                case "event":
                    return NodeEventResult.Event(node);
                case "rest":
                    return NodeEventResult.Rest(node);

                case "start":
                case "empty":
                default:
                    return NodeEventResult.Empty(node);
            }
        }

        public bool MarkNodeCleared(RunState run, WorldMapDefinition map, string nodeId)
        {
            var node = FindNode(map, nodeId);
            var state = FindNodeState(run, nodeId);
            if (node == null || state == null)
            {
                return false;
            }

            state.isVisible = true;
            state.isVisited = true;
            state.isCleared = true;
            RevealFutureLayers(run, map, nodeId);
            return true;
        }

        public bool CheckVictoryCondition(RunState run, WorldMapDefinition map)
        {
            if (run == null || map?.nodes == null)
            {
                return false;
            }

            foreach (var bossNode in map.nodes.Where(node => node != null && node.type == "boss"))
            {
                var state = FindNodeState(run, bossNode.id);
                if (state != null && state.isCleared)
                {
                    run.campaignCompleted = true;
                    run.phase = GamePhase.Victory;
                    run.state = "victory";
                    return true;
                }
            }

            return false;
        }

        public static void RevealFutureLayers(RunState run, WorldMapDefinition map, string nodeId, int layerDepth = 3)
        {
            if (run == null || map?.connections == null)
            {
                return;
            }

            var queue = new Queue<(string NodeId, int Depth)>();
            var visited = new HashSet<string> { nodeId };
            queue.Enqueue((nodeId, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Depth >= layerDepth)
                {
                    continue;
                }

                foreach (var connection in map.connections.Where(connection => connection != null && connection.fromNodeId == current.NodeId))
                {
                    var state = FindNodeState(run, connection.toNodeId);
                    if (state != null)
                    {
                        state.isVisible = true;
                    }

                    if (visited.Add(connection.toNodeId))
                    {
                        queue.Enqueue((connection.toNodeId, current.Depth + 1));
                    }
                }
            }
        }

        private static bool HasConnection(WorldMapDefinition map, string fromNodeId, string toNodeId)
        {
            return map?.connections != null
                && map.connections.Any(connection => connection != null
                    && connection.fromNodeId == fromNodeId
                    && connection.toNodeId == toNodeId);
        }

        private static WorldMapNodeDefinition FindNode(WorldMapDefinition map, string nodeId)
        {
            return map?.nodes?.FirstOrDefault(node => node != null && node.id == nodeId);
        }

        private static WorldMapNodeState FindNodeState(RunState run, string nodeId)
        {
            return run?.worldMapNodes?.FirstOrDefault(state => state != null && state.nodeId == nodeId);
        }
    }

    [Serializable]
    public enum NodeEventType
    {
        None,
        AlreadyCleared,
        Battle,
        Resource,
        Treasure,
        Event,
        Rest,
        Boss
    }

    [Serializable]
    public sealed class NodeEventResult
    {
        public NodeEventType eventType;
        public string nodeId;
        public string nodeType;
        public string enemyPresetId;
        public int rewardGold;
        public string rewardTreasureId;
        public string rewardUnitId;
        public bool alreadyCleared;
        public bool requiresBattle;

        public static NodeEventResult Empty(WorldMapNodeDefinition node = null)
        {
            return FromNode(NodeEventType.None, node);
        }

        public static NodeEventResult Cleared(WorldMapNodeDefinition node)
        {
            var result = FromNode(NodeEventType.AlreadyCleared, node);
            result.alreadyCleared = true;
            return result;
        }

        public static NodeEventResult Battle(WorldMapNodeDefinition node)
        {
            var result = FromNode(NodeEventType.Battle, node);
            result.requiresBattle = true;
            return result;
        }

        public static NodeEventResult Boss(WorldMapNodeDefinition node)
        {
            var result = FromNode(NodeEventType.Boss, node);
            result.requiresBattle = true;
            return result;
        }

        public static NodeEventResult Resource(WorldMapNodeDefinition node)
        {
            return FromNode(NodeEventType.Resource, node);
        }

        public static NodeEventResult Treasure(WorldMapNodeDefinition node)
        {
            return FromNode(NodeEventType.Treasure, node);
        }

        public static NodeEventResult Event(WorldMapNodeDefinition node)
        {
            return FromNode(NodeEventType.Event, node);
        }

        public static NodeEventResult Rest(WorldMapNodeDefinition node)
        {
            return FromNode(NodeEventType.Rest, node);
        }

        private static NodeEventResult FromNode(NodeEventType eventType, WorldMapNodeDefinition node)
        {
            return new NodeEventResult
            {
                eventType = eventType,
                nodeId = node?.id,
                nodeType = node?.type,
                enemyPresetId = node?.enemyPresetId,
                rewardGold = Math.Max(0, node?.reward?.gold ?? 0),
                rewardTreasureId = node?.reward?.treasureId,
                rewardUnitId = node?.reward?.unitId
            };
        }
    }
}
