using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class FeelDebugModeTests
    {
        private static void Click(string name)
        {
            var button = Object.FindObjectsOfType<UnityEngine.UI.Button>().Single(b => b.name == name);
            Assert.IsTrue(button.interactable, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator SettingsEnableUnlimitedSpendingAndRestoreNormalBalanceWhenDisabled()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            Assert.IsFalse(run.FeelDebugMode);
            Assert.AreEqual(0, run.Shop.Gold);
            run.SetState(RunState.Paused);
            yield return null;
            Click("OpenSettings");
            yield return null;
            Click("FeelDebugModeToggle");
            yield return null;
            Assert.IsTrue(run.FeelDebugMode);
            Assert.AreEqual(0, Time.timeScale);
            Directory.CreateDirectory("TestResults/GameplayVerification");
            RuntimeSmokeTests.Capture("TestResults/GameplayVerification/04-debug-settings.png");
            Click("CloseSettings");
            run.SetState(RunState.Playing);
            run.Player.PlaceAt(run.Shop.Position);
            yield return null;
            Assert.IsTrue(run.Shop.Open());
            Assert.AreEqual("无限", run.Shop.GoldText);
            yield return null;
            Click("BuyOffer0");
            Assert.AreEqual(2, run.Player.Weapons.Slots.Count);
            Assert.AreEqual(0, run.Shop.Gold);
            Assert.IsFalse(run.Shop.Buy(0), "Sold stock remains sold in debug mode");
            run.Shop.Credit(7);
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Click("ShopRefresh");
                Assert.AreEqual(7, run.Shop.Gold, "Earned gold is kept while debug purchases are free");
            }
            Assert.AreEqual(3, run.Shop.Refreshes);
            yield return null;
            RuntimeSmokeTests.Capture("TestResults/GameplayVerification/05-unlimited-gold-shop.png");
            run.Shop.Close();
            run.SetState(RunState.Paused);
            yield return null;
            Click("OpenSettings");
            yield return null;
            Click("FeelDebugModeToggle");
            Assert.IsFalse(run.FeelDebugMode);
            Assert.AreEqual("7", run.Shop.GoldText);
            run.SetState(RunState.Playing);
            Assert.IsTrue(run.Shop.Open());
            Assert.IsFalse(run.Shop.RefreshStock(), "Turning debug off restores the gold requirement");
            Assert.IsFalse(run.Shop.Buy(0));
            var offer = run.Shop.Offers[0];
            run.Shop.Credit(run.Shop.Price(offer));
            int balance = run.Shop.Gold;
            Assert.IsTrue(run.Shop.Buy(0));
            Assert.AreEqual(balance - run.Shop.Price(offer), run.Shop.Gold);
            run.SetFeelDebugMode(true);
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            Assert.IsFalse(Object.FindObjectOfType<RunController>().FeelDebugMode, "A new run starts normally");
        }
    }
}
