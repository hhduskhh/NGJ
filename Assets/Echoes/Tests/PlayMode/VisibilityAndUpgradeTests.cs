using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class VisibilityAndUpgradeTests
    {
        private IEnumerator LoadFloor()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            foreach (var enemy in run.Enemies)
                enemy.GetComponent<Rigidbody2D>().position = run.Map.Rooms[1].Center;
        }

        [UnityTest]
        public IEnumerator EightUpgradesApplyToCombatAndLoot()
        {
            yield return LoadFloor();
            var run = Object.FindObjectOfType<RunController>();
            var player = run.Player;
            var slot = player.Weapons.Slots[0];
            float startInterval = player.Weapons.Interval(slot);
            Assert.AreEqual(8, UpgradeDefinition.Catalog.Length);
            void Apply(string title) => System.Array.Find(UpgradeDefinition.Catalog, card => card.Title == title).Apply(player);

            foreach (var card in UpgradeDefinition.Catalog)
            {
                Assert.IsNotNull(PresentationArt.Current.Upgrade(card.Title), card.Title);
                card.Apply(player);
            }
            Assert.AreEqual(startInterval * .88f, player.Weapons.Interval(slot), .0001f);
            Assert.AreEqual(run.Config.decoyCooldown - 2, player.DecoyCooldown, .001f);
            Assert.AreEqual(run.Config.gunNoiseRadius + 5, player.NoiseRadius, .001f);
            Assert.AreEqual(run.Config.bonusPerEnemy + .01f, player.PressureBonus, .001f);
            Assert.AreEqual(2.08f, player.CriticalMultiplier, .001f);
            Assert.AreEqual(.08f, player.DoubleLootChance, .001f);
            for (int i = 0; i < 30; i++)
            {
                Apply("快装枪机");
                Apply("诱饵调校");
                Apply("幸运四叶草");
            }
            Assert.AreEqual(.05f, player.Weapons.Interval(slot), .0001f);
            Assert.AreEqual(4, player.DecoyCooldown);
            Assert.AreEqual(1, player.DoubleLootChance);
            var enemy = run.Enemies[0];
            enemy.TakeDamage(99999);
            var coin = Object.FindObjectOfType<GoldPickup>();
            Assert.IsNotNull(coin);
            player.PlaceAt(coin.transform.position);
            yield return new WaitForSeconds(.1f);
            Assert.AreEqual(2 * run.Config.goldPerKill, run.Shop.Gold);
            Assert.AreEqual(run.Config.experiencePerKill, run.Experience);
        }

        [UnityTest]
        public IEnumerator ShotsUseVisibleTargetsAndRespectManualOverride()
        {
            yield return LoadFloor();
            var run = Object.FindObjectOfType<RunController>();
            Vector2 player = run.Player.transform.position;
            var target = run.Enemies[0];
            var farther = run.Enemies[1];
            target.GetComponent<Rigidbody2D>().position = player + Vector2.right * 2;
            farther.GetComponent<Rigidbody2D>().position = player + Vector2.up * 3;
            Physics2D.SyncTransforms();
            run.Visibility.Reveal(player, run.Config.playerVisionRadius);
            yield return new WaitForSeconds(run.Config.visionExpandDuration);
            Assert.IsTrue(run.Player.TryFire(player + Vector2.left * 5));
            Assert.AreEqual(target, run.Player.AimTarget);
            Assert.Greater(Vector2.Dot(Vector2.right, run.Player.AimDirection), .99f);
            yield return new WaitForSeconds(.15f);
            Assert.Less(target.Health, target.MaxHealth, "An actual automatic shot reaches the target");
            yield return new WaitForSeconds(run.Player.Weapons.Interval(run.Player.Weapons.Slots[0]));
            Assert.IsTrue(run.Player.TryFire(player + Vector2.down * 5, true));
            Assert.IsNull(run.Player.AimTarget, "A skill can explicitly use its supplied direction");
            Assert.Less(run.Player.AimDirection.y, -.99f);
            target.TakeDamage(99999);
            farther.GetComponent<Rigidbody2D>().position = run.Map.Rooms[1].Center;
            yield return new WaitForSeconds(run.Player.Weapons.Interval(run.Player.Weapons.Slots[0]));
            Assert.IsTrue(run.Player.TryFire(player + Vector2.left * 5));
            Assert.IsNull(run.Player.AimTarget, "No visible target falls back to the mouse direction");
            Assert.Less(run.Player.AimDirection.x, -.99f);
        }

        [UnityTest]
        public IEnumerator DarknessRevealsWithSoundAndThenExpires()
        {
            yield return LoadFloor();
            var run = Object.FindObjectOfType<RunController>();
            run.Player.enabled = false;
            Vector2 player = run.Player.transform.position;
            // Keep the probe inside the 1x1 start room, outside the stationary gradient.
            Vector2 hidden = player + Vector2.right * 4;
            var target = run.Enemies[0];
            target.GetComponent<Rigidbody2D>().position = hidden;
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.04f);
            Assert.IsFalse(run.Visibility.IsVisible(hidden));
            run.Player.Weapons.Slots[0].Cooldown = 0;
            Assert.IsTrue(run.Player.TryFire(player + Vector2.down * 6));
            Assert.IsNull(run.Player.AimTarget, "An unrevealed enemy must not attract automatic fire");
            // An unactivated boss and enemies behind the room walls must never become targets.
            run.Boss.GetComponent<Rigidbody2D>().position = run.Map.BossPosition;
            run.Enemies[1].GetComponent<Rigidbody2D>().position = new Vector2(-1, player.y);
            run.Visibility.Reveal(player, 20);
            yield return new WaitForSeconds(run.Player.Weapons.Interval(run.Player.Weapons.Slots[0]) + .02f);
            Assert.IsTrue(run.Visibility.IsVisible(hidden));
            run.Player.Weapons.Slots[0].Cooldown = 0;
            Assert.IsTrue(run.Player.TryFire(player + Vector2.down * 6));
            Assert.AreEqual(target, run.Player.AimTarget, "A sonar-revealed enemy can be auto-targeted");
            // Use a separate, empty region to avoid subsequent gunshots extending its visibility.
            Vector2 probe = player + Vector2.right * 4;
            float duration = run.Config.VisionPulseDuration;
            yield return new WaitForSeconds(duration + .15f);
            Assert.IsFalse(run.Visibility.IsVisible(probe));
            run.Noise.Emit(player, 7, NoiseKind.Decoy);
            Assert.IsFalse(run.Visibility.IsVisible(probe), "The wave must arrive before revealing the floor");
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .05f);
            Assert.IsTrue(run.Visibility.IsVisible(probe));
            run.Visibility.RefreshMask();
            Directory.CreateDirectory("TestResults");
            RuntimeSmokeTests.Capture("TestResults/04-sonar-visibility.png");
            yield return new WaitForSeconds(run.Config.visionHoldDuration + run.Config.visionShrinkDuration + .1f);
            Assert.IsFalse(run.Visibility.IsVisible(probe));
            RuntimeSmokeTests.Capture("TestResults/05-darkness.png");
            var screenshot = new Texture2D(2, 2);
            screenshot.LoadImage(File.ReadAllBytes("TestResults/05-darkness.png"));
            Color edge = screenshot.GetPixel(screenshot.width - 25, screenshot.height / 2);
            Object.Destroy(screenshot);
            Assert.Less(edge.maxColorComponent, .01f, "Unrevealed dungeon stays black at the viewport edge");
        }

        [UnityTest]
        public IEnumerator FullExperiencePausesUntilCardSelectionAndPreservesOverflow()
        {
            yield return LoadFloor();
            var run = Object.FindObjectOfType<RunController>();
            int cost = run.RequiredExperience;
            int level = run.Level;
            Projectile.Create(run, run.Player.transform.position + Vector3.up, Vector2.up, 1, 1, false);
            var projectile = Object.FindObjectOfType<Projectile>();
            for (int i = 0; i < cost; i++) run.Enemies[i].TakeDamage(99999);
            Assert.AreEqual(RunState.Upgrade, run.State);
            Assert.AreEqual(cost, run.Experience, "XP stays full until a card is chosen");
            Assert.AreEqual(level, run.Level);
            Assert.AreEqual(0, Time.timeScale);
            float elapsed = run.Elapsed;
            Vector3 bulletPosition = projectile.transform.position;
            Vector3 playerPosition = run.Player.transform.position;
            run.Noise.Emit(run.Player.transform.position, 7, NoiseKind.Gunshot);
            Vector2 notYetReached = (Vector2)playerPosition + Vector2.up * 6;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.AreEqual(elapsed, run.Elapsed);
            Assert.AreEqual(bulletPosition, projectile.transform.position);
            Assert.AreEqual(playerPosition, run.Player.transform.position);
            Assert.IsFalse(run.Visibility.IsVisible(notYetReached), "Sonar expansion also pauses");
            Assert.IsFalse(run.Player.TryFire(Vector2.zero));
            run.SetState(RunState.Paused);
            Assert.AreEqual(RunState.Upgrade, run.State, "An ordinary pause request cannot dismiss an upgrade");
            run.Enemies[cost].TakeDamage(99999);
            yield return null;
            RuntimeSmokeTests.Capture("TestResults/06-upgrade-paused.png");
            var button = Object.FindObjectsOfType<UnityEngine.UI.Button>()[0];
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual(level + 1, run.Level);
            Assert.AreEqual(1, run.Experience, "Overflow XP survives selection");
            yield return new WaitForSeconds(.1f);
            Assert.Greater(run.Elapsed, elapsed);
        }
    }
}
