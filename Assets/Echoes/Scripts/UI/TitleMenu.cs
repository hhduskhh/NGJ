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
        private Button start, settingsButton, displayButton;
        private TMP_FontAsset font;

        public void Initialize(UnityAction begin)
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
            start = Hotspot("开始游戏", 414, begin);
            settingsButton = Hotspot("设置", 513, () => ToggleSettings(true));
            Hotspot("退出游戏", 610, Quit);

            var panel = Rect("Settings Panel", poster, .018f, .23f, .32f, .60f);
            settings = panel.gameObject;
            settings.AddComponent<Image>().color = new Color(.035f, .12f, .13f, .99f);
            displayButton = PanelButton("切换全屏", panel, .52f, () => Screen.fullScreen = !Screen.fullScreen);
            PanelButton("返回", panel, .12f, () => ToggleSettings(false));
            settings.SetActive(false);
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
            if (open) displayButton.Select();
            else if (!Input.GetMouseButtonUp(0)) settingsButton.Select();
        }

        private void Update()
        {
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
