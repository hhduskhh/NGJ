using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class PresentationArtTests
    {
        private RunController run;
        private const string Images = "Docs/Images/正式素材接入-20261114/";
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            run = Object.FindObjectOfType<RunController>();
        }

        [UnityTest] public IEnumerator CombatLootUpgradeAndShopUseFormalArt()
        {
            var art = PresentationArt.Current;
            Assert.AreSame(art.Weapon(0, 0), run.Player.Weapons.Slots[0].Visual.GetComponent<SpriteRenderer>().sprite);
            float elapsed = run.Elapsed;
            yield return new WaitForSeconds(.15f);
            Assert.Greater(run.Elapsed, elapsed, "Actual simulation must advance.");
            var enemy = run.Enemies.First(e => !e.IsBoss);
            Vector2 position = enemy.transform.position;
            for (int i = 0; i < 16; i++)
            {
                var delta = (Vector2)(Quaternion.Euler(0, 0, i * 22.5f) * Vector2.right) * 4;
                var candidate = run.Map.NearestFloor(position + delta);
                if (Vector2.Distance(candidate, position) > 3.5f && run.Map.HasLineOfSight(candidate, position))
                { run.Player.GetComponent<Rigidbody2D>().position = candidate; break; }
            }
            yield return new WaitForSeconds(.08f);
            int shots = 0; float deadline = Time.realtimeSinceStartup + 8;
            while (enemy.Alive && Time.realtimeSinceStartup < deadline)
            {
                if (run.Player.TryFire(enemy.transform.position, true)) shots++;
                yield return null;
            }
            Assert.IsFalse(enemy.Alive, "A real player projectile must kill the target.");
            Assert.Greater(shots, 0);
            var drop = Object.FindObjectOfType<GoldPickup>();
            if (drop)
            {
                Assert.AreSame(art.goldPickup, drop.GetComponent<SpriteRenderer>().sprite);
                Capture("01-loot-and-weapon");
                run.Player.GetComponent<Rigidbody2D>().position = drop.transform.position;
                yield return new WaitForSeconds(.08f);
            }
            Assert.GreaterOrEqual(run.Shop.Gold, run.Config.goldPerKill);

            int price = run.Shop.Price(run.Shop.Offers[0]);
            bool capturedUpgrade = false;
            foreach (var next in run.Enemies.Where(e => e.Alive && !e.IsBoss).ToArray())
            {
                if (run.Shop.Gold >= price && capturedUpgrade) break;
                next.TakeDamage(99999);
                if (run.State == RunState.Upgrade)
                {
                    yield return null;
                    var icons = Object.FindObjectsOfType<UnityEngine.UI.Image>().Where(i => i.name == "UpgradeIcon").ToArray();
                    Assert.AreEqual(3, icons.Length);
                    Assert.IsTrue(icons.All(i => i.sprite && i.preserveAspect && !i.raycastTarget));
                    Capture("02-upgrade");
                    var choice = icons[0].GetComponentInParent<UnityEngine.UI.Button>();
                    Click(choice);
                    capturedUpgrade = true;
                }
                run.Player.GetComponent<Rigidbody2D>().position = next.transform.position;
                yield return new WaitForSeconds(.06f);
            }
            Assert.IsTrue(capturedUpgrade);
            Assert.GreaterOrEqual(run.Shop.Gold, price);
            run.Player.GetComponent<Rigidbody2D>().position = run.Shop.Position;
            yield return new WaitForSeconds(.08f);
            Assert.IsTrue(run.Shop.Open());
            yield return null;
            var buy = FindButton("BuyOffer0");
            Assert.IsTrue(buy.interactable);
            int gold = run.Shop.Gold;
            Click(buy);
            Assert.AreEqual(2, run.Player.Weapons.Slots.Count);
            Assert.AreEqual(gold - price, run.Shop.Gold);
            yield return null;
            foreach (var slot in run.Player.Weapons.Slots)
            {
                Assert.AreSame(art.Weapon(slot.Kind, slot.Tier), slot.Visual.GetComponent<SpriteRenderer>().sprite);
                Assert.AreEqual(Vector3.one, slot.Visual.localScale);
                Assert.AreEqual(Color.white, slot.Visual.GetComponent<SpriteRenderer>().color);
            }
            Capture("03-shop-purchase");
            Click(FindButton("ShopExit"));
            Assert.AreEqual(RunState.Playing, run.State);
            float resumed = run.Elapsed;
            yield return new WaitForSeconds(.1f);
            Assert.Greater(run.Elapsed, resumed);
        }

        [UnityTest] public IEnumerator PassiveShopIconsFollowPurchaseAndPages()
        {
            var art = PresentationArt.Current;
            foreach (var item in run.Config.passives)
            {
                var sprite = art.Passive(item.title);
                Assert.IsNotNull(sprite, item.title);
                Assert.AreEqual(new Vector2(32, 32), sprite.rect.size);
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
            }
            run.Player.GetComponent<Rigidbody2D>().position = run.Shop.Position;
            yield return new WaitForFixedUpdate();
            run.Shop.Credit(500);
            Assert.IsTrue(run.Shop.Open());
            yield return null;
            var offer = run.Shop.Offers[2];
            var icon = GameObject.Find("Offer2").transform.Find("PassiveIcon").GetComponent<UnityEngine.UI.Image>();
            Assert.AreSame(art.Passive(offer.Item.title), icon.sprite);
            Assert.IsTrue(icon.preserveAspect);
            Assert.IsFalse(icon.raycastTarget);
            Click(FindButton("BuyOffer2"));
            yield return null;
            var owned = Object.FindObjectsOfType<UnityEngine.UI.Image>().Single(i => i.name == "OwnedPassiveIcon");
            Assert.AreSame(art.Passive(offer.Item.title), owned.sprite);
            foreach (var other in run.Config.passives.Where(item => item != offer.Item).Take(4))
                Assert.IsTrue(run.Player.Passives.Add(other));
            run.Hud.RefreshModal();
            yield return null;
            Capture("07-passive-shop-icons");
            Assert.AreEqual(4, Object.FindObjectsOfType<UnityEngine.UI.Image>().Count(i => i.name == "OwnedPassiveIcon"));
            Click(FindButton("ItemPage"));
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsOfType<UnityEngine.UI.Image>().Count(i => i.name == "OwnedPassiveIcon"));
            Click(FindButton("ShopExit"));
            Assert.AreEqual(RunState.Playing, run.State);
        }

        [UnityTest] public IEnumerator CatalogButtonStatesAndFullLoadoutRender()
        {
            var art = PresentationArt.Current;
            Assert.AreEqual(12, art.weapons.Distinct().Count());
            Assert.IsTrue(art.weapons.All(s => s && s.pixelsPerUnit == 16));
            Assert.AreEqual(4, art.offerPanels.Length);
            foreach (var upgrade in UpgradeDefinition.Catalog)
                Assert.IsNotNull(art.Upgrade(upgrade.Title), upgrade.Title);
            GoldPickup.Drop(run, (Vector2)run.Player.transform.position + Vector2.left * 2, 6);
            yield return null;
            Assert.AreSame(art.goldPickup, Object.FindObjectOfType<GoldPickup>().GetComponent<SpriteRenderer>().sprite);

            foreach (int kind in new[] { 1, 2, 0, 1, 2 }) Assert.IsTrue(run.Player.Weapons.Add(kind, 0));
            run.Player.PlaceAt(run.Shop.Position);
            run.Shop.Credit(500);
            Assert.IsTrue(run.Shop.Open());
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsOfType<EventSystem>().Length);
            var background = Object.FindObjectsOfType<UnityEngine.UI.Image>().Single(i => i.name == "ShopBackground");
            Assert.AreSame(art.shopBackground, background.sprite);
            Assert.IsFalse(background.raycastTarget);
            var panel = Object.FindObjectsOfType<UnityEngine.UI.Image>().Single(i => i.name == "ShopMainPanel");
            Assert.AreSame(art.shopMainPanel, panel.sprite);
            Assert.AreEqual(UnityEngine.UI.Image.Type.Sliced, panel.type);
            Assert.AreEqual(6, Object.FindObjectsOfType<UnityEngine.UI.Image>().Count(i => i.name == "EquippedWeaponIcon"));
            var refresh = FindButton("ShopRefresh");
            var graphic = (UnityEngine.UI.Image)refresh.targetGraphic;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(refresh.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.AreSame(art.buttonStates[1], graphic.overrideSprite);
            ExecuteEvents.Execute(refresh.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            Assert.AreSame(art.buttonStates[2], graphic.overrideSprite);
            ExecuteEvents.Execute(refresh.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            refresh.interactable = false;
            Assert.AreSame(art.buttonStates[3], graphic.overrideSprite);
            refresh.interactable = true;
            yield return null;
            Capture("04-shop-six-slots");
            Click(FindButton("MergeWeapon0"));
            Assert.AreEqual(1, run.Player.Weapons.Slots[0].Tier);
            Assert.AreSame(art.Weapon(0, 1), run.Player.Weapons.Slots[0].Visual.GetComponent<SpriteRenderer>().sprite);
            // RefreshModal replaces the shop hierarchy; let the new raycaster register first.
            yield return null;
            int gold = run.Shop.Gold, cost = run.Shop.Price(run.Shop.Offers[2]);
            Click(FindButton("BuyOffer2"));
            Assert.AreEqual(gold - cost, run.Shop.Gold);
            Assert.AreEqual(1, run.Player.Passives.Owned.Count);
            yield return null;
            gold = run.Shop.Gold;
            var sold = run.Player.Weapons.Slots[4];
            int refund = run.Player.Weapons.Definition(sold).prices[sold.Tier] / 2;
            Click(FindButton("SellWeapon4"));
            Assert.AreEqual(4, run.Player.Weapons.Slots.Count);
            Assert.AreEqual(gold + refund, run.Shop.Gold);
            yield return null;
            gold = run.Shop.Gold; cost = run.Shop.RefreshPrice;
            Click(FindButton("ShopRefresh"));
            Assert.AreEqual(gold - cost, run.Shop.Gold);
            Assert.AreEqual(1, run.Shop.Refreshes);
            yield return null;
            Capture("06-shop-transactions");
            Click(FindButton("ShopExit"));
            // Owned-weapon merging preserves the longer cooldown, including while the shop is paused.
            yield return new WaitForSeconds(run.Player.Weapons.Interval(run.Player.Weapons.Slots[0]) + .05f);
            var playerPosition = (Vector2)run.Player.transform.position;
            BellDecoy.Create(run, playerPosition + Vector2.right * 2);
            Assert.AreSame(GameArt.Get("bell"), Object.FindObjectOfType<BellDecoy>().GetComponent<SpriteRenderer>().sprite);
            Assert.IsTrue(run.Player.TryFire(playerPosition + Vector2.right * 4, true));
            yield return new WaitForSeconds(.25f);
            Capture("05-floating-loadout");
            for (int kind = 0; kind < 3; kind++) for (int tier = 0; tier < 4; tier++)
                Assert.AreSame(art.Weapon(kind, tier), GameArt.Get("weapon_" + (kind * 4 + tier)));
        }

        private static UnityEngine.UI.Button FindButton(string name) =>
            Object.FindObjectsOfType<UnityEngine.UI.Button>().Single(b => b.name == name);
        private static void Click(UnityEngine.UI.Button button)
        {
            Assert.IsTrue(button.interactable, button.name);
            Canvas.ForceUpdateCanvases();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, button.GetComponent<RectTransform>().TransformPoint(
                button.GetComponent<RectTransform>().rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, button.name + " must be reachable by the raycaster.");
            Assert.AreSame(button, hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>(),
                button.name + " must not be covered by another graphic.");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static void Capture(string name)
        {
            Directory.CreateDirectory(Images);
            var canvas = Object.FindObjectOfType<Canvas>();
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            bool scaling = scaler.enabled;
            float scale = canvas.scaleFactor, distance = canvas.planeDistance;
            var camera = canvas.worldCamera;
            try
            {
                // The offscreen target is the 1600x900 reference size, regardless of the batch Game View size.
                scaler.enabled = false;
                canvas.scaleFactor = 1;
                RuntimeSmokeTests.Capture(Images + name + ".png");
            }
            finally
            {
                canvas.scaleFactor = scale;
                canvas.planeDistance = distance; canvas.worldCamera = camera;
                scaler.enabled = scaling;
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
