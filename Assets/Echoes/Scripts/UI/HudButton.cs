using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Echoes
{
    // The hit rectangle stays still while the face, icon and lettering move together.
    public sealed class HudButton : UnityEngine.UI.Button
    {
        public RectTransform Face { get; private set; }
        private NoiseSystem sound;
        private float depth;
        private TMP_Text shortage;
        private Color normalText;
        private bool pointerHeld, keyHeld;
        private float highlightUntil;

        public void Initialize(RectTransform face, NoiseSystem audio, float pressDepth)
        {
            Face = face;
            sound = audio;
            depth = pressDepth;
            targetGraphic = face.GetComponent<UnityEngine.UI.Image>();
        }

        private float reboundUntil;

        public void Rebound()
        {
            reboundUntil = Time.unscaledTime + .075f;
            Face.anchoredPosition = Vector2.down * depth;
        }

        public void SetShortageLabel(TMP_Text label)
        {
            shortage = label;
            normalText = label.color;
        }

        public void HoldKey(bool value)
        {
            keyHeld = value;
            if (value) sound.PlayUiPress();
            MoveFace(pointerHeld || keyHeld);
        }

        private void MoveFace(bool down)
        {
            if (Face) Face.anchoredPosition = down ? Vector2.down * (IsInteractable() ? depth : depth * .5f) : Vector2.zero;
        }

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);
            if (e.button != PointerEventData.InputButton.Left || (!IsInteractable() && !shortage)) return;
            reboundUntil = 0;
            pointerHeld = true;
            MoveFace(true);
            if (IsInteractable()) sound.PlayUiPress();
            else
            {
                shortage.color = new Color32(255, 214, 143, 255);
                highlightUntil = Time.unscaledTime + .20f;
            }
        }

        public override void OnPointerExit(PointerEventData e)
        {
            base.OnPointerExit(e);
            MoveFace(keyHeld);
        }

        public override void OnPointerEnter(PointerEventData e)
        {
            base.OnPointerEnter(e);
            MoveFace(pointerHeld || keyHeld);
        }

        public override void OnPointerUp(PointerEventData e)
        {
            base.OnPointerUp(e);
            if (e.button != PointerEventData.InputButton.Left) return;
            pointerHeld = false;
            MoveFace(keyHeld);
        }

        private void Update()
        {
            if (reboundUntil > 0)
            {
                float remaining = Mathf.Clamp01((reboundUntil - Time.unscaledTime) / .075f);
                Face.anchoredPosition = Vector2.down * (depth * remaining);
                if (remaining == 0) reboundUntil = 0;
            }
            if (highlightUntil > 0 && Time.unscaledTime >= highlightUntil)
            {
                highlightUntil = 0;
                shortage.color = normalText;
            }
        }

        protected override void OnDisable()
        {
            pointerHeld = keyHeld = false;
            MoveFace(false);
            base.OnDisable();
        }
    }
}
