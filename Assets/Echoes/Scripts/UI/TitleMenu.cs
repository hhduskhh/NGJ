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
            start = Hotspot("开始游戏", .036f, .532f, begin);
            settingsButton = Hotspot("设置", .036f, .409f, () => ToggleSettings(true));
            Hotspot("退出游戏", .036f, .284f, Quit);

            var panel = Rect("Settings Panel", poster, .018f, .23f, .32f, .60f);
            settings = panel.gameObject;
            settings.AddComponent<Image>().color = new Color(.035f, .12f, .13f, .99f);
            displayButton = PanelButton("切换全屏", panel, .52f, () => Screen.fullScreen = !Screen.fullScreen);
            PanelButton("返回", panel, .12f, () => ToggleSettings(false));
            settings.SetActive(false);
            start.Select();
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

        private Button Hotspot(string name, float left, float bottom, UnityAction action)
        {
            return MakeButton(Rect(name, poster, left, bottom, .287f, bottom + .092f), action);
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
            (open ? displayButton : settingsButton).Select();
        }

        private void Update()
        {
            if (settings.activeSelf && Input.GetKeyDown(KeyCode.Escape)) ToggleSettings(false);
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
