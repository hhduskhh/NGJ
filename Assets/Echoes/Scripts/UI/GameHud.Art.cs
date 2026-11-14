using UnityEngine;

namespace Echoes
{
    public sealed partial class GameHud
    {
        private RectTransform ArtPanel(string name, Transform parent, Vector2 position, Vector2 size,
            Sprite sprite, Vector2? anchor = null, bool icon = false)
        {
            var rect = Panel(name, parent, position, size, Color.white, anchor);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.type = !icon && sprite.border != Vector4.zero ?
                UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = icon;
            image.raycastTarget = false;
            return rect;
        }

        private void SkinButton(RectTransform rect)
        {
            var art = PresentationArt.Current;
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.sprite = art.buttonStates[0];
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = Color.white;
            var button = rect.GetComponent<UnityEngine.UI.Button>();
            button.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            button.targetGraphic = image;
            button.spriteState = new UnityEngine.UI.SpriteState {
                highlightedSprite = art.buttonStates[1], selectedSprite = art.buttonStates[1],
                pressedSprite = art.buttonStates[2], disabledSprite = art.buttonStates[3]
            };
        }
    }
}
