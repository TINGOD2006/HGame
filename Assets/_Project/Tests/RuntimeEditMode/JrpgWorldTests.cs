using System;
using System.Collections.Generic;
using System.Linq;
using HGame.Core;
using HGame.Unity;
using NUnit.Framework;
using UnityEngine;

namespace HGame.Tests.Runtime
{
    public class JrpgWorldTests
    {
        private GameData data;
        private Dictionary<string, WorldMap> maps;
        [SetUp] public void SetUp()
        {
            data = DefinitionLoader.Load();
            maps = Resources.LoadAll<TextAsset>("Demo/Maps")
                .Where(a => a.text.TrimStart().StartsWith("{"))
                .Select(a => JsonUtility.FromJson<WorldMap>(a.text)).ToDictionary(m => m.Id);
        }
        private static string Json(object value) { return JsonUtility.ToJson(value); }
        private RunState DungeonRun(string mapId = "dungeon_c1")
        {
            var run = GameSession.NewGame(data, "qa-jrpg").CaptureRun();
            run.Location = GameLocation.Dungeon; run.Day = 20; run.Chapter = maps[mapId].Chapter;
            run.Chapters = new List<ChapterProgress> { new ChapterProgress { Chapter = run.Chapter } };
            return run;
        }
        private Vector2Int Approach(WorldMap map, RunState run, WorldInteraction marker)
        {
            for (int direction = 0; direction < 4; direction++)
            {
                var p = new Vector2Int(marker.X, marker.Y) - GridRules.FacingDelta(direction);
                if (GridRules.IsReachable(map, run, p, data)) return p;
            }
            Assert.Fail("No reachable approach to " + map.Id + "/" + marker.Id);
            return Vector2Int.zero;
        }

        [Test] public void MapReferencesAndSequentialApproachesAreValid_WithoutInitialProgressBypass()
        {
            CollectionAssert.AreEquivalent(new[] { "town", "inn", "dungeon_c1", "dungeon_c2" }, maps.Keys);
            foreach (var map in maps.Values)
            {
                Assert.That(GridRules.Validate(map), Is.Empty, map.Id);
                Assert.That(map.Interactions.Select(i => i.Id).Distinct().Count(), Is.EqualTo(map.Interactions.Count), map.Id);
                foreach (var gate in map.Gates) Assert.That(data.Node(gate.NodeId), Is.Not.Null, gate.NodeId);
                var run = map.Theme == "dungeon" ? DungeonRun(map.Id) : GameSession.NewGame(data, "qa-town").CaptureRun();
                if (map.Id == "inn") run.Slot = TimeSlot.LateNight;
                foreach (var marker in map.Interactions.Where(i => i.NodeId == "")) Approach(map, run, marker);
                var nodeMarkers = map.Interactions.Where(i => i.NodeId != "").OrderBy(i => data.Node(i.NodeId).Index).ToList();
                foreach (var marker in nodeMarkers)
                {
                    var node = data.Node(marker.NodeId);
                    Assert.That(node, Is.Not.Null, marker.NodeId);
                    Assert.That(node.Chapter, Is.EqualTo(run.Chapter));
                    Approach(map, run, marker);
                    run.Chapters[0].ClearedNodeIds.Add(node.Id);
                    run.CurrentNodeId = node.Id;
                }
                if (nodeMarkers.Count > 1)
                {
                    var firstRun = DungeonRun(map.Id);
                    var later = nodeMarkers.Last();
                    bool canReach = Enumerable.Range(0, 4).Any(d => GridRules.IsReachable(map, firstRun,
                        new Vector2Int(later.X, later.Y) - GridRules.FacingDelta(d), data));
                    Assert.That(canReach, Is.False, "Walking cannot bypass uncleared node gates: " + map.Id);
                }
            }
        }

        [Test] public void GridMovementIsSingleAxisAndCannotCrossWallsWaterDecorationsOrInteractionObjects()
        {
            foreach (var input in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(0, -1), Vector2.zero })
            {
                var axis = GridRules.SelectAxis(input);
                Assert.That(Math.Abs(axis.x) + Math.Abs(axis.y), Is.EqualTo(input == Vector2.zero ? 0 : 1));
                if (axis != Vector2Int.zero) Assert.That(GridRules.FacingDelta(GridRules.Facing(axis)), Is.EqualTo(axis));
            }
            var map = maps["town"]; var run = GameSession.NewGame(data, "qa-collision").CaptureRun();
            Assert.That(GridRules.CanEnter(map, -1, 0, run, data), Is.False);
            Assert.That(GridRules.CanEnter(map, map.Width, map.Height - 1, run, data), Is.False);
            for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
                if (map.Ground[y][x] == '#' || map.Ground[y][x] == '~')
                    Assert.That(GridRules.CanEnter(map, x, y, run, data), Is.False);
            foreach (var d in map.Decorations.Where(d => d.Blocks)) Assert.That(GridRules.CanEnter(map, d.X, d.Y, run, data), Is.False);
            foreach (var i in map.Interactions) Assert.That(GridRules.CanEnter(map, i.X, i.Y, run, data), Is.False);
            var shop = map.Interactions.First(i => i.Kind == "Shop");
            var below = new Vector2Int(shop.X, shop.Y + 1);
            Assert.That(GridRules.InteractionAhead(map, below, 0).Id, Is.EqualTo(shop.Id));
            Assert.That(GridRules.InteractionAhead(map, below, 2), Is.Null, "Interaction requires facing the object.");
        }

        [Test] public void PresentationUpdatesChangeOnlyWorld_AndCloneCallerValues()
        {
            var session = GameSession.NewGame(data, "qa-world-only");
            var before = session.Snapshot;
            var world = GridRules.ResolvePosition(maps["town"], before.Run, data);
            session.UpdatePresentation(world);
            world.X++; world.Facing = 1;
            session.UpdatePresentation(world);
            var after = session.Snapshot;
            Assert.That(Json(after.Meta), Is.EqualTo(Json(before.Meta)));
            Assert.That(Json(after.Profile), Is.EqualTo(Json(before.Profile)));
            after.Run.World = before.Run.World;
            Assert.That(Json(after.Run), Is.EqualTo(Json(before.Run)), "No head, time, resource, route or experience changes.");
            int x = session.Snapshot.Run.World.X;
            world.X++;
            Assert.That(session.Snapshot.Run.World.X, Is.EqualTo(x));
        }

        [Test] public void SameTimelineHeadLoadRestoresDifferentSavedPositionWithoutCountingRewind()
        {
            var session = GameSession.NewGame(data, "qa-position-load");
            session.UpdatePresentation(WorldSaveRules.Resolve(session.CaptureRun()));
            var stored = session.CaptureRun();
            var store = new MemoryStore();
            var repository = new SaveRepository(data, store, WorldSaveRules.CanSave, WorldSaveRules.Resolve);
            repository.Save(session, "manual");
            var moved = session.Snapshot.Run.World; moved.X++; moved.Facing = 1;
            Assert.That(GridRules.ValidStoredPosition(maps["town"], session.CaptureRun(), moved, data), Is.True);
            session.UpdatePresentation(moved);
            Assert.That(session.Snapshot.Run.TimelineHeadId, Is.EqualTo(stored.TimelineHeadId));
            string message;
            var loaded = repository.Load(session, "manual", LoadIntent.ManualLoad, out message);
            Assert.That(Json(loaded.CaptureRun()), Is.EqualTo(Json(stored)));
            Assert.That(loaded.Snapshot.Meta.EffectiveRewinds, Is.Zero);
        }

        [Test] public void LegacyAndInvalidPositionsRecoverToReachableSpawn_WhileValidPositionIsKept()
        {
            var run = GameSession.NewGame(data, "qa-fallback").CaptureRun(); var map = maps["town"];
            var valid = GridRules.ResolvePosition(map, run, data); valid.X++;
            run.World = valid;
            Assert.That(Json(WorldSaveRules.Resolve(run)), Is.EqualTo(Json(valid)));
            foreach (var invalid in new WorldPresentationState[]
            {
                null, new WorldPresentationState(),
                new WorldPresentationState { MapId = "town", X = 0, Y = 0, Chapter = 1 },
                new WorldPresentationState { MapId = "old-map", X = valid.X, Y = valid.Y, Chapter = 1 },
                new WorldPresentationState { MapId = "town", X = valid.X, Y = valid.Y, Chapter = 1, AnchorNodeId = "stale-node" }
            })
            {
                run.World = invalid;
                var resolved = WorldSaveRules.Resolve(run);
                Assert.That(GridRules.ValidStoredPosition(map, run, resolved, data), Is.True, Json(invalid));
            }
            run.World = valid; run.Slot = TimeSlot.LateNight;
            Assert.That(WorldSaveRules.Resolve(run).MapId, Is.EqualTo("inn"));
            run.Location = GameLocation.Dungeon; run.Chapter = 2; run.CurrentNodeId = "";
            Assert.That(WorldSaveRules.Resolve(run).MapId, Is.EqualTo("dungeon_c2"));
        }

        [Test] public void StoredPositionBeyondDateOrExpeditionGateMustFallBackBeforeLockedRegion()
        {
            var map = maps["dungeon_c1"]; var run = DungeonRun();
            run.CurrentNodeId = "c1_n04";
            run.Chapters[0].ClearedNodeIds.AddRange(data.Nodes.Where(n => n.Chapter == 1 && n.Index <= 4).Select(n => n.Id));
            var later = map.Interactions.First(i => i.NodeId == "c1_n05");
            foreach (bool limitedByDate in new[] { true, false })
            {
                run.Day = limitedByDate ? 2 : 3;
                run.ExpeditionNewNodes = limitedByDate ? 0 : 4;
                run.World = GridRules.MakePosition(map, run, new Vector2Int(later.X, later.Y + 1), 0);
                Assert.That(GridRules.ValidStoredPosition(map, run, run.World, data), Is.False);
                var resolved = WorldSaveRules.Resolve(run);
                Assert.That(GridRules.ValidStoredPosition(map, run, resolved, data), Is.True,
                    "Fallback must apply Core date/expedition gates, not only static collision and cleared-node gates.");
            }
        }

        [Test] public void LeavingSaveAnchorRejectsManualSaveAndDoesNotOverwriteAutoSlot()
        {
            var fresh = GameSession.NewGame(data, "qa-save-gate"); var run = fresh.CaptureRun();
            run.Location = GameLocation.Dungeon; run.Day = 3; run.CurrentNodeId = "c1_n02";
            run.Chapters[0].ClearedNodeIds.AddRange(new[] { "c1_n01", "c1_n02" });
            var map = maps["dungeon_c1"]; var marker = map.Interactions.First(i => i.NodeId == run.CurrentNodeId);
            var atAnchor = Approach(map, run, marker);
            run.World = GridRules.MakePosition(map, run, atAnchor, 0);
            var session = new GameSession(data, run, fresh.CaptureMeta(), fresh.CaptureProfile());
            var store = new MemoryStore();
            var repository = new SaveRepository(data, store, WorldSaveRules.CanSave, WorldSaveRules.Resolve);
            Assert.That(repository.CanSave(session.Snapshot), Is.True);
            repository.Save(session, "manual"); repository.Persist(session);
            string manual = store.Read("manual.json"), auto = store.Read("auto.json");
            Vector2Int away = Vector2Int.zero; bool found = false;
            for (int y = 0; y < map.Height && !found; y++) for (int x = 0; x < map.Width && !found; x++)
            {
                var candidate = new Vector2Int(x, y);
                if (!GridRules.Adjacent(candidate, marker) && GridRules.IsReachable(map, run, candidate, data))
                { away = candidate; found = true; }
            }
            Assert.That(found, Is.True);
            session.UpdatePresentation(GridRules.MakePosition(map, run, away, 2));
            Assert.That(repository.CanSave(session.Snapshot), Is.False);
            Assert.Throws<InvalidOperationException>(() => repository.Save(session, "manual"));
            repository.Persist(session);
            Assert.That(store.Read("manual.json"), Is.EqualTo(manual));
            Assert.That(store.Read("auto.json"), Is.EqualTo(auto), "Persist may write Meta/Profile, but not unsafe run position.");
        }

        private sealed class MemoryStore : ISaveStore
        {
            private readonly Dictionary<string, string> files = new Dictionary<string, string>();
            public bool Exists(string name) { return files.ContainsKey(name); }
            public string Read(string name) { return files[name]; }
            public void WriteAtomic(string name, string value)
            { if (files.ContainsKey(name)) files[name + ".bak"] = files[name]; files[name] = value; }
        }
    }
}
