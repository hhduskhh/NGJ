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
        private TMP_Text[] roomCellLabels;
        private float noticeUntil;
        private bool settingsOpen;
        private static TMP_FontAsset font;
        private readonly Color panel = new Color32(15, 23, 35, 246);
        private readonly Color muted = new Color32(147, 163, 183, 255);

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
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            if (!FindObjectOfType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var iron = new Color32(19, 25, 24, 226);
            var parchment = new Color32(235, 220, 184, 255);
            var brass = new Color32(194, 155, 86, 255);
            var vitals = Panel("Vitals", canvas, new Vector2(24, -24), new Vector2(256, 76), iron);
            ArtPanel("Explorer", vitals, new Vector2(2, -6), new Vector2(64, 64),
                GameArt.Get("player"), icon: true);
            healthText = Label(vitals, "", new Vector2(70, -5), new Vector2(180, 28), 21, parchment);
            healthBar = Bar(vitals, new Vector2(70, -34), new Vector2(174, 8), new Color32(198, 81, 64, 255));
            xpText = Label(vitals, "", new Vector2(70, -44), new Vector2(180, 24), 15, parchment);
            xpBar = Bar(vitals, new Vector2(70, -68), new Vector2(174, 3), brass);
            pressureText = Label(canvas, "", new Vector2(28, -108), new Vector2(300, 32), 19, PixelArt.Gold);

            mapPanel = Panel("MiniMap", canvas, new Vector2(-24, -24), new Vector2(180, 179), iron, Vector2.one);
            mapTitle = Label(mapPanel, "", new Vector2(12, -6), new Vector2(158, 25), 16, parchment);
            RebuildMinimap();
            roomText = Label(mapPanel, "", new Vector2(12, -140), new Vector2(158, 20), 15, parchment);
            clockText = Label(mapPanel, "", new Vector2(12, -159), new Vector2(158, 18), 14, muted);

            var belt = Panel("Decoy", canvas, new Vector2(24, 24), new Vector2(156, 52), iron, Vector2.zero);
            ArtPanel("Bell", belt, new Vector2(2, -2), new Vector2(48, 48), GameArt.Get("bell"), icon: true);
            Label(belt, "Q   诱饵", new Vector2(56, -4), new Vector2(96, 24), 18, parchment);
            statusText = Label(belt, "", new Vector2(56, -28), new Vector2(96, 21), 15, brass);
            var purse = Panel("Purse", canvas, new Vector2(-24, 24), new Vector2(116, 40), iron, Vector2.right);
            ArtPanel("HudGoldIcon", purse, new Vector2(6, -4), new Vector2(32, 32),
                PresentationArt.Current.shopGoldCoin, icon: true);
            goldText = Label(purse, "", new Vector2(42, -6), new Vector2(70, 29), 20, brass);
            noticeText = Label(canvas, "", new Vector2(0, -52), new Vector2(690, 45), 23, PixelArt.Gold, new Vector2(.5f, 1));
            noticeText.alignment = TextAlignmentOptions.Center;
            bossText = Label(canvas, "", new Vector2(0, -112), new Vector2(520, 28), 18, PixelArt.Red, new Vector2(.5f, 1));
            bossText.alignment = TextAlignmentOptions.Center;
            bossBar = Bar(canvas, new Vector2(0, -145), new Vector2(450, 8), PixelArt.Red, new Vector2(.5f, 1));
            bossBar.transform.parent.gameObject.SetActive(false);
            Notify("WASD 移动   左键射击   Esc 操作说明", 5);
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
            if (run.BossActivated)
            {
                bossBar.transform.parent.gameObject.SetActive(true);
                bossBar.fillAmount = run.Boss.Health / run.Boss.MaxHealth;
                bossText.text = $"守关者   {Mathf.CeilToInt(run.Boss.Health)}";
            }
            else if (Vector2.Distance(run.Player.transform.position, run.Map.AltarPosition) < 3)
                Notify("[E] 唤醒守关者", .2f);
            if (run.State == RunState.Playing && run.Shop.InRange) Notify("[E] 打开补给商店", .2f);
            if (run.State == RunState.Upgrade)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) run.ChooseUpgrade(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) run.ChooseUpgrade(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) run.ChooseUpgrade(2);
            }
        }

        public void RebuildMinimap()
        {
            if (!mapPanel) return;
            if (roomIcons != null)
                for (int i = 0; i < roomIcons.Length; i++)
                    if (roomIcons[i]) Destroy(roomIcons[i].gameObject);
            roomIcons = new UnityEngine.UI.Image[DungeonMap.GridSize * DungeonMap.GridSize];
            roomCellLabels = new TMP_Text[roomIcons.Length];
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
                        roomCellLabels[index] = label;
                    }
                }
            mapTitle.text = $"第{run.Floor}层  {run.FloorTitle.Replace(" ", "")}";
        }

        public void Notify(string text, float seconds)
        { noticeText.text = text; noticeUntil = Time.unscaledTime + seconds; }

        public void RefreshModal()
        {
            if (run.State != RunState.Paused) settingsOpen = false;
            if (modal) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); }
            if (run.State == RunState.Playing) return;
            modal = Panel("Modal", canvas, Vector2.zero, Vector2.zero, new Color(.025f, .04f, .065f, .96f));
            modal.anchorMin = Vector2.zero; modal.anchorMax = Vector2.one;
            modal.offsetMin = modal.offsetMax = Vector2.zero;
            modal.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            if (run.State == RunState.Shop) { DrawShop(); return; }
            if (settingsOpen) { DrawSettings(); return; }
            string title = run.State == RunState.Upgrade ? "共 振 升 级" :
                run.State == RunState.FloorCleared ? "本 层 已 净 化" :
                run.State == RunState.Won ? "遗 迹 重 归 寂 静" :
                run.State == RunState.Lost ? "探 险 暂 止" : "暂 停 探 索";
            var heading = Label(modal, title, new Vector2(0, -185), new Vector2(1000, 70), 44, PixelArt.Cyan, new Vector2(.5f, 1));
            heading.alignment = TextAlignmentOptions.Center;
            var sub = Label(modal, run.State == RunState.Upgrade ? $"已暂停 · 升至等级 {run.Level + 1} · 点击卡片或按 1 / 2 / 3" :
                run.State == RunState.FloorCleared ? $"第 {run.Floor} 层完成 · 生命恢复 30% · 武器、被动与金币保留" :
                $"探索 {(int)run.Elapsed / 60:00}:{(int)run.Elapsed % 60:00}   ·   击杀 {run.Kills}   ·   等级 {run.Level}",
                new Vector2(0, -274), new Vector2(1000, 35), 22, muted, new Vector2(.5f, 1));
            sub.alignment = TextAlignmentOptions.Center;
            if (run.State == RunState.Upgrade)
            {
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    var choice = run.Choices[i];
                    var card = Button(modal, "", new Vector2((i - 1) * 374, -365), new Vector2(350, 290),
                        () => run.ChooseUpgrade(index));
                    Label(card, $"0{i + 1}  /  {(choice.IncreasesNoise ? "风险升级" : "装备改良")}", new Vector2(23, -22),
                        new Vector2(310, 30), 17, choice.IncreasesNoise ? PixelArt.Red : PixelArt.Cyan);
                    ArtPanel("UpgradeIcon", card, new Vector2(24, -76), new Vector2(64, 64),
                        PresentationArt.Current.Upgrade(choice.Title), new Vector2(0, 1), true);
                    Label(card, choice.Title, new Vector2(100, -85), new Vector2(230, 52), 26, Color.white);
                    Label(card, choice.Description, new Vector2(23, -155), new Vector2(310, 110), 21, muted);
                }
            }
            else
            {
                string explanation = run.State == RunState.Paused ?
                    $"WASD 移动　左键射击（自动瞄准）　Q 向鼠标方向投诱饵\nE 商店 / 祭坛 / 下潜　Esc 继续　M 开关声音\n金环：枪声 {run.Player.NoiseRadius:0.#}　青色声波：诱饵　青色细环：围攻范围\n围攻上限 +{run.Player.PressureCap * run.Player.PressureBonus * 100:0}%　武器 {run.Player.Weapons.Slots.Count}/6\n灰线：沉睡　金点：警戒　红线：追击　前往终点祭坛挑战守关者" :
                    run.State == RunState.FloorCleared ? "按 E 或点击按钮下潜。下一层会随机生成新的房间布局。\n敌人、Boss 与商店库存重置；生命恢复 30%，成长与金币保留。" :
                    run.State == RunState.Won ? "守关者已被击败。你无需清空所有普通怪物。" : "试着控制枪声，借助诱饵调动敌群，再选择战斗位置。";
                var description = Label(modal, explanation, new Vector2(0, -350), new Vector2(1100, 175), 23, muted, new Vector2(.5f, 1));
                description.alignment = TextAlignmentOptions.Center;
                Button(modal, run.State == RunState.Paused ? "继续探索" :
                    run.State == RunState.FloorCleared ? "进入下一层 [E]" : "重新开始", new Vector2(0, -580),
                    new Vector2(320, 65), () =>
                    {
                        if (run.State == RunState.Paused) run.SetState(RunState.Playing);
                        else if (run.State == RunState.FloorCleared) run.AdvanceFloor();
                        else run.Restart();
                    });
                if (run.State == RunState.Paused)
                {
                    var settings = Button(modal, "设置", new Vector2(0, -670), new Vector2(320, 65),
                        () => { settingsOpen = true; RefreshModal(); });
                    settings.name = "OpenSettings";
                }
            }
        }

        private void DrawSettings()
        {
            var heading = Label(modal, "设 置", new Vector2(0, -185), new Vector2(1000, 70),
                44, PixelArt.Cyan, new Vector2(.5f, 1));
            heading.alignment = TextAlignmentOptions.Center;
            var toggle = Button(modal, "手感调试模式：" + (run.FeelDebugMode ? "开启" : "关闭"),
                new Vector2(0, -340), new Vector2(560, 70), () =>
                {
                    run.SetFeelDebugMode(!run.FeelDebugMode);
                    RefreshModal();
                });
            toggle.name = "FeelDebugModeToggle";
            var description = Label(modal,
                "开启后拥有无限金币，购买与刷新不消耗余额。\n关闭后恢复正常消费，已有金币和拾取收益保留。\n仅本次探险有效，重新开始时默认关闭。",
                new Vector2(0, -440), new Vector2(1000, 135), 23, muted, new Vector2(.5f, 1));
            description.alignment = TextAlignmentOptions.Center;
            var back = Button(modal, "返回暂停菜单", new Vector2(0, -630), new Vector2(320, 65),
                () => { settingsOpen = false; RefreshModal(); });
            back.name = "CloseSettings";
        }

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
            label.fontStyle = FontStyles.Bold;
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
            var rect = Panel("Button", parent, position, size, new Color(.1f, .18f, .24f), new Vector2(.5f, 1));
            rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = button.colors; colors.highlightedColor = new Color(.6f, .95f, .95f);
            colors.pressedColor = PixelArt.Cyan; button.colors = colors;
            button.onClick.AddListener(action);
            var label = Label(rect, text, new Vector2(0, -10), size - new Vector2(10, 10), 24, Color.white, new Vector2(.5f, 1));
            label.alignment = TextAlignmentOptions.Center;
            return rect;
        }
    }
}
