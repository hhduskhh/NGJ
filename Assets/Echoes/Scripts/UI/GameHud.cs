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
        private UnityEngine.UI.Image[] roomIcons;
        private float noticeUntil;
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

            var header = Panel("Header", canvas, new Vector2(24, -24), new Vector2(432, 158), panel);
            Label(header, "寂 响 遗 迹", new Vector2(20, -12), new Vector2(380, 40), 29, Color.white);
            Label(header, "E C H O E S   B E L O W   /   原型 0.1", new Vector2(22, -53), new Vector2(390, 22), 13, PixelArt.Cyan);
            healthBar = Bar(header, new Vector2(22, -88), new Vector2(250, 11), PixelArt.Red);
            healthText = Label(header, "", new Vector2(288, -77), new Vector2(120, 28), 19, Color.white);
            xpBar = Bar(header, new Vector2(22, -119), new Vector2(250, 7), PixelArt.Cyan);
            xpText = Label(header, "", new Vector2(288, -109), new Vector2(130, 28), 16, muted);

            var info = Panel("Pressure", canvas, new Vector2(24, -196), new Vector2(315, 135), panel);
            Label(info, "躁 动 → 力 量", new Vector2(18, -12), new Vector2(280, 28), 17, muted);
            pressureText = Label(info, "", new Vector2(18, -45), new Vector2(285, 40), 27, PixelArt.Gold);
            Label(info, "青色细环 = 围攻加成计数范围", new Vector2(18, -99), new Vector2(290, 22), 15, muted);

            var map = Panel("MiniMap", canvas, new Vector2(-24, -24), new Vector2(244, 208), panel, new Vector2(1, 1));
            Label(map, "地 下 一 层", new Vector2(16, -10), new Vector2(215, 25), 17, muted);
            roomIcons = new UnityEngine.UI.Image[run.Map.Rooms.Count];
            for (int i = 0; i < roomIcons.Length; i++)
            {
                var b = run.Map.Rooms[i].Bounds;
                roomIcons[i] = Panel("Room" + i, map, new Vector2(19 + b.x / 18 * 70, -151 + b.y / 14 * 46),
                    new Vector2(58, 34), muted).GetComponent<UnityEngine.UI.Image>();
                Label(roomIcons[i].rectTransform, i == 0 ? "起点" : i == roomIcons.Length - 1 ? "守关" : "0" + (i + 1),
                    new Vector2(5, -3), new Vector2(50, 26), 14, Color.white);
            }
            roomText = Label(canvas, "", new Vector2(-24, -246), new Vector2(300, 30), 18, Color.white, new Vector2(1, 1));
            roomText.alignment = TextAlignmentOptions.Right;
            clockText = Label(canvas, "", new Vector2(-24, -283), new Vector2(300, 30), 17, muted, new Vector2(1, 1));
            clockText.alignment = TextAlignmentOptions.Right;
            goldText = Label(canvas, "", new Vector2(-24, -319), new Vector2(300, 30), 21, PixelArt.Gold, new Vector2(1, 1));
            goldText.alignment = TextAlignmentOptions.Right;

            var footer = Panel("Footer", canvas, new Vector2(24, 24), new Vector2(-48, 92), panel, Vector2.zero);
            footer.anchorMax = new Vector2(1, 0);
            statusText = Label(footer, "", new Vector2(18, -10), new Vector2(1450, 28), 19, PixelArt.Cyan);
            Label(footer, "WASD 移动   /   左键射击 · 自动瞄准可见敌人   /   Q 鼠标方向诱饵   /   E 商店 / 挑战守关者   /   Esc 暂停   /   M 静音",
                new Vector2(18, -49), new Vector2(1450, 28), 17, muted);
            noticeText = Label(canvas, "", new Vector2(0, -52), new Vector2(690, 45), 23, PixelArt.Gold, new Vector2(.5f, 1));
            noticeText.alignment = TextAlignmentOptions.Center;
            bossText = Label(canvas, "", new Vector2(0, -112), new Vector2(520, 28), 18, PixelArt.Red, new Vector2(.5f, 1));
            bossText.alignment = TextAlignmentOptions.Center;
            bossBar = Bar(canvas, new Vector2(0, -145), new Vector2(450, 8), PixelArt.Red, new Vector2(.5f, 1));
            bossBar.transform.parent.gameObject.SetActive(false);
            Notify("静止仅保留渐变视野 · 移动或射击生成视野圈", 8);
        }

        private void Update()
        {
            if (!run.Player) return;
            healthText.text = $"{Mathf.CeilToInt(run.Player.Health)} / {run.Player.MaxHealth:0}";
            healthBar.fillAmount = run.Player.Health / run.Player.MaxHealth;
            xpText.text = run.State == RunState.Upgrade ? $"Lv.{run.Level}  待升级" : $"Lv.{run.Level}  {run.Experience}/{run.RequiredExperience}";
            xpBar.fillAmount = Mathf.Clamp01((float)run.Experience / run.RequiredExperience);
            pressureText.text = $"附近 {run.NearbyEnemies}   /   伤害 +{(run.DamageMultiplier - 1) * 100:0}%";
            roomText.text = run.Map.Rooms[run.Map.CurrentRoom].Name;
            clockText.text = $"{(int)run.Elapsed / 60:00}:{(int)run.Elapsed % 60:00}   ·   击杀 {run.Kills}";
            goldText.text = $"金币 {run.Shop.Gold}   ·   武器 {run.Player.Weapons.Slots.Count}/6";
            float cooldown = run.Player.DecoyCooldownRemaining;
            statusText.text = $"枪声半径 {run.Player.NoiseRadius:0.#}   ·   诱饵 {(cooldown <= 0 ? "就绪 [Q]" : cooldown.ToString("0.0") + " 秒")}   ·   " +
                $"围攻上限 +{run.Player.PressureCap * run.Player.PressureBonus * 100:0}%   ·   {(run.Muted ? "声音已关闭 [M]" : "声音开启 [M]")}";
            if (Time.unscaledTime >= noticeUntil) noticeText.text = "";
            for (int i = 0; i < roomIcons.Length; i++)
                roomIcons[i].color = i == run.Map.CurrentRoom ? new Color(.16f, .52f, .53f) :
                    run.Map.Rooms[i].Visited ? new Color(.27f, .34f, .43f) : new Color(.12f, .17f, .25f);
            if (run.BossActivated)
            {
                bossBar.transform.parent.gameObject.SetActive(true);
                bossBar.fillAmount = run.Boss.Health / run.Boss.MaxHealth;
                bossText.text = $"守 关 者   /   {Mathf.CeilToInt(run.Boss.Health)}";
            }
            else if (Vector2.Distance(run.Player.transform.position, run.Map.AltarPosition) < 3)
                Notify("[E] 唤醒守关者 · 可随时挑战，无需清空普通怪", .2f);
            if (run.State == RunState.Playing && run.Shop.InRange) Notify("[E] 打开补给商店 · 武器 / 道具 / 金币刷新", .2f);
            if (run.State == RunState.Upgrade)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) run.ChooseUpgrade(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) run.ChooseUpgrade(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) run.ChooseUpgrade(2);
            }
        }

        public void Notify(string text, float seconds)
        { noticeText.text = text; noticeUntil = Time.unscaledTime + seconds; }

        public void RefreshModal()
        {
            if (modal) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); }
            if (run.State == RunState.Playing) return;
            modal = Panel("Modal", canvas, Vector2.zero, Vector2.zero, new Color(.025f, .04f, .065f, .96f));
            modal.anchorMin = Vector2.zero; modal.anchorMax = Vector2.one;
            modal.offsetMin = modal.offsetMax = Vector2.zero;
            modal.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            if (run.State == RunState.Shop) { DrawShop(); return; }
            string title = run.State == RunState.Upgrade ? "共 振 升 级" : run.State == RunState.Won ? "遗 迹 重 归 寂 静" :
                run.State == RunState.Lost ? "探 险 暂 止" : "暂 停 探 索";
            var heading = Label(modal, title, new Vector2(0, -185), new Vector2(1000, 70), 44, PixelArt.Cyan, new Vector2(.5f, 1));
            heading.alignment = TextAlignmentOptions.Center;
            var sub = Label(modal, run.State == RunState.Upgrade ? $"已暂停 · 升至等级 {run.Level + 1} · 点击卡片或按 1 / 2 / 3" :
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
                    Label(card, choice.Title, new Vector2(23, -83), new Vector2(310, 45), 29, Color.white);
                    Label(card, choice.Description, new Vector2(23, -155), new Vector2(310, 110), 21, muted);
                }
            }
            else
            {
                string explanation = run.State == RunState.Paused ?
                    "金色声波：枪声范围   /   青色声波：诱饵范围\n灰色横线：沉睡   /   金色方块：调查与警戒   /   红色竖线：追击\n普通房间始终开放，敌人会跨房间追击。\n前往右上方守关者圣所，靠近祭坛按 E 开战。" :
                    run.State == RunState.Won ? "守关者已被击败。你无需清空所有普通怪物。" : "试着控制枪声，借助诱饵调动敌群，再选择战斗位置。";
                var description = Label(modal, explanation, new Vector2(0, -350), new Vector2(1100, 175), 23, muted, new Vector2(.5f, 1));
                description.alignment = TextAlignmentOptions.Center;
                Button(modal, run.State == RunState.Paused ? "继续探索" : "重新开始", new Vector2(0, -580),
                    new Vector2(320, 65), () => { if (run.State == RunState.Paused) run.SetState(RunState.Playing); else run.Restart(); });
            }
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
