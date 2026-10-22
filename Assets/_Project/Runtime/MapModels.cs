using System;
using System.Collections.Generic;
namespace HGame.Unity
{
    [Serializable] public class WorldMap
    {
        public string Id = "", Name = "", Theme = "town";
        public int Chapter = 1, Width, Height, SpawnX, SpawnY;
        public List<string> Ground = new List<string>();
        public List<WorldDecoration> Decorations = new List<WorldDecoration>();
        public List<WorldInteraction> Interactions = new List<WorldInteraction>();
        public List<WorldGate> Gates = new List<WorldGate>();
    }
    [Serializable] public class WorldDecoration
    { public int X, Y; public string SpriteKey = "tree"; public bool Blocks = true; }
    [Serializable] public class WorldInteraction
    { public string Id = "", Label = "", Kind = "", NodeId = "", SpriteKey = "sign"; public int X, Y; }
    [Serializable] public class WorldGate
    { public int X, Y; public string NodeId = ""; }
}
