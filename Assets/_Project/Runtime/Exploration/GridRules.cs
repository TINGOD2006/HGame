using System;
using System.Collections.Generic;
using System.Linq;
using HGame.Core;
using UnityEngine;

namespace HGame.Unity
{
    public static class GridRules
    {
        // Grid Y points down; facing: up, right, down, left.
        public static Vector2Int FacingDelta(int facing)
        {
            switch (facing) { case 0: return Vector2Int.down; case 1: return Vector2Int.right;
                case 3: return Vector2Int.left; default: return Vector2Int.up; }
        }
        public static int Facing(Vector2Int axis)
        { return axis.x > 0 ? 1 : axis.x < 0 ? 3 : axis.y < 0 ? 0 : 2; }
        public static Vector2Int SelectAxis(Vector2 input)
        {
            if (Mathf.Abs(input.x) > .1f) return new Vector2Int(input.x > 0 ? 1 : -1, 0);
            if (Mathf.Abs(input.y) > .1f) return new Vector2Int(0, input.y > 0 ? 1 : -1);
            return Vector2Int.zero;
        }
        public static bool InBounds(WorldMap map, int x, int y)
        { return map != null && x >= 0 && y >= 0 && x < map.Width && y < map.Height; }
        public static bool CanEnter(WorldMap map, int x, int y, RunState run, GameData data = null)
        { return string.IsNullOrEmpty(BlockedReason(map, x, y, run, data)); }
        public static string BlockedReason(WorldMap map, int x, int y, RunState run, GameData data = null)
        {
            if (!InBounds(map, x, y)) return "已到地圖邊界。";
            if (map.Ground == null || y >= map.Ground.Count || map.Ground[y] == null || x >= map.Ground[y].Length) return "這個格子尚未配置。";
            char ground = map.Ground[y][x];
            if (ground == '#' || ground == '~') return "前方無法通行。";
            if (map.Decorations != null && map.Decorations.Any(d => d.X == x && d.Y == y && d.Blocks)) return "前方有障礙物。";
            if (map.Interactions != null && map.Interactions.Any(i => i.X == x && i.Y == y)) return "請面向此處按 E／Enter 互動。";
            if (map.Gates != null)
                foreach (var gate in map.Gates.Where(g => g.X == x && g.Y == y))
                {
                    if (run == null) return "通道尚未開放。";
                    var progress = run.Chapters == null ? null : run.Chapters.FirstOrDefault(p => p.Chapter == run.Chapter);
                    if (progress == null || !progress.ClearedNodeIds.Contains(gate.NodeId)) return "請先完成前方節點。";
                    if (data != null)
                    {
                        var required = data.Node(gate.NodeId);
                        var next = required == null ? null : data.Nodes.FirstOrDefault(n => n.Chapter == required.Chapter && n.Index == required.Index + 1);
                        if (next != null && !progress.ClearedNodeIds.Contains(next.Id))
                        {
                            string coreReason = DungeonRules.NodeReason(StateCloner.Clone(run), data, next);
                            if (coreReason != "") return coreReason;
                        }
                    }
                }
            return "";
        }
        public static WorldInteraction InteractionAhead(WorldMap map, Vector2Int position, int facing)
        {
            if (map == null || map.Interactions == null) return null;
            Vector2Int target = position + FacingDelta(facing);
            return map.Interactions.FirstOrDefault(i => i.X == target.x && i.Y == target.y);
        }
        public static bool Adjacent(Vector2Int position, WorldInteraction interaction)
        { return interaction != null && Math.Abs(position.x - interaction.X) + Math.Abs(position.y - interaction.Y) == 1; }
        public static string MapIdFor(RunState run)
        {
            if (run.Location == GameLocation.Town || run.Location == GameLocation.GameOver || run.Location == GameLocation.DemoComplete)
                return run.Slot == TimeSlot.LateNight && run.Location == GameLocation.Town ? "inn" : "town";
            return run.Chapter == 2 ? "dungeon_c2" : "dungeon_c1";
        }
        public static bool IsReachable(WorldMap map, RunState run, Vector2Int target, GameData data = null)
        {
            if (!CanEnter(map, target.x, target.y, run, data)) return false;
            var start = new Vector2Int(map.SpawnX, map.SpawnY);
            if (!CanEnter(map, start.x, start.y, run, data)) return false;
            var visited = new HashSet<Vector2Int>(); var queue = new Queue<Vector2Int>();
            queue.Enqueue(start); visited.Add(start);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue(); if (p == target) return true;
                for (int d = 0; d < 4; d++)
                {
                    var next = p + FacingDelta(d);
                    if (!visited.Contains(next) && CanEnter(map, next.x, next.y, run, data)) { visited.Add(next); queue.Enqueue(next); }
                }
            }
            return false;
        }
        public static bool ValidStoredPosition(WorldMap map, RunState run, WorldPresentationState value, GameData data = null)
        {
            if (value == null || value.Version != 1 || value.Chapter != run.Chapter || value.MapId != map.Id || value.Facing < 0 || value.Facing > 3) return false;
            if (value.AnchorNodeId != (run.CurrentNodeId ?? "")) return false;
            return IsReachable(map, run, new Vector2Int(value.X, value.Y), data);
        }
        public static WorldPresentationState ResolvePosition(WorldMap map, RunState run, GameData data = null)
        {
            if (map == null || run == null) throw new ArgumentNullException("地圖／當局");
            if (ValidStoredPosition(map, run, run.World, data)) return StateCloner.Clone(run.World);
            Vector2Int spawn = new Vector2Int(map.SpawnX, map.SpawnY);
            WorldInteraction anchor = map.Interactions == null ? null : map.Interactions.FirstOrDefault(i => !string.IsNullOrEmpty(i.NodeId) && i.NodeId == run.CurrentNodeId);
            if (anchor != null)
            {
                foreach (int face in new[] { 0, 1, 2, 3 })
                {
                    Vector2Int candidate = new Vector2Int(anchor.X, anchor.Y) - FacingDelta(face);
                    if (IsReachable(map, run, candidate, data)) return MakePosition(map, run, candidate, face);
                }
            }
            if (CanEnter(map, spawn.x, spawn.y, run, data)) return MakePosition(map, run, spawn, 2);
            // Broken/migrated maps are recovered deterministically, never into a wall.
            for (int radius = 1; radius <= map.Width + map.Height; radius++)
                for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
                    if (Math.Abs(x - spawn.x) + Math.Abs(y - spawn.y) == radius && CanEnter(map, x, y, run))
                        return MakePosition(map, run, new Vector2Int(x, y), 2);
            throw new InvalidOperationException("地圖沒有可通行的安全出生點：" + map.Id);
        }
        public static WorldPresentationState MakePosition(WorldMap map, RunState run, Vector2Int position, int facing)
        {
            return new WorldPresentationState { Version = 1, MapId = map.Id, Chapter = run.Chapter,
                X = position.x, Y = position.y, Facing = facing, AnchorNodeId = run.CurrentNodeId ?? "" };
        }
        public static List<string> Validate(WorldMap map)
        {
            var errors = new List<string>();
            if (map == null) { errors.Add("地圖不存在。"); return errors; }
            if (string.IsNullOrEmpty(map.Id) || map.Width < 1 || map.Height < 1) errors.Add("地圖識別／尺寸不合法。");
            if (map.Ground == null || map.Ground.Count != map.Height || map.Ground.Any(r => r == null || r.Length != map.Width)) errors.Add("地板列與尺寸不符。");
            foreach (var i in map.Interactions ?? new List<WorldInteraction>()) if (!InBounds(map, i.X, i.Y)) errors.Add("互動超出邊界：" + i.Id);
            foreach (var g in map.Gates ?? new List<WorldGate>()) if (!InBounds(map, g.X, g.Y)) errors.Add("通道超出邊界：" + g.NodeId);
            return errors;
        }
    }
}
