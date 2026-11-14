using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class HudFeedbackTests
    {
        private RunController run;
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            run = Object.FindObjectOfType<RunController>();
        }

        [UnityTest]
        public IEnumerator ButtonTravelShortageSaleAndImmediateRefresh()
        {
            run.Player.PlaceAt(run.Shop.Position);
            Assert.IsTrue(run.Shop.Open());
            var pointer = new PointerEventData(EventSystem.current);
            var buy = GameObject.Find("BuyOffer0").GetComponent<HudButton>();
            buy.OnPointerDown(pointer);
            Assert.AreEqual(-run.Config.buttonPressDepth / 2, buy.Face.anchoredPosition.y);
            buy.OnPointerUp(pointer);
            buy.OnPointerClick(pointer);
            Assert.IsFalse(run.Shop.Offers[0].Sold);
            run.Shop.Credit(2000);
            run.Hud.RefreshModal();
            buy = GameObject.Find("BuyOffer0").GetComponent<HudButton>();
            var hitRect = buy.GetComponent<RectTransform>();
            Vector2 anchor = hitRect.anchoredPosition;
            buy.OnPointerDown(pointer);
            Assert.AreEqual(-run.Config.buttonPressDepth, buy.Face.anchoredPosition.y);
            Assert.AreEqual(anchor, hitRect.anchoredPosition);
            buy.OnPointerExit(pointer);
            buy.OnPointerUp(pointer);
            Assert.AreEqual(Vector2.zero, buy.Face.anchoredPosition);
            Assert.IsFalse(run.Shop.Offers[0].Sold);
            buy.OnPointerEnter(pointer);
            buy.OnPointerDown(pointer);
            buy.OnPointerUp(pointer);
            buy.OnPointerClick(pointer);
            buy = GameObject.Find("BuyOffer0").GetComponent<HudButton>();
            buy.OnPointerDown(pointer);
            Assert.IsTrue(run.Shop.Offers[0].Sold);
            Assert.AreEqual(Vector2.zero, buy.Face.anchoredPosition);
            for (int i = 0; i < 2; i++)
                GameObject.Find("ShopRefresh").GetComponent<HudButton>().OnPointerClick(pointer);
            Assert.AreEqual(2, run.Shop.Refreshes);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.AreEqual(Vector2.zero, GameObject.Find("ShopRefresh").GetComponent<HudButton>().Face.anchoredPosition);
        }

        [UnityTest]
        public IEnumerator UpgradeOnlyOnMatchingReleaseAndDamageTrailSettles()
        {
            run.Player.TakeDamage(10);
            yield return null;
            var trail = GameObject.Find("DamageTrail").GetComponent<UnityEngine.UI.Image>();
            float hp = run.Player.Health / run.Player.MaxHealth;
            Assert.Less(hp, 1);
            Assert.Greater(trail.fillAmount, hp);
            yield return new WaitForSecondsRealtime(run.Config.healthTrailSeconds + .1f);
            Assert.AreEqual(hp, trail.fillAmount, .001f);
            for (int i = 0; run.State == RunState.Playing; i++) run.Enemies[i].TakeDamage(99999);
            run.Hud.PressUpgradeKey(1);
            Assert.AreEqual(-run.Config.buttonPressDepth, GameObject.Find("UpgradeChoice1").GetComponent<HudButton>().Face.anchoredPosition.y);
            run.Hud.ReleaseUpgradeKey(0);
            Assert.AreEqual(RunState.Upgrade, run.State);
            run.Hud.ReleaseUpgradeKey(1);
            Assert.AreEqual(2, run.Level);
            Assert.AreEqual(RunState.Playing, run.State);
        }

        [UnityTest]
        public IEnumerator RevealConsumesFirstPressAndReleaseBeforeDescent()
        {
            run.SetState(RunState.FloorCleared);
            var page = GameObject.Find("RunResult").GetComponent<CanvasGroup>();
            Assert.AreEqual(0, page.alpha);
            Assert.IsFalse(page.interactable);
            yield return null;
            run.Hud.HandleResultInput(false, true, true, false);
            Assert.AreEqual(1, page.alpha);
            run.Hud.HandleResultInput(false, false, false, true);
            Assert.AreEqual(1, run.Floor);
            yield return null;
            run.Hud.HandleResultInput(true, false, false, false);
            Assert.AreEqual(2, run.Floor);
            run.SetState(RunState.Won);
            page = GameObject.Find("RunResult").GetComponent<CanvasGroup>();
            Assert.IsFalse(page.interactable);
            yield return new WaitForSecondsRealtime(run.Config.victoryRevealSeconds + .15f);
            Assert.IsTrue(page.interactable);
            Assert.AreEqual(1, page.alpha);
        }
    }
}
