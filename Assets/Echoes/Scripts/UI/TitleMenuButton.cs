using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Echoes
{
    public sealed class TitleMenuButton : Button
    {
        private static readonly Color FocusColor = new Color(.025f, .065f, .075f, .35f);

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (IsInteractable()) Select();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (targetGraphic)
                targetGraphic.color = state == SelectionState.Selected || state == SelectionState.Pressed
                    ? FocusColor : Color.clear;
        }
    }
}
