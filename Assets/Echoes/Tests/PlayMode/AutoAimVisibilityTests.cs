using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class AutoAimVisibilityTests
    {
        private RunController run;
        private EnemyController target;
        private Vector2 origin, destination;
        private float previousStep;

        [UnitySetUp]
        public IEnumerator Load()
        {
            previousStep = Time.captureDeltaTime;
            Time.captureDeltaTime = .02f;
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            run = Object.FindObjectOfType<RunController>();
            run.Player.enabled = false;
            foreach (var trap in Object.FindObjectsOfType<RoomTrap>()) trap.enabled = false;
            foreach (var enemy in run.Enemies)
            {
                enemy.enabled = false;
                var body = enemy.GetComponent<Rigidbody2D>();
                body.velocity = Vector2.zero;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
                EnemyRoleTests.PlaceEnemy(enemy, run.Map.BossPosition);
            }
            origin = DungeonTestGeometry.FindClearLane(run, 16);
            destination = origin + Vector2.right * 12;
            run.Player.PlaceAt(origin);
            target = run.Enemies.First(e => !e.IsBoss);
            EnemyRoleTests.PlaceEnemy(target, destination);
            yield return null; // Let the real follow camera reach the firing lane.
            Assert.IsTrue(run.Visibility.InCamera(destination));
            Assert.IsTrue(run.Map.HasClearInteractionPath(origin, destination));
            run.Visibility.ClearPulses();
            Assert.IsFalse(run.Visibility.IsVisible(destination));
        }

        [TearDown]
        public void RestoreClock() => Time.captureDeltaTime = previousStep;

        [UnityTest]
        public IEnumerator ScreenLightWakesSleepersInTheViewportAtCastTime()
        {
            var door = DungeonTestGeometry.FindDoor(run);
            Vector2 source = DungeonTestGeometry.World(door.Source);
            Vector2 behindWall = DungeonTestGeometry.World(door.Around);
            run.Player.PlaceAt(source);
            EnemyRoleTests.PlaceEnemy(target, behindWall);
            EnemyRoleTests.PlaceEnemy(run.Boss, behindWall);
            var outside = run.Enemies.First(e => !e.IsBoss && e != target);
            EnemyRoleTests.PlaceEnemy(outside, source + Vector2.up * 1000);
            yield return null;

            Assert.IsTrue(run.Visibility.InCamera(behindWall));
            Assert.IsFalse(run.Map.HasLineOfSight(source, behindWall));
            Assert.IsFalse(run.Visibility.InCamera(outside.transform.position));
            run.SetState(RunState.Paused);
            Assert.IsFalse(run.Visibility.TryLightScreen());
            Assert.IsFalse(target.Awake);
            run.SetState(RunState.Playing);
            Assert.IsTrue(run.Visibility.TryLightScreen());
            Assert.AreEqual(EnemyState.Investigating, target.State);
            Assert.AreEqual(source, target.InvestigationTarget);
            Assert.IsFalse(outside.Awake);
            Assert.IsFalse(run.Boss.Awake);

            EnemyRoleTests.PlaceEnemy(outside, behindWall);
            yield return null;
            Assert.IsFalse(run.Visibility.TryLightScreen());
            Assert.IsFalse(outside.Awake);
        }

        [UnityTest]
        public IEnumerator ScreenLightRevealsViewportAndRechargesInPlayTime()
        {
            var vision = run.Visibility;
            var cover = run.transform.Find("UnseenDungeon").gameObject;
            var laugh = Resources.Load<AudioClip>("CombatAudio/ScreenLightLaugh");
            Assert.IsNotNull(laugh);
            Assert.AreEqual(4f, laugh.length, .01f);
            var voice = run.Noise.GetComponents<AudioSource>().Single(source => source.clip == laugh);
            run.SetState(RunState.Paused);
            Assert.IsFalse(vision.TryLightScreen());
            Assert.IsFalse(voice.isPlaying);
            run.SetState(RunState.Playing);
            Assert.IsTrue(vision.TryLightScreen());
            Assert.IsTrue(voice.isPlaying);
            Assert.AreEqual(15f, vision.ScreenLightCooldownRemaining, .01f);
            Assert.AreEqual(1f, vision.VisibilityAt(destination));
            Assert.AreSame(target, run.Player.FindVisibleTarget());
            Assert.IsFalse(vision.IsVisible(origin + Vector2.up * 1000));
            yield return null;
            Assert.IsFalse(cover.activeSelf);
            run.SetState(RunState.Paused);
            float remaining = vision.ScreenLightCooldownRemaining;
            yield return null;
            Assert.AreEqual(remaining, vision.ScreenLightCooldownRemaining);
            run.SetState(RunState.Playing);
            Time.captureDeltaTime = .25f;
            yield return new WaitForSeconds(4f);
            Assert.IsFalse(cover.activeSelf);
            Assert.IsTrue(vision.IsVisible(destination));
            yield return new WaitForSeconds(1.25f);
            Assert.IsTrue(cover.activeSelf);
            Assert.IsFalse(vision.IsVisible(destination));
            voice.Stop();
            Assert.IsFalse(vision.TryLightScreen());
            Assert.IsFalse(voice.isPlaying);
            yield return new WaitForSeconds(vision.ScreenLightCooldownRemaining + .25f);
            Assert.IsTrue(vision.TryLightScreen());
        }

        [UnityTest]
        public IEnumerator FurnitureBlocksTheCloserTargetButNotTheShotToTheOtherEnemy()
        {
            var near = run.Enemies.First(e => !e.IsBoss && e != target);
            origin += Vector2.right * 8;
            run.Player.PlaceAt(origin);
            EnemyRoleTests.PlaceEnemy(near, origin + Vector2.right * 4);
            EnemyRoleTests.PlaceEnemy(target, origin + Vector2.left * 5);
            // Block the shot from both the player's feet and the elevated muzzle.
            var cover = new GameObject("Cover");
            cover.transform.SetParent(run.Map.GetComponentsInChildren<BoxCollider2D>()[0].transform, true);
            cover.transform.position = origin + Vector2.right * 2;
            cover.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 2);
            Physics2D.SyncTransforms();
            run.Visibility.Reveal(origin, 9);
            yield return new WaitForSeconds(.4f);
            Assert.AreSame(target, run.Player.FindVisibleTarget());
            Assert.IsTrue(run.Player.TryFire(near.transform.position));
            yield return new WaitForSeconds(.4f);
            Assert.Less(target.Health, target.MaxHealth);
            Assert.AreEqual(near.MaxHealth, near.Health);
        }

        [UnityTest]
        public IEnumerator PistolHitsLitEnemyBeyondItsFormerTargetingRange() => VerifyLongShot(0);

        [UnityTest]
        public IEnumerator ShotgunHitsLitEnemyBeyondItsFormerTargetingRange() => VerifyLongShot(1);

        [UnityTest]
        public IEnumerator CrossbowHitsLitEnemyBeyondItsFormerTargetingRange() => VerifyLongShot(2);

        private IEnumerator VerifyLongShot(int kind)
        {
            var weapons = run.Player.Weapons;
            weapons.Remove(0);
            Assert.IsTrue(weapons.Add(kind, 0));
            var slot = weapons.Slots[0];
            Assert.Greater(Vector2.Distance(origin, destination), weapons.Definition(slot).range);
            // The enemy lies in the dim outer band, beyond even the crossbow's old limit.
            run.Visibility.Reveal(origin, 10);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .02f);
            Assert.That(run.Visibility.VisibilityAt(destination), Is.InRange(.01f, .99f));
            Vector2 manual = origin + Vector2.left * 8;
            slot.Cooldown = 0;
            weapons.Tick(0, manual);
            float health = target.Health;
            Assert.IsTrue(run.Player.TryFire(manual));
            Assert.AreEqual(target, run.Player.AimTarget);
            Assert.Greater(Vector2.Dot(slot.Visual.right, Vector2.right), .99f,
                "The actual gun must agree with the player's target even beyond its former range");
            float deadline = Time.time + 1;
            while (target.Health == health && Time.time < deadline) yield return null;
            Assert.Less(target.Health, health, "The automatically aimed projectile must reach the distant enemy");
        }

        [UnityTest]
        public IEnumerator AllSixWeaponsAimAtAnEnemyLitOnlyByABulletTrail()
        {
            var weapons = run.Player.Weapons;
            while (weapons.Slots.Count < WeaponInventory.Capacity)
                Assert.IsTrue(weapons.Add(weapons.Slots.Count % 3, 0));
            var trail = run.Visibility.BeginTrail(destination - Vector2.right * 2, Vector2.right, 22);
            trail.Advance(destination);
            Assert.AreEqual(0, run.Visibility.Pulses.Count);
            Assert.IsTrue(run.Visibility.IsVisible(destination));
            Vector2 manual = origin + Vector2.left * 8;
            weapons.Tick(0, manual);
            foreach (var slot in weapons.Slots)
            {
                slot.Cooldown = 0;
                Assert.Greater(Vector2.Dot(slot.Visual.right, Vector2.right), .98f,
                    "Every weapon must aim at the trail-lit target regardless of its old range");
            }
            Assert.IsTrue(run.Player.TryFire(manual));
            Assert.AreEqual(target, run.Player.AimTarget);
            var shots = Object.FindObjectsOfType<Projectile>().ToDictionary(p => p, p => p.transform.position);
            Assert.AreEqual(14, shots.Count); // Two pistols, two crossbows and two five-pellet shotguns.
            yield return null;
            foreach (var shot in shots)
            {
                Assert.IsTrue(shot.Key);
                Assert.Greater(shot.Key.transform.position.x, shot.Value.x,
                    "Every projectile must travel toward the lit target, away from the mouse");
            }
        }
    }
}
