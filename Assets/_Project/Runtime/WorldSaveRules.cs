using System.Collections.Generic;
using System.Linq;
using HGame.Core;
using UnityEngine;
namespace HGame.Unity
{
    public static class WorldSaveRules
    {
        private static Dictionary<string,WorldMap> maps;
        private static GameData data;
        public static WorldMap MapFor(RunState run)
        {
            if(maps == null) maps = Resources.LoadAll<TextAsset>("Demo/Maps").Where(x=>x.text.TrimStart().StartsWith("{"))
                .Select(x=>JsonUtility.FromJson<WorldMap>(x.text)).Where(x=>x != null && x.Id != "").ToDictionary(x=>x.Id);
            return maps[GridRules.MapIdFor(run)];
        }
        private static GameData Data { get { return data ?? (data=DefinitionLoader.Load()); } }
        public static WorldPresentationState Resolve(RunState run) { return GridRules.ResolvePosition(MapFor(run),run,Data); }
        public static bool CanSave(GameSnapshot snapshot)
        {
            var r = snapshot.Run; var map = MapFor(r);
            if(!GridRules.ValidStoredPosition(map,r,r.World,Data)) return false;
            if(r.Location == GameLocation.Town) return true;
            if(r.Location != GameLocation.Dungeon || !string.IsNullOrEmpty(r.PendingSupplyNodeId)) return false;
            var marker = map.Interactions.FirstOrDefault(x=>x.NodeId == r.CurrentNodeId && x.NodeId != "");
            return GridRules.Adjacent(new Vector2Int(r.World.X,r.World.Y),marker);
        }
    }
}
