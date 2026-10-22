using System;
using System.Collections.Generic;
using System.Linq;
using HGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace HGame.Unity
{
    public sealed class WorldController : MonoBehaviour
    {
        private GameData data;
        private Func<GameSnapshot> read;
        private Action<WorldPresentationState> moved;
        private Action<WorldInteraction> interact;
        private Func<string, Sprite> sprite;
        private readonly Dictionary<string, WorldMap> maps = new Dictionary<string, WorldMap>();
        private readonly Dictionary<string, SpriteRenderer> markers = new Dictionary<string, SpriteRenderer>();
        private GameObject visualRoot;
        private SpriteRenderer hero, shadow;
        private Camera worldCamera;
        private Vector2Int position, destination;
        private Vector3 moveFrom;
        private float moveProgress;
        private int facing = 2;
        private bool moving, visible = true, locked;
        private bool interactionArmed = true;
        private string lastCampaign = "", anchor = "", blockedHint = "";
        private string battleSafeNode = "";
        private Vector2Int battleSafePosition;
        private WorldMap map;
        private Sprite fallback;
        public bool InputLocked { get { return locked; } set { locked = value; if (value) interactionArmed = false; } }
        public bool IsMoving { get { return moving; } }
        public Vector2Int Position { get { return position; } }
        public WorldMap Map { get { return map; } }
        public WorldInteraction Nearby { get { return GridRules.InteractionAhead(map, position, facing); } }
        public string Hint
        {
            get
            {
                if (locked || !visible) return "";
                var nearby = Nearby;
                return nearby == null ? blockedHint : "E／Enter　" + nearby.Label;
            }
        }
        public void Initialize(GameData gameData, Func<GameSnapshot> snapshot, Action<WorldPresentationState> onMoved,
            Action<WorldInteraction> onInteract, Func<string, Sprite> spriteProvider)
        {
            data = gameData; read = snapshot; moved = onMoved; interact = onInteract; sprite = spriteProvider;
            maps.Clear();
            foreach (var asset in Resources.LoadAll<TextAsset>("Demo/Maps"))
            {
                if (!asset.text.TrimStart().StartsWith("{")) continue;
                var loaded = JsonUtility.FromJson<WorldMap>(asset.text);
                if (loaded == null || string.IsNullOrEmpty(loaded.Id)) continue;
                var errors = GridRules.Validate(loaded);
                if (errors.Count > 0) throw new InvalidOperationException(loaded.Id + "：" + string.Join("；", errors));
                maps.Add(loaded.Id, loaded);
            }
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                var cameraObject = new GameObject("Exploration Camera", typeof(Camera));
                worldCamera = cameraObject.GetComponent<Camera>(); cameraObject.tag = "MainCamera";
            }
            worldCamera.orthographic = true; worldCamera.orthographicSize = 8.5f;
            worldCamera.rect = new Rect(0, 0, .75f, 1);
            worldCamera.backgroundColor = new Color(.08f, .12f, .16f); worldCamera.clearFlags = CameraClearFlags.SolidColor;
            if (read != null) Sync(read().Run, true);
        }
        public void Sync(RunState run, bool force = false)
        {
            if (run == null || read == null) return;
            string id = GridRules.MapIdFor(run);
            WorldMap next;
            if (!maps.TryGetValue(id, out next)) throw new InvalidOperationException("缺少測試地圖：" + id);
            bool changedMap = map == null || map.Id != next.Id || lastCampaign != run.CampaignId;
            bool changedAnchor = anchor != (run.CurrentNodeId ?? "");
            if (changedMap) { map = next; BuildMap(); }
            if (changedMap || force) battleSafeNode = "";
            if (changedMap || force || changedAnchor)
            {
                moving = false;
                WorldPresentationState p;
                var nodeMarker = map.Interactions.FirstOrDefault(i => !string.IsNullOrEmpty(i.NodeId) && i.NodeId == run.CurrentNodeId);
                // A completed encounter returns to its actual approach square, not an arbitrary neighbour.
                if (!changedMap && !force && changedAnchor && GridRules.Adjacent(position, nodeMarker) && GridRules.CanEnter(map, position.x, position.y, run))
                    p = GridRules.MakePosition(map, run, position, facing);
                else p = GridRules.ResolvePosition(map, run, data);
                position = new Vector2Int(p.X, p.Y); destination = position; facing = p.Facing;
                anchor = run.CurrentNodeId ?? ""; lastCampaign = run.CampaignId;
                UpdateHero(); EmitPosition(run);
                interactionArmed = false; blockedHint = "";
            }
            if (run.Battle != null && run.Battle.NodeId == run.CurrentNodeId &&
                (run.Location == GameLocation.Battle || run.Battle.IsTerminal && run.Battle.Outcome != BattleOutcome.Defeat) && battleSafeNode != run.CurrentNodeId)
            {
                var fightMarker = map.Interactions.FirstOrDefault(i => i.NodeId == run.CurrentNodeId);
                if (GridRules.Adjacent(position, fightMarker)) { battleSafeNode = run.CurrentNodeId; battleSafePosition = position; }
            }
            RefreshMarkers(run); UpdateCamera();
        }
        public void SetVisible(bool value)
        {
            visible = value;
            if (visualRoot != null) visualRoot.SetActive(value);
            if (worldCamera != null) worldCamera.enabled = value;
            if (!value) { interactionArmed = false; moving = false; UpdateHero(); }
        }
        public void SetGridPosition(int x, int y, int direction)
        {
            if (direction < 0 || direction > 3) throw new ArgumentOutOfRangeException("direction");
            var run = read().Run;
            string reason = GridRules.BlockedReason(map, x, y, run, data);
            if (reason != "") throw new InvalidOperationException(reason);
            if (!GridRules.IsReachable(map, run, new Vector2Int(x, y), data)) throw new InvalidOperationException("該位置超出目前可到達的範圍。");
            position = new Vector2Int(x, y); destination = position; facing = direction; moving = false;
            blockedHint = ""; UpdateHero(); EmitPosition(run); UpdateCamera();
        }
        public bool CanSave(GameSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Run == null || map == null || moving) return false;
            var r = snapshot.Run;
            if (r.Location == GameLocation.Town) return true;
            if (r.Location != GameLocation.Dungeon || !string.IsNullOrEmpty(r.PendingSupplyNodeId) || map.Id != GridRules.MapIdFor(r)) return false;
            var n = data.Node(r.CurrentNodeId);
            bool save = n != null && DungeonRules.IsSave(n);
            bool battle = r.Battle != null && r.Battle.IsTerminal && r.Battle.Outcome != BattleOutcome.Defeat && r.Battle.NodeId == r.CurrentNodeId;
            if (!save && !battle) return false;
            var marker = map.Interactions.FirstOrDefault(i => i.NodeId == r.CurrentNodeId);
            return save ? GridRules.Adjacent(position, marker) : battleSafeNode == r.CurrentNodeId && position == battleSafePosition;
        }
        private void Update()
        {
            if (map == null || read == null || !visible) return;
            var keyboard = Keyboard.current;
            bool pressed = keyboard != null && (keyboard.eKey.isPressed || keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed);
            if (!pressed) interactionArmed = true;
            if (locked) return;
            if (moving)
            {
                moveProgress += Time.deltaTime * 6.4f;
                UpdateHeroFrame(moveProgress < .5f ? 0 : 2);
                Vector3 feet = Vector3.Lerp(moveFrom, World(destination), Mathf.Clamp01(moveProgress));
                hero.transform.position = feet + Vector3.up * Mathf.Sin(Mathf.Clamp01(moveProgress) * Mathf.PI) * .055f;
                hero.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(moveProgress * Mathf.PI * 2) * 4f);
                shadow.transform.position = feet + new Vector3(0, -.52f, .01f);
                if (moveProgress >= 1f) { position = destination; moving = false; UpdateHero(); EmitPosition(read().Run); }
                UpdateCamera(); return;
            }
            if (keyboard == null) return;
            if (pressed && interactionArmed)
            {
                interactionArmed = false;
                var near = Nearby;
                if (near != null) { interact?.Invoke(near); return; }
            }
            Vector2 input = new Vector2((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0),
                (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0) - (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0));
            Vector2Int axis = GridRules.SelectAxis(input);
            if (axis == Vector2Int.zero) return;
            int nextFacing = GridRules.Facing(axis);
            if (nextFacing != facing) { facing = nextFacing; UpdateHero(); EmitPosition(read().Run); }
            destination = position + axis;
            var run = read().Run;
            blockedHint = MovementReason(destination, run);
            if (blockedHint != "") return;
            moveFrom = World(position); moveProgress = 0; moving = true;
        }
        private string MovementReason(Vector2Int target, RunState run)
        {
            return GridRules.BlockedReason(map, target.x, target.y, run, data);
        }
        private void EmitPosition(RunState run)
        { if (map != null) moved?.Invoke(GridRules.MakePosition(map, run, position, facing)); }
        private static Vector3 World(Vector2Int p) { return new Vector3(p.x, -p.y, 0); }
        private void UpdateHero()
        {
            if (hero == null) return;
            UpdateHeroFrame(1); hero.transform.position = World(position);
            hero.transform.rotation = Quaternion.identity;
            hero.sortingOrder = position.y * 10 + 5;
            shadow.sortingOrder = hero.sortingOrder - 1;
            shadow.transform.position = World(position) + new Vector3(0, -.52f, .01f);
        }
        private void UpdateHeroFrame(int frame)
        {
            if (hero == null) return;
            string key = facing == 0 ? "hero_up" : facing == 1 ? "hero_right" : facing == 3 ? "hero_left" : "hero_down";
            hero.sprite = GetSprite(key + "_" + frame); Size(hero, 1.2f);
            // The sheet contains separate left/right artwork; never mirror these frames again.
            hero.flipX = false;
        }
        private Sprite GetSprite(string key)
        {
            Sprite s = sprite == null ? null : sprite(key); if (s != null) return s;
            if (fallback == null)
            {
                var t = new Texture2D(1, 1); t.SetPixel(0, 0, Color.white); t.Apply(); t.filterMode = FilterMode.Point;
                fallback = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            }
            return fallback;
        }
        private void BuildMap()
        {
            if (visualRoot != null) { visualRoot.SetActive(false); Destroy(visualRoot); }
            visualRoot = new GameObject("World " + map.Id); visualRoot.transform.SetParent(transform, false); markers.Clear();
            for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
            {
                char ground = map.Ground[y][x];
                string key = ground == '#' ? "wall" : ground == '~' ? "water" : ground == '=' ? "path" : ground == 'f' ? "floor" : ground == 'c' ? "carpet" : "grass";
                var renderer = Create(key, new Vector2Int(x, y), -1000, 1.02f);
                if (renderer.sprite == fallback) renderer.color = key == "grass" ? new Color(.3f, .48f, .3f) : key == "water" ? new Color(.17f, .38f, .62f) : key == "wall" ? new Color(.25f, .26f, .32f) : new Color(.6f, .57f, .47f);
            }
            foreach (var decoration in map.Decorations) Create(decoration.SpriteKey, new Vector2Int(decoration.X, decoration.Y), decoration.Y * 10, 1.15f);
            foreach (var interaction in map.Interactions)
            {
                var marker = Create(interaction.SpriteKey, new Vector2Int(interaction.X, interaction.Y), interaction.Y * 10 + 2, 1f);
                if (marker.sprite == fallback) marker.color = new Color(.9f, .68f, .25f);
                markers[interaction.Id] = marker;
                if (map.Id == "town" || map.Id == "inn") CreateLabel(interaction);
            }
            shadow = Create("shadow", position, 0, .68f); shadow.color = new Color(0, 0, 0, .28f); shadow.transform.localScale = new Vector3(.6f, .2f, 1);
            hero = Create("hero_down", position, 1, 1.2f);
            if (hero.sprite == fallback) hero.color = new Color(1f, .86f, .5f);
            visualRoot.SetActive(visible);
        }
        private SpriteRenderer Create(string key, Vector2Int grid, int order, float size)
        {
            var go = new GameObject(key); go.transform.SetParent(visualRoot.transform, false); go.transform.position = World(grid);
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = GetSprite(key); r.sortingOrder = order; Size(r, key == "house" ? 3f : size); return r;
        }
        private void CreateLabel(WorldInteraction interaction)
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts/DemoFont");
            if (font == null) return;
            var go = new GameObject("Label " + interaction.Id);
            go.transform.SetParent(visualRoot.transform, false);
            go.transform.position = World(new Vector2Int(interaction.X, interaction.Y)) + new Vector3(0, 1.12f, -.1f);
            var text = go.AddComponent<TextMeshPro>(); text.font = font; text.fontSize = 3.1f;
            text.text = interaction.Label; text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.color = new Color(1, .98f, .87f);
            text.outlineColor = new Color(.08f, .1f, .12f, .95f); text.outlineWidth = .2f;
            text.rectTransform.sizeDelta = new Vector2(7, .65f);
            var renderer = text.GetComponent<MeshRenderer>(); if (renderer != null) renderer.sortingOrder = 10000;
        }
        private static void Size(SpriteRenderer renderer, float target)
        {
            if (renderer.sprite == null) return;
            var size = renderer.sprite.bounds.size; float factor = target / Mathf.Max(size.x, size.y, .001f);
            renderer.transform.localScale = new Vector3(factor, factor, 1);
        }
        private void RefreshMarkers(RunState run)
        {
            var progress = run.Chapters.FirstOrDefault(p => p.Chapter == run.Chapter);
            foreach (var interaction in map.Interactions)
            {
                SpriteRenderer renderer;
                if (!markers.TryGetValue(interaction.Id, out renderer)) continue;
                bool clear = progress != null && progress.ClearedNodeIds.Contains(interaction.NodeId);
                renderer.color = clear ? new Color(.55f, .7f, .65f, .8f) : Color.white;
            }
        }
        private void UpdateCamera()
        {
            if (worldCamera == null || map == null || hero == null) return;
            Rect viewport = worldCamera.pixelRect;
            float aspect = viewport.height > 0 ? viewport.width / viewport.height : 4f / 3f;
            worldCamera.aspect = aspect;
            float halfH = worldCamera.orthographicSize, halfW = halfH * aspect;
            float x = map.Width < halfW * 2 ? (map.Width - 1) * .5f : Mathf.Clamp(hero.transform.position.x, -.5f + halfW, map.Width - .5f - halfW);
            float top = .5f, bottom = -map.Height + .5f;
            float y = map.Height < halfH * 2 ? -(map.Height - 1) * .5f : Mathf.Clamp(hero.transform.position.y, bottom + halfH, top - halfH);
            worldCamera.transform.position = new Vector3(x, y, -10);
        }
        private void OnDestroy()
        {
            if (fallback != null) { var texture = fallback.texture; Destroy(fallback); Destroy(texture); }
        }
    }
}
