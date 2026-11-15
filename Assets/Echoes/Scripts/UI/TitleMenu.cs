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

        public void Initialize(UnityAction<Hero> begin)
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
            BuildSelection(begin);
        }

        private void BuildSelection(UnityAction<Hero> begin)
        {
            var panel = Rect("选择角色", poster, 0, 0, 1, 1);
            selection = panel.gameObject;
            selection.AddComponent<Image>().color = new Color(.035f, .09f, .12f);
            CardLabel(Rect("标题", panel, .12f, .80f, .88f, .94f), "选择角色", 52);
            firstHero = HeroCard(panel, .14f, Hero.Explorer, "主角一", "均衡型", "标准移速与火力，生命充足", begin);
            HeroCard(panel, .52f, Hero.Runner, "主角二", "追风", "移速转伤害，最高 +100%", begin);
            var back = Rect("返回标题", panel, .42f, .055f, .58f, .15f);
            MakeButton(back, () => ToggleSelection(false));
            CardLabel(Rect("文字", back, 0, 0, 1, 1), "返回", 32);
            selection.SetActive(false);
        }

        private Button HeroCard(RectTransform panel, float x, Hero hero, string title, string trait,
            string description, UnityAction<Hero> begin)
        {
            var card = Rect(title, panel, x, .20f, x + .34f, .78f);
            var button = MakeButton(card, () => begin(hero));
            var colors = button.colors;
            colors.normalColor = new Color(.12f, .22f, .25f);
            colors.highlightedColor = colors.selectedColor = new Color(.24f, .37f, .38f);
            colors.pressedColor = new Color(.32f, .39f, .32f);
            button.colors = colors;
            var portrait = Rect("肖像", card, .15f, .43f, .85f, .96f).gameObject.AddComponent<Image>();
            portrait.sprite = GameArt.Get(GameArt.PlayerKind(hero));
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            CardLabel(Rect("名称", card, .03f, .28f, .97f, .48f), title + " · " + trait, 38);
            CardLabel(Rect("特性", card, .04f, .08f, .96f, .24f), description, 25);
            return button;
        }

        private void CardLabel(RectTransform rect, string value, int size)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = value;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8;
            label.fontSizeMax = size;
            label.alignment = TextAlignmentOptions.Center;
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
