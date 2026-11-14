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
        private UnityEngine.UI.Image[] roomIcons;
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
                font = Resources.Load<TMP_FontAsset>("PrototypeFont");
                if (!font) throw new System.InvalidOperationException("Run Echoes > Create or Update Prototype Scene to create the CJK font asset.");
            }
            canvas = new GameObject("GameHUD", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<RectTransform>();
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
            healthText = Label(vitals, "", new Vector2(70, -5), new Vector2(180, 28), 21, Paper);
            healthBar = Bar(vitals, new Vector2(70, -34), new Vector2(174, 8), new Color32(198, 81, 64, 255));
            damageTrail = Instantiate(healthBar, healthBar.transform.parent);
            damageTrail.name = "DamageTrail";
            damageTrail.color = Paper;
            damageTrail.transform.SetAsFirstSibling();
            lastHealth = run.Player.Health;
            xpText = Label(vitals, "", new Vector2(70, -44), new Vector2(180, 24), 15, Paper);
            xpBar = Bar(vitals, new Vector2(70, -68), new Vector2(174, 3), Brass);
            pressureText = Label(canvas, "", new Vector2(28, -108), new Vector2(300, 32), 19, PixelArt.Gold);

            mapPanel = Panel("MiniMap", canvas, new Vector2(-24, -24), new Vector2(180, 179), iron, Vector2.one);
            mapTitle = Label(mapPanel, "", new Vector2(12, -6), new Vector2(158, 25), 16, Paper);
            RebuildMinimap();
            roomText = Label(mapPanel, "", new Vector2(12, -140), new Vector2(158, 20), 15, Paper);
            clockText = Label(mapPanel, "", new Vector2(12, -159), new Vector2(158, 18), 14, muted);

            var belt = Panel("Decoy", canvas, new Vector2(24, 24), new Vector2(156, 52), iron, Vector2.zero);
            bellIcon = ArtPanel("Bell", belt, new Vector2(2, -2), new Vector2(48, 48), GameArt.Get("bell"), icon: true);
            Label(belt, "Q   诱饵", new Vector2(56, -4), new Vector2(96, 24), 18, Paper);
            statusText = Label(belt, "", new Vector2(56, -28), new Vector2(96, 21), 15, Brass);
            var purse = Panel("Purse", canvas, new Vector2(-24, 24), new Vector2(116, 40), iron, Vector2.right);
            ArtPanel("HudGoldIcon", purse, new Vector2(6, -4), new Vector2(32, 32),
                PresentationArt.Current.shopGoldCoin, icon: true);
            goldText = Label(purse, "", new Vector2(42, -6), new Vector2(70, 29), 20, Brass);
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
            for (int i = 0; i < roomIcons.Length; i++)
            {
                if (!roomIcons[i]) continue;
                int col = i % DungeonMap.GridSize;
                int row = i / DungeonMap.GridSize;
                var room = run.Map.RoomAtCell(col, row);
                roomIcons[i].color = room == null ? new Color(.08f, .11f, .16f) :
                    room.Index == run.Map.CurrentRoom ? new Color(.16f, .52f, .53f) :
                    room.Visited ? new Color(.27f, .34f, .43f) : new Color(.12f, .17f, .25f);
            }
            if (run.BossActivated && run.Boss.Alive)
            {
                bossBar.transform.parent.gameObject.SetActive(true);
                bossBar.fillAmount = run.Boss.Health / run.Boss.MaxHealth;
                bossText.text = $"守关者   {Mathf.CeilToInt(run.Boss.Health)}";
            }
            else if (run.State == RunState.Playing && Vector2.Distance(run.Player.transform.position, run.Map.AltarPosition) < 3)
                Notify("[E] 唤醒守关者", .2f);
            if (run.State == RunState.Playing && run.Shop.InRange) Notify("[E] 打开补给商店", .2f);
            if (run.State == RunState.Upgrade) ReadUpgradeKeys();
        }

        public void RebuildMinimap()
        {
            if (!mapPanel) return;
            if (roomIcons != null)
                for (int i = 0; i < roomIcons.Length; i++)
                    if (roomIcons[i]) Destroy(roomIcons[i].gameObject);
            roomIcons = new UnityEngine.UI.Image[DungeonMap.GridSize * DungeonMap.GridSize];
            if (bossBar) bossBar.transform.parent.gameObject.SetActive(false);
            if (bossText) bossText.text = "";
            for (int row = 0; row < DungeonMap.GridSize; row++)
                for (int col = 0; col < DungeonMap.GridSize; col++)
                {
                    var room = run.Map.RoomAtCell(col, row);
                    if (room == null) continue;
                    int index = row * DungeonMap.GridSize + col;
                    var icon = Panel("RoomCell" + index, mapPanel,
                        new Vector2(12 + col * 32, -37 - row * 20),
                        new Vector2(28, 16), new Color(.12f, .17f, .25f));
                    roomIcons[index] = icon.GetComponent<UnityEngine.UI.Image>();
                    if (room.Role == RoomRole.Start || room.Role == RoomRole.Boss)
                    {
                        var label = Label(icon,
                            room.Role == RoomRole.Start ? "起" : "终",
                            Vector2.zero, new Vector2(28, 16), 12,
                            room.Role == RoomRole.Start ? PixelArt.Gold : PixelArt.Red,
                            new Vector2(.5f, .5f));
                        label.alignment = TextAlignmentOptions.Center;
                    }
                }
            mapTitle.text = $"第{run.Floor}层  {run.FloorTitle.Replace(" ", "")}";
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
            label.fontStyle = fontSize >= 24 ? FontStyles.Bold : FontStyles.Normal;
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
                var label = Label(face, text, Vector2.zero, size, 20, Paper, new Vector2(.5f, .5f));
                label.alignment = TextAlignmentOptions.Center;
            }
            return rect;
        }
    }
}
