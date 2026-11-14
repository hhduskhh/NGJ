using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Echoes
{
    public sealed partial class GameHud : MonoBehaviour
    {
        private RunController run;
        private RectTransform canvas, modal;
        private TMP_Text healthText, xpText, roomText, pressureText, statusText, clockText, goldText, noticeText, bossText;
        private UnityEngine.UI.Image healthBar, xpBar, bossBar;
        private RectTransform mapPanel;
        private TMP_Text mapTitle;
        private Texture2D mapTexture;
        private UnityEngine.UI.RawImage mapImage;
        private RectTransform playerMarker;
        private int drawnRoom = -1;
        private RectTransform modalBackdrop;
        private float noticeUntil;
        private bool settingsOpen;
        private static TMP_FontAsset font;
        private static readonly Color Iron = new Color32(24, 29, 30, 255);
        private static readonly Color Paper = new Color32(238, 226, 200, 255);
        private static readonly Color Brass = new Color32(210, 169, 100, 255);
        private readonly Color muted = new Color32(183, 183, 168, 255);

        public void Initialize(RunController controller)
        {
            run = controller;
            if (!font)
            {
                font = Resources.Load<TMP_FontAsset>("FusionPixelUI");
                if (!font) throw new System.InvalidOperationException("Run Echoes > Bake Interface Font to create the pixel font asset.");
            }
            canvas = new GameObject("GameHUD", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<RectTransform>();
            canvas.GetComponent<Canvas>().pixelPerfect = true;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if (!FindObjectOfType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var iron = new Color32(19, 25, 24, 226);
            var vitals = Panel("Vitals", canvas, new Vector2(24, -24), new Vector2(256, 76), iron);
            ArtPanel("Explorer", vitals, new Vector2(2, -6), new Vector2(64, 64),
                GameArt.Get("player"), icon: true);
            healthText = Label(vitals, "", new Vector2(70, -5), new Vector2(180, 30), 24, Paper);
            healthBar = Bar(vitals, new Vector2(70, -34), new Vector2(174, 8), new Color32(198, 81, 64, 255));
            damageTrail = Instantiate(healthBar, healthBar.transform.parent);
            damageTrail.name = "DamageTrail";
            damageTrail.color = Paper;
            damageTrail.transform.SetAsFirstSibling();
            lastHealth = run.Player.Health;
            xpText = Label(vitals, "", new Vector2(70, -44), new Vector2(180, 24), 15, Paper);
            xpBar = Bar(vitals, new Vector2(70, -68), new Vector2(174, 3), Brass);
            pressureText = Label(canvas, "", new Vector2(28, -108), new Vector2(300, 32), 19, PixelArt.Gold);

            float size = run.Config.minimapSize;
            mapPanel = Panel("MiniMap", canvas, new Vector2(-24, -24), new Vector2(size + 24, size + 72), iron, Vector2.one);
            mapTitle = Label(mapPanel, "", new Vector2(12, -4), new Vector2(size, 24), 16, Paper);
            RebuildMinimap();
            roomText = Label(mapPanel, "", new Vector2(12, -size - 31), new Vector2(size, 20), 15, Paper);
            clockText = Label(mapPanel, "", new Vector2(12, -size - 51), new Vector2(size, 18), 14, muted);

            var belt = Panel("Decoy", canvas, new Vector2(24, 24), new Vector2(156, 52), iron, Vector2.zero);
            bellIcon = ArtPanel("Bell", belt, new Vector2(2, -2), new Vector2(48, 48), GameArt.Get("bell"), icon: true);
            Label(belt, "Q   诱饵", new Vector2(56, -4), new Vector2(96, 24), 18, Paper);
            statusText = Label(belt, "", new Vector2(56, -28), new Vector2(96, 21), 15, Brass);
            var purse = Panel("Purse", canvas, new Vector2(-24, 24), new Vector2(116, 40), iron, Vector2.right);
            ArtPanel("HudGoldIcon", purse, new Vector2(6, -4), new Vector2(32, 32),
                PresentationArt.Current.shopGoldCoin, icon: true);
            goldText = Label(purse, "", new Vector2(42, -6), new Vector2(70, 29), 20, Brass);
            var pack = Button(canvas, "背包 [Tab]", Vector2.zero, new Vector2(166, 44), run.OpenBackpack);
            pack.name = "HudBackpack";
            pack.anchorMin = pack.anchorMax = pack.pivot = Vector2.right;
            pack.anchoredPosition = new Vector2(-150, 22);
            SkinButton(pack);
            noticeText = Label(canvas, "", new Vector2(0, 33), new Vector2(690, 42), 21, Paper, new Vector2(.5f, 0));
            noticeText.alignment = TextAlignmentOptions.Center;
            bossText = Label(canvas, "", new Vector2(0, -22), new Vector2(520, 28), 18, PixelArt.Red, new Vector2(.5f, 1));
            bossText.alignment = TextAlignmentOptions.Center;
            bossBar = Bar(canvas, new Vector2(0, -52), new Vector2(450, 8), PixelArt.Red, new Vector2(.5f, 1));
            bossBar.transform.parent.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!run.Player) return;
            healthText.text = $"{Mathf.CeilToInt(run.Player.Health)} / {run.Player.MaxHealth:0}";
            healthBar.fillAmount = run.Player.Health / run.Player.MaxHealth;
            xpText.text = run.State == RunState.Upgrade ? $"Lv.{run.Level}  待升级" : $"Lv.{run.Level}  {run.Experience}/{run.RequiredExperience}";
            xpBar.fillAmount = Mathf.Clamp01((float)run.Experience / run.RequiredExperience);
            pressureText.text = run.NearbyEnemies > 0 ? $"围攻 {run.NearbyEnemies}   伤害 +{(run.DamageMultiplier - 1) * 100:0}%" : "";
            roomText.text = run.Map.Rooms[run.Map.CurrentRoom].Name;
            clockText.text = $"{(int)run.Elapsed / 60:00}:{(int)run.Elapsed % 60:00}   ·   击杀 {run.Kills}{(run.Muted ? "  静音" : "")}";
            goldText.text = run.Shop.GoldText;
            float cooldown = run.Player.DecoyCooldownRemaining;
            statusText.text = cooldown <= 0 ? "就绪" : $"{cooldown:0.0} 秒";
            UpdateHudFeedback(cooldown);
            if (Time.unscaledTime >= noticeUntil) noticeText.text = "";
            if (drawnRoom != run.Map.CurrentRoom) DrawMinimap();
            Vector2 position = run.Player.transform.position;
            Vector2 size = mapImage.rectTransform.rect.size;
            playerMarker.anchoredPosition = new Vector2((position.x + .5f) * size.x / DungeonMap.Width,
                (position.y + .5f) * size.y / DungeonMap.Height);
            if (run.BossActivated && run.Boss.Alive)
            {
                bossBar.transform.parent.gameObject.SetActive(true);
                bossBar.fillAmount = run.Boss.Health / run.Boss.MaxHealth;
                bossText.text = $"守关者   {Mathf.CeilToInt(run.Boss.Health)}";
            }
            else if (run.State == RunState.Playing && Vector2.Distance(run.Player.transform.position, run.Map.AltarPosition) < 3)
                Notify("[E] 唤醒守关者", .2f);
            if (run.State == RunState.Playing && Vector2.Distance(run.Player.transform.position, run.Map.SignPosition) < 3)
                Notify("WASD 移动 · 左键射击 · 沿箭头探索，到祭坛唤醒守关者", .2f);
            if (run.State == RunState.Playing && run.Shop.InRange) Notify("[E] 打开补给商店", .2f);
            if (run.State == RunState.Upgrade) ReadUpgradeKeys();
        }

        public void RebuildMinimap()
        {
            if (!mapPanel) return;
            if (!mapImage)
            {
                mapImage = new GameObject("RoomMap", typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
                var rect = mapImage.rectTransform;
                rect.SetParent(mapPanel, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
                rect.sizeDelta = Vector2.one * run.Config.minimapSize;
                rect.anchoredPosition = new Vector2(12, -28);
                mapImage.raycastTarget = false;
                mapTexture = new Texture2D(DungeonMap.Width, DungeonMap.Height, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                mapImage.texture = mapTexture;
                // A square rotated 45 degrees has a diagonal sqrt(2) times its side.
                float side = run.Config.minimapMarkerSize / Mathf.Sqrt(2);
                playerMarker = Panel("PlayerMarker", rect, Vector2.zero, Vector2.one * side, Iron, Vector2.zero);
                playerMarker.pivot = Vector2.one * .5f;
                playerMarker.localRotation = Quaternion.Euler(0, 0, 45);
                Panel("Center", playerMarker, Vector2.zero, Vector2.one * (side - 2), Color.white, Vector2.one * .5f);
            }
            DrawMinimap();
            if (bossBar) bossBar.transform.parent.gameObject.SetActive(false);
            if (bossText) bossText.text = "";
            mapTitle.text = $"第{run.Floor}层  {run.FloorTitle.Replace(" ", "")}";
        }

        private void DrawMinimap()
        {
            var map = run.Map;
            drawnRoom = map.CurrentRoom;
            var pixels = new Color32[DungeonMap.Width * DungeonMap.Height];
            foreach (var room in map.Rooms)
            {
                if (!room.Visited) continue;
                Color32 color = room.Role == RoomRole.Boss ? PixelArt.Red : room.Role == RoomRole.Start ? PixelArt.Gold :
                    new Color(.35f, .43f, .50f);
                foreach (var cell in room.Cells)
                {
                    int left = cell.x * DungeonMap.CellWidth, bottom = DungeonMap.CellOriginY(cell.y);
                    for (int y = bottom; y < bottom + DungeonMap.CellHeight; y++)
                        for (int x = left; x < left + DungeonMap.CellWidth; x++)
                        {
                            var tile = new Vector2Int(x, y);
                            if (!map.IsFloorTile(tile)) continue;
                            pixels[y * DungeonMap.Width + x] = room.Index == drawnRoom && IsMinimapEdge(map, tile)
                                ? (Color32)run.Config.minimapRoomOutline : color;
                        }
                }
            }
            foreach (var door in map.Doors)
            {
                if (!door.A.Visited && !door.B.Visited) continue;
                var opening = door.Opening;
                bool frontier = door.A.Visited != door.B.Visited;
                Color32 ink = frontier ? run.Config.minimapFrontierDoor : run.Config.minimapExploredDoor;
                foreach (var tile in opening.allPositionsWithin)
                    pixels[tile.y * DungeonMap.Width + tile.x] = ink;
                if (!frontier) continue;

                // Probe across this doorway; an L-shaped room's centre can lie off-axis.
                var normal = opening.width == 2 ? Vector2Int.right : Vector2Int.up;
                var center = Vector2Int.FloorToInt(door.Center);
                if (map.RoomAtPosition(center + normal * 2).Visited) normal = -normal;
                var tangent = new Vector2Int(-normal.y, normal.x);
                for (int row = 0; row < 4; row++)
                {
                    int halfWidth = 3 - row;
                    for (int offset = -halfWidth; offset <= halfWidth; offset++)
                    {
                        var tile = center + normal * (3 + row) + tangent * offset;
                        pixels[tile.y * DungeonMap.Width + tile.x] = ink;
                    }
                }
            }
            mapTexture.SetPixels32(pixels);
            mapTexture.Apply(false);
        }

        private bool IsMinimapEdge(DungeonMap map, Vector2Int tile)
        {
            for (int distance = 1; distance <= run.Config.minimapOutlineTiles; distance++)
            {
                if (!map.IsFloorTile(tile + Vector2Int.left * distance) || !map.IsFloorTile(tile + Vector2Int.right * distance) ||
                    !map.IsFloorTile(tile + Vector2Int.up * distance) || !map.IsFloorTile(tile + Vector2Int.down * distance))
                    return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (mapTexture) Destroy(mapTexture);
        }

        public void Notify(string text, float seconds)
        { noticeText.text = text; noticeUntil = Time.unscaledTime + seconds; }

        private RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color, Vector2? anchor = null)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0, 1);
            rect.pivot = new Vector2(rect.anchorMin.x, rect.anchorMin.y);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            var image = rect.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
            return rect;
        }

        private TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color, Vector2? anchor = null)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = anchor ?? new Vector2(0, 1);
            label.rectTransform.pivot = new Vector2(label.rectTransform.anchorMin.x, label.rectTransform.anchorMin.y);
            label.rectTransform.sizeDelta = size; label.rectTransform.anchoredPosition = position;
            label.font = font; label.text = text; label.fontSize = fontSize; label.color = color;
            label.fontStyle = FontStyles.Normal;
            label.enableWordWrapping = true; label.raycastTarget = false;
            return label;
        }

        private UnityEngine.UI.Image Bar(Transform parent, Vector2 position, Vector2 size, Color color, Vector2? anchor = null)
        {
            var background = Panel("Bar", parent, position, size, new Color(.15f, .19f, .25f), anchor);
            var fill = Panel("Fill", background, Vector2.zero, size, color).GetComponent<UnityEngine.UI.Image>();
            fill.sprite = PixelArt.Get("solid"); fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillOrigin = 0;
            return fill;
        }

        private RectTransform Button(Transform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var rect = Panel("Button", parent, position, size, Color.clear, new Vector2(.5f, 1));
            rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var face = Panel("Face", rect, Vector2.zero, size, new Color32(58, 61, 56, 255), new Vector2(.5f, .5f));
            var button = rect.gameObject.AddComponent<HudButton>();
            button.Initialize(face, run.Noise, run.Config.buttonPressDepth);
            var colors = button.colors; colors.highlightedColor = new Color(1.45f, 1.34f, 1.12f);
            colors.pressedColor = Brass; button.colors = colors;
            button.onClick.AddListener(action);
            if (text.Length > 0)
            {
                var label = Label(face, text, Vector2.zero, size, 24, Paper, new Vector2(.5f, .5f));
                label.alignment = TextAlignmentOptions.Center;
            }
            return rect;
        }
    }
}
