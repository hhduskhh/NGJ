using TMPro;
using UnityEngine;

namespace Echoes
{
    public sealed partial class GameHud
    {
        private UnityEngine.UI.Image damageTrail;
        private RectTransform bellIcon;
        private TMP_Text shopBalance;
        private float lastHealth, trailStart, trailAmount, bellUntil, pressureUntil, balanceFlashUntil;
        private bool coolingDown, underPressure;
        private readonly HudButton[] upgradeButtons = new HudButton[3];
        private int heldChoice = -1;

        private CanvasGroup resultPage;
        private LineRenderer lastEcho;
        private float revealStart, revealDuration;
        private bool revealing, swallowMouseRelease;
        private int revealFrame, consumedFrame = -1;

        private void UpdateHudFeedback(float cooldown)
        {
            float now = Time.unscaledTime;
            float hp = healthBar.fillAmount;
            if (run.Player.Health < lastHealth)
            {
                trailAmount = Mathf.Max(damageTrail.fillAmount, lastHealth / run.Player.MaxHealth);
                trailStart = now;
            }
            lastHealth = run.Player.Health;
            float t = (now - trailStart) / Mathf.Max(.01f, run.Config.healthTrailSeconds);
            damageTrail.fillAmount = Mathf.Max(hp, Mathf.Lerp(trailAmount, hp, Mathf.InverseLerp(.25f, 1f, t)));

            if (coolingDown && cooldown <= 0) bellUntil = now + .3f;
            coolingDown = cooldown > 0;
            if (bellUntil > 0)
            {
                float remaining = Mathf.Clamp01((bellUntil - now) / .3f);
                bellIcon.localRotation = Quaternion.Euler(0, 0, Mathf.Sin((1 - remaining) * Mathf.PI * 2) * remaining * 16);
                if (remaining == 0) bellUntil = 0;
            }

            bool nearby = run.NearbyEnemies > 0;
            if (!underPressure && nearby) pressureUntil = now + .24f;
            underPressure = nearby;
            pressureText.color = Color.Lerp(PixelArt.Gold, Color.white, (pressureUntil - now) / .24f);
            if (shopBalance && balanceFlashUntil > 0)
            {
                shopBalance.color = Color.Lerp(PixelArt.Gold, Color.white, (balanceFlashUntil - now) / .22f);
                if (now >= balanceFlashUntil) balanceFlashUntil = 0;
            }
        }

        private void ReadUpgradeKeys()
        {
            if (heldChoice >= 0)
            {
                var key = (KeyCode)((int)KeyCode.Alpha1 + heldChoice);
                if (Input.GetKeyUp(key)) ReleaseUpgradeKey(heldChoice);
                return;
            }
            for (int i = 0; i < run.Choices.Count; i++)
            {
                if (!Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) continue;
                PressUpgradeKey(i);
                break;
            }
        }

        public void PressUpgradeKey(int index)
        {
            if (run.State != RunState.Upgrade || heldChoice >= 0 || index < 0 || index >= run.Choices.Count) return;
            heldChoice = index;
            upgradeButtons[index].HoldKey(true);
        }

        public void ReleaseUpgradeKey(int index)
        {
            if (heldChoice < 0 || heldChoice != index) return;
            CancelChoicePress();
            run.ChooseUpgrade(index);
        }

        private void CancelChoicePress()
        {
            if (heldChoice >= 0 && upgradeButtons[heldChoice]) upgradeButtons[heldChoice].HoldKey(false);
            heldChoice = -1;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelChoicePress();
        }

        private void ResetScreenFeedback()
        {
            CancelChoicePress();
            if (lastEcho) Destroy(lastEcho.gameObject);
            resultPage = null;
            revealing = swallowMouseRelease = false;
            shopBalance = null;
            balanceFlashUntil = 0;
        }

        private void BeginResultReveal()
        {
            bossText.text = "";
            bossBar.transform.parent.gameObject.SetActive(false);
            Notify("", 0);
            revealing = true;
            revealFrame = Time.frameCount;
            revealStart = Time.unscaledTime;
            revealDuration = Mathf.Max(.01f, run.State == RunState.Won ? run.Config.victoryRevealSeconds : run.Config.floorRevealSeconds);
            resultPage = modal.gameObject.AddComponent<CanvasGroup>();
            resultPage.alpha = 0;
            resultPage.interactable = resultPage.blocksRaycasts = false;
            modalBackdrop.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            lastEcho = PixelArt.Circle("FinalEcho", run.transform, PixelArt.Gold);
            lastEcho.transform.position = run.Boss.transform.position;
            lastEcho.sortingOrder = 36;
            swallowMouseRelease = Input.GetMouseButton(0);
        }

        private void SetRevealProgress(float t)
        {
            if (lastEcho)
            {
                PixelArt.SetRadius(lastEcho, Mathf.Lerp(.8f, 6f, t));
                var color = PixelArt.Gold;
                color.a = .8f * (1 - t);
                lastEcho.startColor = lastEcho.endColor = color;
            }
            float page = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.38f, 1, t));
            resultPage.alpha = page;
            modalBackdrop.GetComponent<UnityEngine.UI.Image>().color = Color.Lerp(Color.clear, ResultShade, page);
            if (t < 1) return;
            revealing = false;
            if (lastEcho) Destroy(lastEcho.gameObject);
        }

        public void ReadResultInput()
        {
            if (!resultPage || Time.frameCount == revealFrame) return;
            bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space) || (run.State == RunState.FloorCleared && Input.GetKeyDown(KeyCode.E));
            HandleResultInput(confirm, Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0));
        }

        public void HandleResultInput(bool confirm, bool mouseDown, bool mouseHeld, bool mouseUp)
        {
            if (!resultPage || Time.frameCount == revealFrame) return;
            if (revealing)
            {
                bool skip = mouseDown || confirm;
                if (skip)
                {
                    consumedFrame = Time.frameCount;
                    swallowMouseRelease = mouseHeld;
                }
                SetRevealProgress(skip ? 1 : Mathf.Clamp01((Time.unscaledTime - revealStart) / revealDuration));
                return;
            }
            if (swallowMouseRelease)
            {
                if (mouseHeld || mouseUp) return;
                swallowMouseRelease = false;
            }
            resultPage.interactable = resultPage.blocksRaycasts = true;
            if (confirm) ConfirmResult();
        }

        private void ConfirmResult()
        {
            if (revealing || swallowMouseRelease || consumedFrame == Time.frameCount) return;
            consumedFrame = Time.frameCount;
            if (run.State == RunState.FloorCleared) run.AdvanceFloor();
            else if (run.State == RunState.Won || run.State == RunState.Lost) run.Restart();
        }
    }
}
