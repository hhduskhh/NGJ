using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Echoes
{
    public sealed class TitleMenu : MonoBehaviour
    {
        private RectTransform poster;
        private GameObject settings;
        private GameObject selection;
        private Button firstHero;
        private Button start, settingsButton, displayButton;
        private TMP_FontAsset font;
        private TextMeshProUGUI laughLabel;

        public void Initialize(UnityAction<Hero> begin, GameConfig config)
        {
            if (!EventSystem.current)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvas = new GameObject("Title Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            poster = Rect("Title Poster", canvas.transform, 0, 0, 1, 1);
            var art = poster.gameObject.AddComponent<RawImage>();
            art.texture = Resources.Load<Texture2D>("TitlePoster");
            var fitter = poster.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)art.texture.width / art.texture.height;
            font = Resources.Load<TMP_FontAsset>("FusionPixelUI");
            start = Hotspot("开始游戏", 414, () => ToggleSelection(true));
            settingsButton = Hotspot("设置", 513, () => ToggleSettings(true));
            Hotspot("退出游戏", 610, Quit);

            var panel = Rect("Settings Panel", poster, .018f, .23f, .32f, .60f);
            settings = panel.gameObject;
            settings.AddComponent<Image>().color = new Color(.035f, .12f, .13f, .99f);
            displayButton = PanelButton("切换全屏", panel, .66f, () => Screen.fullScreen = !Screen.fullScreen);
            var laughButton = PanelButton("特殊音效", panel, .35f, () =>
            {
                NoiseSystem.ScreenLightLaughEnabled = !NoiseSystem.ScreenLightLaughEnabled;
                RefreshLaughLabel();
            });
            laughLabel = laughButton.GetComponentInChildren<TextMeshProUGUI>();
            PanelButton("返回", panel, .04f, () => ToggleSettings(false));
            settings.SetActive(false);
            BuildSelection(begin, config);
        }

        private static readonly Color Paper = new Color32(250, 239, 211, 255);
        private static readonly Color Brass = new Color32(230, 180, 102, 255);
        private static readonly Color Mint = new Color32(132, 217, 201, 255);
        private static readonly Color Rule = new Color32(74, 80, 77, 255);

        private void BuildSelection(UnityAction<Hero> begin, GameConfig config)
        {
            var panel = Rect("选择角色", poster, 0, 0, 1, 1);
            selection = panel.gameObject;
            var backdrop = panel.gameObject.AddComponent<RawImage>();
            backdrop.texture = poster.GetComponent<RawImage>().texture;
            backdrop.uvRect = new Rect(.54f, 0, .46f, 1);
            backdrop.color = new Color32(28, 33, 37, 255);

            CardLabel(Rect("标题", panel, .075f, .83f, .65f, .955f), "选择角色", 58, Paper);
            Fill(Rect("分隔线", panel, .075f, .816f, .925f, .818f), Rule);
            var back = Rect("返回标题", panel, .818f, .855f, .925f, .925f);
            var backButton = MakeButton(back, () => ToggleSelection(false));
            var backColors = backButton.colors;
            backColors.normalColor = new Color32(43, 49, 48, 255);
            backColors.highlightedColor = backColors.selectedColor = new Color32(92, 88, 69, 255);
            backButton.colors = backColors;
            CardLabel(Rect("返回", back, 0, 0, 1, 1), "返回", 24, Paper, TextAlignmentOptions.Center);

            firstHero = HeroCard(panel, Hero.Explorer, begin, config);
            HeroCard(panel, Hero.Runner, begin, config);
            selection.SetActive(false);
        }

        private Button HeroCard(RectTransform panel, Hero hero, UnityAction<Hero> begin, GameConfig config)
        {
            bool runner = hero == Hero.Runner;
            string name = runner ? "主角二" : "主角一";
            Color accent = runner ? Mint : Brass;
            const float cardWidth = .415f;
            float left = runner ? .51f : .075f;
            var card = Rect(name, panel, left, .105f, left + cardWidth, .763f);
            var button = MakeButton(card, () => begin(hero));
            var colors = button.colors;
            colors.normalColor = Rule;
            colors.highlightedColor = colors.selectedColor = accent;
            colors.pressedColor = Color.white;
            button.colors = colors;

            var body = Rect("卡面", card, .006f, .006f, .994f, .994f);
            var portrait = body.gameObject.AddComponent<RawImage>();
            portrait.texture = Resources.Load<Texture2D>(runner ? "SelectRunner" : "SelectExplorer");
            portrait.raycastTarget = false;

            // Keep the in-game likeness visible on both illustrations.
            var badge = Rect("精灵底板", body, .022f, .024f, .138f, .154f);
            Fill(badge, new Color32(14, 22, 25, 226));
            var icon = Rect("游戏内形象", badge, .06f, .04f, .94f, .96f).gameObject.AddComponent<Image>();
            icon.sprite = GameArt.Get(GameArt.PlayerKind(hero));
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            if (runner)
            {
                var caption = Rect("说明底板", body, .138f, .024f, .978f, .154f);
                Fill(caption, new Color32(14, 22, 25, 236));
                string conversion = $"每 1% 永久移速加成，伤害 +{config.runnerSpeedConversion:0.##}%，上限 +{config.runnerMaxDamageBonus:0%}";
                CardLabel(Rect("被动", caption, .022f, .08f, .976f, .92f), conversion, 24, Paper);
            }
            return button;
        }

        private static void Fill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private void CardLabel(RectTransform rect, string value, int size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = value;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8;
            label.fontSizeMax = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
        }

        private void ToggleSelection(bool open)
        {
            foreach (Transform child in poster)
                if (child.GetComponent<Button>()) child.gameObject.SetActive(!open);
            selection.SetActive(open);
            if (open) firstHero.Select();
            else start.Select();
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float right, float top)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Button MakeButton(RectTransform rect, UnityAction action)
        {
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.clear;
            colors.highlightedColor = colors.selectedColor = new Color(1, .87f, .55f, .12f);
            colors.pressedColor = new Color(.4f, .25f, .09f, .32f);
            button.colors = colors;
            button.onClick.AddListener(action);
            return button;
        }

        private Button Hotspot(string name, int topPixel, UnityAction action)
        {
            // Menu lettering is baked into the 1672 x 941 poster. Match its 400 x 88 plaque.
            var rect = Rect(name, poster, 47f / 1672, 1 - (topPixel + 88) / 941f,
                447f / 1672, 1 - topPixel / 941f);
            var graphic = rect.gameObject.AddComponent<Image>();
            graphic.canvasRenderer.cullTransparentMesh = false;
            graphic.color = Color.clear;
            var button = rect.gameObject.AddComponent<TitleMenuButton>();
            button.targetGraphic = graphic;
            button.onClick.AddListener(action);
            return button;
        }

        private Button PanelButton(string name, Transform parent, float y, UnityAction action)
        {
            var rect = Rect(name, parent, .08f, y, .92f, y + .3f);
            var button = MakeButton(rect, action);
            var label = Rect("Label", rect, 0, 0, 1, 1).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = name;
            label.enableAutoSizing = true;
            label.fontSizeMax = 42;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return button;
        }

        private void ToggleSettings(bool open)
        {
            foreach (Transform child in poster)
                if (child.GetComponent<Button>()) child.gameObject.SetActive(!open);
            settings.SetActive(open);
            if (open)
            {
                RefreshLaughLabel();
                displayButton.Select();
            }
            else if (!Input.GetMouseButtonUp(0)) settingsButton.Select();
        }

        private void RefreshLaughLabel()
        {
            laughLabel.text = "特殊音效  " + (NoiseSystem.ScreenLightLaughEnabled ? "开" : "关");
        }

        private void Update()
        {
            if (selection.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) ToggleSelection(false);
                return;
            }
            if (settings.activeSelf && Input.GetKeyDown(KeyCode.Escape)) ToggleSettings(false);
            if (!settings.activeSelf && !EventSystem.current.currentSelectedGameObject &&
                (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow) ||
                 Input.GetKeyDown(KeyCode.Tab)))
                start.Select();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
