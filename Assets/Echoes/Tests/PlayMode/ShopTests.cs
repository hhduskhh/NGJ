using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class ShopTests
    {
        private RunController run;
        private sealed class QualitySample : System.Random
        {
            private readonly double sample;
            public QualitySample(double sample) { this.sample = sample; }
            public override double NextDouble() => sample;
            public override int Next(int maxValue) => 0;
        }
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            run = Object.FindObjectOfType<RunController>();
            // Test setup moves to the real counter without granting the
            // floor-transition invulnerability of PlayerController.PlaceAt.
            run.Player.GetComponent<Rigidbody2D>().position = run.Shop.Position;
            run.Player.transform.position = run.Shop.Position;
            Physics2D.SyncTransforms();
            yield return null;
        }
        [UnityTest] public IEnumerator CorpseAndShadowFadeWhileDroppedGoldRemains()
        {
            var enemy = run.Enemies[0];
            enemy.TakeDamage(enemy.MaxHealth);
            var body = enemy.GetComponent<SpriteRenderer>();
            var shadow = enemy.transform.Find("FootShadow").GetComponent<SpriteRenderer>();
            yield return new WaitForSecondsRealtime(run.Config.corpseHoldSeconds + run.Config.corpseFadeSeconds * .5f);
            Assert.That(body.color.a, Is.InRange(.1f, .9f));
            Assert.AreEqual(body.color.a, shadow.color.a, .001f);
            yield return new WaitForSecondsRealtime(run.Config.corpseFadeSeconds);
            Assert.IsFalse(enemy.gameObject.activeSelf);
            Assert.AreEqual(1, run.Kills);
            Assert.AreEqual(1, Object.FindObjectsOfType<GoldPickup>().Length);
        }

        [UnityTest] public IEnumerator GoldFliesOnlyInRangeAndCreditsOnceOnArrival()
        {
            Vector2 origin = run.Map.StartPosition;
            float radius = run.Config.goldPickupRadius;
            var direction = new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up }
                .First(d => run.Map.HasClearInteractionPath(origin, origin + d * (radius + 2)));
            run.Player.PlaceAt(origin + direction * (radius + 2));
            GoldPickup.Drop(run, origin, 7);
            var coin = Object.FindObjectOfType<GoldPickup>();
            yield return new WaitForSeconds(.1f);
            Assert.AreEqual(origin, (Vector2)coin.transform.position);
            run.Player.PlaceAt(origin + direction * (radius - .25f));
            yield return new WaitForSeconds(.08f);
            Vector2 midway = coin.transform.position;
            Assert.Greater(Vector2.Distance(midway, origin), 0);
            Assert.AreEqual(0, run.Shop.Gold);
            run.SetState(RunState.Paused);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.AreEqual(midway, (Vector2)coin.transform.position);
            run.SetState(RunState.Playing);
            run.Player.PlaceAt(origin + direction * (radius + 1));
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(coin);
            Assert.AreEqual(7, run.Shop.Gold);
        }

        [UnityTest] public IEnumerator LootPausePurchaseRefreshAndReopen()
        {
            Assert.AreEqual(0, run.Shop.Gold);
            Assert.IsTrue(run.Shop.InRange);
            var enemy = run.Enemies[0];
            enemy.TakeDamage(99999);
            Assert.AreEqual(1, Object.FindObjectsOfType<GoldPickup>().Length);
            run.Player.GetComponent<Rigidbody2D>().position = enemy.transform.position;
            yield return new WaitForSeconds(.05f);
            Assert.AreEqual(run.Config.goldPerKill, run.Shop.Gold);
            Assert.IsFalse(run.Shop.Open(), "Remote shopping is blocked");
            run.Player.GetComponent<Rigidbody2D>().position = run.Shop.Position;
            yield return new WaitForSeconds(.05f);
            Assert.IsTrue(run.Shop.Open());
            float elapsed = run.Elapsed;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(elapsed, run.Elapsed);
            Assert.IsFalse(run.Shop.Buy(0));
            run.Shop.Credit(1000);
            var first = run.Shop.Offers[0];
            int before = run.Shop.Gold;
            Assert.IsTrue(run.Shop.Buy(0));
            Assert.AreEqual(before - run.Shop.Price(first), run.Shop.Gold);
            Assert.IsFalse(run.Shop.Buy(0));
            Assert.AreEqual(2, run.Player.Weapons.Slots.Count);
            run.Shop.Close(); Assert.IsTrue(run.Shop.Open());
            Assert.AreSame(first, run.Shop.Offers[0]); Assert.IsTrue(first.Sold);
            before = run.Shop.Gold; int price = run.Shop.RefreshPrice;
            Assert.IsTrue(run.Shop.RefreshStock());
            Assert.AreEqual(before - price, run.Shop.Gold);
            Assert.AreEqual(1, run.Shop.Refreshes);
            Assert.IsTrue(run.Shop.Buy(2));
            Assert.AreEqual(1, run.Player.Passives.Owned.Count);
            yield return null;
            RuntimeSmokeTests.Capture("TestResults/22-shop.png");
            run.Shop.Close();
            yield return new WaitForSeconds(.1f);
            Assert.Greater(run.Elapsed, elapsed);
        }
        [UnityTest] public IEnumerator QualityChangesOnRefreshAfterFiveLevels()
        {
            typeof(ShopController).GetField("random", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(run.Shop, new QualitySample(.63));
            run.Shop.BeginFloor(run.Map.Rooms[0].Center);
            Assert.AreEqual(4, run.Shop.Offers.Count);
            foreach (var offer in run.Shop.Offers) Assert.AreEqual(0, offer.Tier);
            var original = run.Shop.Offers[0];
            typeof(RunController).GetProperty("Level").SetValue(run, 6);
            Assert.IsTrue(run.Shop.Open());
            Assert.AreSame(original, run.Shop.Offers[0], "Level increases do not silently replace stock");
            run.Shop.Credit(100);
            Assert.IsTrue(run.Shop.RefreshStock());
            Assert.AreEqual(4, run.Shop.Offers.Count);
            foreach (var offer in run.Shop.Offers) Assert.AreEqual(1, offer.Tier, "Weapons and passives use the level-6 distribution");
            yield return null;
            RuntimeSmokeTests.Capture("TestResults/23-shop-quality.png");
        }
        [Test] public void OnlyTwoMatchingWeaponsCanCombine()
        {
            var inventory = run.Player.Weapons;
            Assert.IsTrue(run.Shop.Open());
            Assert.IsFalse(run.Shop.MergeOwned(0));
            Assert.IsTrue(inventory.Add(1, 0));
            Assert.IsTrue(inventory.Add(0, 1));
            Assert.IsFalse(run.Shop.MergeOwned(0), "Kind and quality must both match");
            Assert.IsTrue(inventory.Add(0, 0));
            Assert.IsTrue(run.Shop.MergeOwned(0));
            Assert.AreEqual(3, inventory.Slots.Count);
            Assert.AreEqual(1, inventory.Slots[0].Tier);
            Assert.IsTrue(inventory.Add(0, 3));
            Assert.IsTrue(inventory.Add(0, 3));
            Assert.IsFalse(run.Shop.MergeOwned(4), "Maximum quality cannot merge");
        }

        [Test] public void PurchaseSuppliesTheSecondCopyEvenAtCapacity()
        {
            Assert.IsTrue(run.Shop.Open());
            var offer = run.Shop.Offers[0];
            offer.Weapon = 0;
            offer.Tier = 0;
            var inventory = run.Player.Weapons;
            while (inventory.Slots.Count < WeaponInventory.Capacity)
                Assert.IsTrue(inventory.Add(1, 0));
            run.Shop.Credit(run.Shop.Price(offer));
            Assert.IsFalse(run.Shop.Buy(0));
            Assert.IsTrue(run.Shop.Buy(0, true));
            Assert.AreEqual(WeaponInventory.Capacity, inventory.Slots.Count);
            Assert.AreEqual(1, inventory.Slots[0].Tier);
            Assert.AreEqual(0, run.Shop.Gold);
            Assert.IsTrue(offer.Sold);
            Assert.IsFalse(run.Shop.Buy(0, true));
        }

        [UnityTest] public IEnumerator FullSlotsMergeSaleAndFloorReset()
        {
            var inventory = run.Player.Weapons;
            while (inventory.Slots.Count < 6) Assert.IsTrue(inventory.Add(0, 0));
            Assert.IsFalse(inventory.Add(1, 0));
            run.Shop.Open();
            inventory.Slots[0].Cooldown = inventory.Interval(inventory.Slots[0]) * .7f;
            inventory.Slots[1].Cooldown = inventory.Interval(inventory.Slots[1]) * .9f;
            Assert.IsTrue(run.Shop.MergeOwned(0));
            Assert.AreEqual(5, inventory.Slots.Count);
            Assert.AreEqual(1, inventory.Slots[0].Tier);
            Assert.AreEqual(.9f, inventory.Slots[0].Cooldown / inventory.Interval(inventory.Slots[0]), .001f);
            Assert.IsTrue(run.Shop.Sell(0)); Assert.AreEqual(24, run.Shop.Gold);
            while (inventory.Slots.Count > 1) Assert.IsTrue(run.Shop.Sell(0));
            Assert.IsFalse(run.Shop.Sell(0));
            run.Shop.Credit(1000); run.Shop.RefreshStock();
            int gold = run.Shop.Gold;
            run.Shop.Close();
            run.SetState(RunState.FloorCleared);
            run.AdvanceFloor();
            Assert.AreEqual(0, run.Shop.Refreshes); Assert.AreEqual(gold, run.Shop.Gold);
            Assert.AreEqual(run.Map.ShopCounterPosition, run.Shop.Position);
            yield return null;
        }
        [Test] public void RuntimePassiveCatalogMatchesEachTier()
        {
            var expected = new[]
            {
                new[] { "硬质弹头", "轻型枪机", "厚衬外套", "软底长靴" },
                new[] { "消声套管", "扩音枪口", "承压线圈", "回声铜铃" },
                new[] { "疾行发电机", "应急修复器" },
                new[] { "遗迹扩声阵列", "寂静终焉" }
            };
            var items = run.Config.passives;
            Assert.AreEqual(12, items.Length);
            for (int tier = 0; tier < expected.Length; tier++)
                CollectionAssert.AreEquivalent(expected[tier], items.Where(i => i.tier == tier).Select(i => i.title));
        }

        [Test] public void PassiveStackAndHealthLimits()
        {
            var player = run.Player;
            var coat = run.Config.passives.Single(i => i.title == "厚衬外套");
            Assert.IsTrue(player.Passives.Add(coat));
            Assert.AreEqual(116, player.MaxHealth);
            Assert.AreEqual(116, player.Health);
            Assert.IsTrue(player.Passives.Add(coat));
            Assert.IsFalse(player.Passives.Add(coat));
            Assert.AreEqual(2, player.Passives.Count(coat));
            player.MaxHealth = 25; player.Heal(0);
            Assert.IsFalse(player.Passives.CanAdd(run.Config.passives.Single(i => i.title == "软底长靴")));
        }

        [Test] public void PurplePurchasesConvertSpeedAndRepairEverySixKills()
        {
            var player = run.Player;
            player.TakeDamage(50);
            Assert.IsTrue(run.Shop.Open());
            run.Shop.Credit(200);
            // At level one .94 selects purple for both passive slots.
            typeof(ShopController).GetField("random", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(run.Shop, new QualitySample(.94));
            Assert.IsTrue(run.Shop.RefreshStock());
            int gold = run.Shop.Gold;
            foreach (var special in new[] { ItemSpecial.SpeedDamage, ItemSpecial.Repair })
            {
                var offer = run.Shop.Offers.Single(o => o.Item?.special == special);
                Assert.IsTrue(run.Shop.Buy(run.Shop.Offers.ToList().IndexOf(offer)));
                gold -= 60;
                Assert.AreEqual(gold, run.Shop.Gold);
                Assert.AreEqual(1, player.Passives.Count(offer.Item));
                Assert.IsFalse(player.Passives.CanAdd(offer.Item));
            }
            Assert.AreEqual(62, player.Health);
            Assert.AreEqual(1.048f, player.TotalDamageScale, .001f);
            player.MoveScale += .12f;
            Assert.AreEqual(1.12f, player.TotalDamageScale, .001f);
            player.MoveScale += .5f;
            Assert.AreEqual(1.24f, player.TotalDamageScale, .001f);
            run.Shop.Close();
            var enemies = run.Enemies.Where(e => !e.IsBoss).Take(12).ToArray();
            Assert.AreEqual(12, enemies.Length);
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i].TakeDamage(99999);
                Assert.AreEqual(62 + (i + 1) / 6 * 8, player.Health, $"Kill {i + 1}");
                if (run.State == RunState.Upgrade)
                    run.ChooseUpgrade(run.Choices.FindIndex(choice => choice.Title != "应急修复"));
            }
        }
        [UnityTest] public IEnumerator WeaponVolleyRangeAndPiercing()
        {
            run.Player.GetComponent<Rigidbody2D>().position = run.Map.Rooms[0].Center;
            run.Player.transform.position = run.Map.Rooms[0].Center;
            Physics2D.SyncTransforms();
            var weapons = run.Player.Weapons;
            weapons.Remove(0); weapons.Add(1, 0); weapons.Slots[0].Cooldown = 0;
            int pulses = run.Visibility.Pulses.Count;
            Assert.IsTrue(run.Player.TryFire((Vector2)run.Player.transform.position + Vector2.up * 10, true));
            Assert.AreEqual(5, Object.FindObjectsOfType<Projectile>().Length);
            Assert.AreEqual(pulses + 1, run.Visibility.Pulses.Count, "Shotgun emits one pulse");
            yield return new WaitForSeconds(.4f);
            Assert.Greater(Object.FindObjectsOfType<Projectile>().Length, 0, "Pellets continue beyond targeting range");
            foreach (var enemy in run.Enemies) enemy.GetComponent<Rigidbody2D>().position = run.Map.Rooms[1].Center;
            Vector2 origin = run.Map.Rooms[0].Center;
            var a = run.Enemies[0]; var b = run.Enemies[1];
            a.GetComponent<Rigidbody2D>().position = origin + Vector2.right * 2;
            b.GetComponent<Rigidbody2D>().position = origin + Vector2.right * 3;
            Physics2D.SyncTransforms(); float ah = a.Health, bh = b.Health;
            Projectile.Create(run, origin, Vector2.right, 26, 10, false, 1);
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(ah - 10, a.Health, .001f); Assert.AreEqual(bh - 7.5f, b.Health, .001f);
        }
    }
}
