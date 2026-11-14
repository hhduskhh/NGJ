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
        public IEnumerator DistantTorsoHitDealsDamageAndTracksMovingPlayer()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            var bounds = run.Map.Rooms[0].Bounds;
            var target = run.Enemies[0];
            // Find a clear horizontal firing lane in the starting room.
            Vector2 start = Vector2.zero, feet = Vector2.zero;
            for (int y = bounds.yMin + 3; y < bounds.yMax - 3 && start == Vector2.zero; y++)
                for (int x = bounds.xMin + 3; x + 20 < bounds.xMax - 2; x++)
                {
                    var source = new Vector2(x, y);
                    var end = source + Vector2.right * 20;
                    if (!run.Map.HasClearInteractionPath(source, end) ||
                        !run.Map.HasClearInteractionPath(source + Vector2.up, end + Vector2.up)) continue;
                    start = source;
                    feet = end;
                    break;
                }
            Assert.AreNotEqual(Vector2.zero, start);
            run.Player.PlaceAt(start);
            target.transform.position = feet;
            target.GetComponent<Rigidbody2D>().position = feet;
            Physics2D.SyncTransforms();
            float health = target.Health;
            Assert.AreEqual(EnemyState.Sleeping, target.State);
            Assert.IsTrue(run.Player.TryFire(feet + Vector2.up * .8f, true));
            float deadline = Time.time + 2;
            while (target.Health == health && Time.time < deadline) yield return null;
            Assert.Less(target.Health, health, "A long shot through the torso must register");
            Assert.AreEqual(EnemyState.Chasing, target.State);
            run.Player.PlaceAt(start + Vector2.right);
            target.Hear(feet);
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(EnemyState.Chasing, target.State);
            Assert.Less(Vector2.Distance(target.LastSeenPosition, run.Player.transform.position), .1f);
            Assert.Less(Vector2.Distance(target.transform.position, run.Player.transform.position), 18.5f);
        }

        [UnityTest]
        public IEnumerator SlowShotLightsItsPathUntilWallImpactAndDarknessReturns()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            var room = run.Map.Rooms[0];
            Vector2 origin = new Vector2(room.Bounds.xMin + 6, room.Bounds.yMin + 7);
            Vector2 sample = origin + Vector2.left * 3;
            Assert.IsFalse(run.Visibility.IsVisible(sample));
            Projectile.Create(run, origin, Vector2.left, 2, 1, false);
            var shot = Object.FindObjectOfType<Projectile>();
            yield return new WaitForSeconds(1.6f);
            Assert.IsTrue(run.Visibility.IsVisible(sample), "A traversed point is illuminated");
            yield return new WaitForSeconds(.45f);
            Assert.IsTrue(shot, "An unobstructed shot outlives the former timeout");
            yield return new WaitForSeconds(1.4f);
            Assert.IsFalse(shot, "The boundary wall stops the shot");
            Assert.IsFalse(run.Visibility.IsVisible(sample), "The path returns to darkness");
        }

        [UnityTest]
        public IEnumerator DoorTraversalAndProjectileCollision()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            var geometry = DungeonTestGeometry.FindDoor(run);
            var enemy = run.Enemies[0];
            var park = run.Map.Rooms[run.Map.Rooms.Count - 1].Center;
            // Isolate one existing monster; never spawn test-only gameplay actors.
            foreach (var other in run.Enemies)
                if (other != enemy)
                {
                    other.enabled = false;
                    other.GetComponent<Rigidbody2D>().position = park;
                }
            enemy.enabled = true;
            enemy.GetComponent<Rigidbody2D>().position = DungeonTestGeometry.World(geometry.Source);
            Physics2D.SyncTransforms();
            Vector2 target = DungeonTestGeometry.World(geometry.Through) +
                (geometry.Vertical ? Vector2.right * 3 : Vector2.up * 3);
            enemy.Hear(target);
            // A slow rendered frame contains several physics steps. Movement
            // must still reach each waypoint without oscillating across it.
            float previousStep = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = .12f;
                yield return new WaitForSeconds(6f);
            }
            finally { Time.captureDeltaTime = previousStep; }
            if (geometry.Vertical)
                Assert.Greater(enemy.transform.position.x, geometry.Door.Opening.x + 1,
                    "Awakened enemy crosses the open room doorway");
            else
                Assert.Greater(enemy.transform.position.y, geometry.Door.Opening.y + 2,
                    "Awakened enemy crosses the open room doorway");

            enemy.enabled = false;
            var body = enemy.GetComponent<Rigidbody2D>();
            body.velocity = Vector2.zero;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.position = DungeonTestGeometry.World(geometry.Through);
            enemy.transform.position = body.position;
            Physics2D.SyncTransforms();
            float health = enemy.Health;
            Vector2 direction = (DungeonTestGeometry.World(geometry.Through) -
                DungeonTestGeometry.World(geometry.Source)).normalized;
            Projectile.Create(run, DungeonTestGeometry.World(geometry.Source), direction, 30, 5, false);
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(health - 5, enemy.Health, .001f, "Actual projectile damages an enemy through the doorway");

            enemy.GetComponent<Rigidbody2D>().position = DungeonTestGeometry.World(geometry.Around);
            Physics2D.SyncTransforms();
            health = enemy.Health;
            direction = (DungeonTestGeometry.World(geometry.Around) -
                DungeonTestGeometry.World(geometry.Source)).normalized;
            Projectile.Create(run, DungeonTestGeometry.World(geometry.Source), direction, 30, 5, false);
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
            Assert.GreaterOrEqual(run.Map.Rooms.Count, 10);
            Assert.LessOrEqual(run.Map.Rooms.Count, 25);
            int initialCount = run.Enemies.Count;
            Assert.Greater(initialCount, 1);
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
            Assert.AreEqual(EnemyState.Chasing, enemy.State, "A distant hit starts pursuit");
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
            Assert.AreEqual(RunState.FloorCleared, run.State, "Floor 1 opens the descent instead of ending the run");
            Assert.IsTrue(run.Enemies.Exists(e => e.Alive && !e.IsBoss), "Clearing a floor does not require killing every normal enemy");
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
