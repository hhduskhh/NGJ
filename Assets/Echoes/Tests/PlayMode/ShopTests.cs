using System.Collections;
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
            public override double NextDouble() => .63;
            public override int Next(int maxValue) => 0;
        }
        [UnitySetUp] public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype"); yield return null;
            run = Object.FindObjectOfType<RunController>();
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
            run.Player.GetComponent<Rigidbody2D>().position = run.Map.Rooms[0].Center;
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
                .SetValue(run.Shop, new QualitySample());
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
            run.Shop.BeginFloor(run.Map.Rooms[1].Center);
            Assert.AreEqual(0, run.Shop.Refreshes); Assert.AreEqual(gold, run.Shop.Gold);
            Assert.Less(Vector2.Distance(run.Shop.Position, run.Map.Rooms[1].Center), 3);
            yield return null;
        }
        [UnityTest] public IEnumerator PassiveLimitsConversionsAndRepair()
        {
            var player = run.Player; var items = run.Config.passives;
            Assert.IsTrue(player.Passives.Add(items[2])); Assert.AreEqual(116, player.MaxHealth); Assert.AreEqual(116, player.Health);
            Assert.IsTrue(player.Passives.Add(items[2])); Assert.IsFalse(player.Passives.Add(items[2]));
            Assert.IsTrue(player.Passives.Add(items[18]));
            Assert.AreEqual(.21f, player.Passives.ConvertedDamage, .001f);
            Assert.IsFalse(player.Passives.Add(items[18]));
            player.MaxHealth = 25; player.Heal(0);
            Assert.IsFalse(player.Passives.CanAdd(items[3]));
            player.MaxHealth = 100; player.Heal(100); player.TakeDamage(40);
            Assert.IsTrue(player.Passives.Add(items[21])); Assert.AreEqual(72, player.Health);
            for (int i = 0; i < 5; i++) player.Passives.OnKill();
            Assert.AreEqual(72, player.Health); player.Passives.OnKill(); Assert.AreEqual(80, player.Health);
            yield return null;
        }
        [UnityTest] public IEnumerator WeaponVolleyRangeAndPiercing()
        {
            var weapons = run.Player.Weapons;
            weapons.Remove(0); weapons.Add(1, 0); weapons.Slots[0].Cooldown = 0;
            int pulses = run.Visibility.Pulses.Count;
            Assert.IsTrue(run.Player.TryFire((Vector2)run.Player.transform.position + Vector2.up * 10, true));
            Assert.AreEqual(5, Object.FindObjectsOfType<Projectile>().Length);
            Assert.AreEqual(pulses + 1, run.Visibility.Pulses.Count, "Shotgun emits one pulse");
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(0, Object.FindObjectsOfType<Projectile>().Length, "Range expires before lifetime");
            foreach (var enemy in run.Enemies) enemy.GetComponent<Rigidbody2D>().position = run.Map.Rooms[4].Center;
            Vector2 origin = run.Map.Rooms[0].Center;
            var a = run.Enemies[0]; var b = run.Enemies[1];
            a.GetComponent<Rigidbody2D>().position = origin + Vector2.right * 2;
            b.GetComponent<Rigidbody2D>().position = origin + Vector2.right * 3;
            Physics2D.SyncTransforms(); float ah = a.Health, bh = b.Health;
            Projectile.Create(run, origin, Vector2.right, 26, 10, false, 10, 1);
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(ah - 10, a.Health, .001f); Assert.AreEqual(bh - 7.5f, b.Health, .001f);
        }
    }
}
