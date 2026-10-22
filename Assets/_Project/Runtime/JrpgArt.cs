using System.Collections.Generic;
using UnityEngine;
namespace HGame.Unity
{
    public sealed class JrpgArt
    {
        private readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public Sprite Get(string key)
        {
            if (cache.TryGetValue(key, out var found)) return found;
            string pack = "town"; int tile = 0;
            switch (key)
            {
                case "grass": tile = 0; break; case "path": tile = 25; break;
                case "water": return cache[key] = Water(); case "wall": pack = "dungeon"; tile = 39; break;
                case "floor": pack = "dungeon"; tile = 48; break; case "carpet": pack = "dungeon"; tile = 68; break;
                case "tree": tile = 5; break; case "bush": tile = 1; break; case "flower": tile = 2; break;
                case "rock": tile = 43; break; case "door": tile = 87; break; case "sign": tile = 83; break;
                case "npc": pack = "dungeon"; tile = 84; break;
                case "save": pack = "dungeon"; tile = 32; break; case "chest": pack = "dungeon"; tile = 62; break;
                case "enemy": pack = "dungeon"; tile = 108; break; case "boss": pack = "dungeon"; tile = 121; break;
                case "bed": pack = "dungeon"; tile = 69; break; case "table": pack = "dungeon"; tile = 71; break;
                case "torch": pack = "dungeon"; tile = 125; break;
                case "hero_up": case "hero_down": case "hero_left": case "hero_right": pack = "dungeon"; tile = 85; break;
                case "house": return cache[key] = House();
                case "roof": tile = 53; break;
                case "shadow":
                    var dot = new Texture2D(1, 1); dot.SetPixel(0, 0, Color.white); dot.Apply();
                    return cache[key] = Sprite.Create(dot, new Rect(0, 0, 1, 1), new Vector2(.5f,.5f), 1);
            }
            var t = Resources.Load<Texture2D>("Art/Jrpg/" + pack + "/tile_" + tile.ToString("0000"));
            return cache[key] = t == null ? null : Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f,.5f), 16);
        }
        private Sprite Water()
        {
            var t=new Texture2D(16,16,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Point;
            for(int y=0;y<16;y++) for(int x=0;x<16;x++)
                t.SetPixel(x,y,y%7==1&&x%11<6?new Color(.42f,.73f,.80f):new Color(.24f,.52f,.68f));
            t.Apply();return Sprite.Create(t,new Rect(0,0,16,16),new Vector2(.5f,.5f),16);
        }
        private Sprite House()
        {
            var t = new Texture2D(48,48,TextureFormat.RGBA32,false); t.filterMode = FilterMode.Point;
            var clear = new Color[48*48]; t.SetPixels(clear);
            int[] tiles = {85,86,87,72,73,75,52,53,54};
            for(int i=0;i<9;i++)
            {
                var part = Resources.Load<Texture2D>("Art/Jrpg/town/tile_" + tiles[i].ToString("0000"));
                if(part != null) t.SetPixels((i%3)*16,(i/3)*16,16,16,part.GetPixels());
            }
            t.Apply(); return Sprite.Create(t,new Rect(0,0,48,48),new Vector2(.5f,.5f),16);
        }
    }
}
