using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class RuntimeSmokeTests
    {
        [UnityTest]
        public IEnumerator DoorTraversalAndProjectileCollision()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            var enemy = run.Enemies[0];
            // Isolate one existing monster; never spawn test-only gameplay actors.
            foreach (var other in run.Enemies)
                if (other != enemy) other.GetComponent<Rigidbody2D>().position = run.Map.Rooms[4].Center;
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(16, 6);
            Physics2D.SyncTransforms();
            enemy.Hear(new Vector2(20, 6));
            yield return new WaitForSeconds(3.2f);
            Assert.Greater(enemy.transform.position.x, 18, "Awakened enemy crosses the open room doorway");
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(26, 6);
            Physics2D.SyncTransforms();
            float health = enemy.Health;
            Projectile.Create(run, new Vector2(24, 6), Vector2.right, 22, 5, false);
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(health - 5, enemy.Health, .001f, "Actual projectile damages an enemy");
            enemy.GetComponent<Rigidbody2D>().position = new Vector2(19, 3);
            Physics2D.SyncTransforms();
            health = enemy.Health;
            Projectile.Create(run, new Vector2(15, 3), Vector2.right, 22, 5, false);
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(health, enemy.Health, "Wall blocks a projectile between rooms");
        }

        [UnityTest]
        public IEnumerator FloorSoundProgressionAndVictory()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            Assert.IsNotNull(run.Player);
            Assert.AreEqual(7, run.Map.Rooms.Count);
            int initialCount = run.Enemies.Count;
            Assert.AreEqual(run.Config.enemiesPerRoom * 5 + 1, initialCount);
            foreach (var room in run.Map.Rooms)
                Assert.Greater(run.Map.FindPath(run.Player.transform.position, room.Center).Count +
                    (room == run.Map.Rooms[0] ? 1 : 0), 0, "Every room must be reachable");
            var enemy = run.Enemies[0];
            Assert.AreEqual(EnemyState.Sleeping, enemy.State);
            run.Noise.Emit((Vector2)enemy.transform.position + Vector2.up * 1.5f, 2, NoiseKind.Gunshot);
            yield return new WaitForSeconds(.25f);
            Assert.IsTrue(enemy.Awake);
            Assert.AreEqual(EnemyState.Investigating, enemy.State);
            Assert.AreEqual(initialCount, run.Enemies.Count, "Sound cannot spawn enemies");
            yield return new WaitForSeconds(1);
            Assert.IsTrue(enemy.Awake, "No automatic return to sleep");
            var source = run.Map.Rooms[3].Center;
            enemy.Hear(source);
            Assert.Less(Vector2.Distance(enemy.InvestigationTarget, run.Map.NearestFloor(source)), .01f);
            Assert.Greater(run.Map.FindPath(enemy.transform.position, source).Count, 10);
            enemy.TakeDamage(1);
            Assert.AreEqual(EnemyState.Investigating, enemy.State, "A distant hit reveals a source, not a live player target");
            Assert.AreEqual(run.Config.enemyHealth - 1, enemy.Health);
            var before = run.Boss.Health;
            run.Boss.TakeDamage(99999);
            Assert.AreEqual(before, run.Boss.Health, "Boss must be deliberately activated");
            run.RefreshPressure();
            Assert.AreEqual(0, run.NearbyEnemies, "Distant awakened enemies give no bonus");
            for (int i = 0; i < run.Config.firstLevelExperience; i++) run.Enemies[i].TakeDamage(99999);
            Assert.AreEqual(RunState.Upgrade, run.State);
            Assert.AreEqual(0, Time.timeScale);
            Assert.AreEqual(3, run.Choices.Count);
            run.ChooseUpgrade(0);
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual(initialCount, run.Enemies.Count);
            foreach (var choice in UpgradeDefinition.Catalog)
            {
                float noise = run.Player.NoiseRadius;
                choice.Apply(run.Player);
                if (!choice.IncreasesNoise) Assert.AreEqual(noise, run.Player.NoiseRadius);
                else Assert.Greater(run.Player.NoiseRadius, noise);
            }
            run.ActivateBoss();
            Assert.IsTrue(run.Boss.Awake);
            Assert.AreEqual(initialCount, run.Enemies.Count);
            run.Boss.TakeDamage(99999);
            Assert.AreEqual(RunState.Won, run.State);
            Assert.IsTrue(run.Enemies.Exists(e => e.Alive && !e.IsBoss), "Winning does not require clearing the floor");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeAdvancesAndCapturesView()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            float time = run.Elapsed;
            yield return new WaitForSeconds(.5f);
            Assert.Greater(run.Elapsed, time, "Simulation must advance beyond the first frame");
            Directory.CreateDirectory("TestResults");
            Capture("TestResults/01-exploration.png");
            yield return null;
            run.Player.transform.position = run.Map.Rooms[1].Center;
            foreach (var enemy in run.Enemies)
                if (enemy && !enemy.IsBoss && Vector2.Distance(enemy.transform.position, run.Player.transform.position) < 7)
                    enemy.Hear(run.Player.transform.position);
            run.Noise.Emit(run.Player.transform.position, run.Player.NoiseRadius, NoiseKind.Gunshot);
            yield return new WaitForSeconds(.15f);
            Capture("TestResults/02-combat.png");
            yield return null;
            for (int i = 0; i < run.Config.firstLevelExperience; i++) run.Enemies[i].TakeDamage(99999);
            yield return null;
            Capture("TestResults/03-upgrade.png");
            yield return null;
            run.ChooseUpgrade(0);
            run.Player.GetComponent<Rigidbody2D>().position = run.Map.Rooms[0].Center;
            yield return new WaitForSeconds(run.Config.playerInvulnerability + .1f);
            run.Player.TakeDamage(99999);
            Assert.AreEqual(RunState.Lost, run.State);
            yield return null;
        }

        internal static void Capture(string path)
        {
            // Batch mode has no Game View backbuffer. Render the same camera and HUD to an explicit target.
            var camera = Camera.main;
            var canvas = Object.FindObjectOfType<Canvas>();
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            int previousSortingOrder = canvas.sortingOrder;
            var target = new RenderTexture(1600, 900, 24);
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 1000;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = previousSortingOrder;
            Object.Destroy(target); Object.Destroy(texture);
        }
    }
}
