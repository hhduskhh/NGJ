using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class ThirdFloorRoomArtTests
    {
        private RunController run;
        private const string CaptureRoot = "TestResults/ThirdFloorArt/";

        [UnitySetUp]
        public IEnumerator EnterSanctuary()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            run = Object.FindObjectOfType<RunController>();
            for (int floor = 1; floor < 3; floor++)
            {
                var player = run.Player;
                int weaponCount = player.Weapons.Slots.Count, gold = run.Shop.Gold;
                run.ActivateBoss();
                run.Boss.TakeDamage(99999);
                Assert.AreEqual(RunState.FloorCleared, run.State);
                run.AdvanceFloor();
                yield return null;
                Assert.AreSame(player, run.Player);
                Assert.AreEqual(weaponCount, player.Weapons.Slots.Count);
                Assert.AreEqual(gold, run.Shop.Gold);
            }
            Assert.AreEqual(3, run.Floor);
            Assert.AreEqual(3, run.Map.Floor);
            Assert.AreEqual("Floor3/", GameArt.Floor);
            Directory.CreateDirectory(CaptureRoot);
        }

        [UnityTest]
        public IEnumerator TenNativeAssetsPhysicsConnectivityAndProjectileBlocking()
        {
            var entries = RoomArt.ForFloor(3, false);
            Assert.AreEqual(10, entries.Length);
            foreach (var entry in entries)
            {
                var sprite = RoomArt.Sprite(entry.Kind);
                Assert.AreEqual((Vector2)entry.Canvas, sprite.rect.size, entry.Kind);
                Assert.AreEqual(16, sprite.pixelsPerUnit);
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
                Assert.AreEqual(1, sprite.texture.mipmapCount);
                Assert.AreEqual(new Vector2(entry.Canvas.x * .5f,
                    entry.Canvas.y * (entry.Ground ? .5f : .125f)), sprite.pivot);
                var image = new Texture2D(2, 2);
                image.LoadImage(File.ReadAllBytes("Assets/Echoes/Resources/" + entry.Path + ".png"));
                Assert.IsTrue(System.Array.TrueForAll(image.GetPixels32(), c => c.a == 0 || c.a == 255));
                Object.Destroy(image);
            }
            var used = new HashSet<string>();
            Physics2D.SyncTransforms();
            foreach (var binding in run.Map.GetComponentsInChildren<ArtBinding>())
            {
                used.Add(binding.Kind);
                if (!binding.name.StartsWith("Obstacle_")) continue;
                Assert.IsTrue(binding.Kind.StartsWith("room_f3_") || binding.Kind == StartStall.Kind);
                Assert.AreEqual(Vector3.one, binding.transform.localScale);
                Assert.AreEqual(Quaternion.identity, binding.transform.rotation);
                var renderer = binding.GetComponent<SpriteRenderer>();
                Assert.AreEqual(GameArt.FootOrder(binding.transform.position.y) + 1, renderer.sortingOrder);
                var box = binding.GetComponent<BoxCollider2D>();
                var bounds = box.bounds;
                Assert.LessOrEqual(bounds.size.x, renderer.bounds.size.x + .25f, "No invisible extra collision width");
                for (int x = Mathf.CeilToInt(bounds.min.x); x <= Mathf.FloorToInt(bounds.max.x); x++)
                    for (int y = Mathf.CeilToInt(bounds.min.y); y <= Mathf.FloorToInt(bounds.max.y); y++)
                        Assert.IsFalse(run.Map.IsWalkable(new Vector2Int(x, y)), binding.Kind);
            }
            foreach (var entry in entries) Assert.IsTrue(used.Contains(entry.Kind), "Live sanctuary uses " + entry.Kind);
            Assert.AreEqual(run.Map.Doors.Count, run.Map.GetComponentsInChildren<Transform>().Count(t => t.name == "DoorThreshold"));
            Assert.AreEqual(4, run.Map.GetComponentsInChildren<ArtBinding>().Count(a => a.Kind == "room_f3_boss_pillar"));
            foreach (var room in run.Map.Rooms)
            {
                Assert.Greater(run.Map.FindPath(run.Map.StartPosition, room.Center).Count +
                    (room.Role == RoomRole.Start ? 1 : 0), 0, room.Name);
                if (room.Role != RoomRole.Middle) continue;
                float coverage = run.Map.SolidCoverage(room);
                Debug.Log("ECHOES_F3_COVERAGE " + room.Shape + " " + coverage);
                Assert.That(coverage, Is.InRange(.08f, .18f));
            }
            Move(run.Shop.Position);
            Assert.IsTrue(run.Shop.Open());
            run.Shop.Close();
            var shell = run.Map.GetComponentsInChildren<ArtBinding>().First(a => a.Kind == "room_f3_shell");
            var collider = shell.GetComponent<BoxCollider2D>();
            Vector2 front = new Vector2(collider.bounds.center.x, collider.bounds.min.y - 2);
            Assert.IsTrue(run.Map.IsWalkable(Vector2Int.RoundToInt(front)));
            Assert.IsFalse(run.Map.HasLineOfSight(front, collider.bounds.center));
            Assert.AreSame(collider, Physics2D.Raycast(front, Vector2.up, 3).collider);
            Move(front);
            Projectile.Create(run, front, Vector2.up, 20, 10, false, 10);
            var shot = Object.FindObjectOfType<Projectile>();
            Assert.IsNotNull(shot);
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(!shot, "A real projectile stops at the porcelain base");
        }

        [UnityTest]
        public IEnumerator CaptureFiveShapesStartBossNormalVisionAndVictory()
        {
            ConfigureGameView();
            foreach (var enemy in run.Enemies) enemy.enabled = false;
            var cover = run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>();
            float elapsed = run.Elapsed;
            int frames = Time.frameCount;
            yield return new WaitForSeconds(.15f);
            Assert.Greater(run.Elapsed, elapsed);
            Assert.Greater(Time.frameCount, frames + 1);
            Move(run.Shop.Position);
            yield return Capture("01-start");
            var captured = new HashSet<RoomShape>();
            cover.enabled = false;
            foreach (var room in run.Map.Rooms)
                if (room.Role == RoomRole.Middle && captured.Add(room.Shape))
                {
                    yield return CaptureRoom(room, "02-" + room.Shape);
                }
            yield return CaptureRoom(run.Map.Rooms.Last(), "03-boss");
            cover.enabled = true;
            var shrine = run.Map.GetComponentsInChildren<ArtBinding>().First(a => a.Kind == "room_f3_shrine");
            Move(run.Map.NearestFloor((Vector2)shrine.transform.position + Vector2.down * 2));
            run.Visibility.ClearPulses();
            run.Player.Weapons.Slots[0].Cooldown = 0;
            Assert.IsTrue(run.Player.TryFire((Vector2)run.Player.transform.position + Vector2.right * 10, true));
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .04f);
            run.Visibility.RefreshMask();
            yield return Capture("04-gameplay-vision");
            Move(run.Map.AltarPosition);
            yield return null; // Let the normal room tracker update before building the result UI.
            run.ActivateBoss();
            run.Boss.TakeDamage(99999);
            Assert.AreEqual(RunState.Won, run.State, "Third boss completes the same three-floor run");
            yield return new WaitForSecondsRealtime(run.Config.victoryRevealSeconds + .15f);
            yield return Capture("05-victory");
            run.SetState(RunState.Playing);

            int originalSeed = run.Config.randomSeed;
            try
            {
                // Empty layout fixtures cover a room shape absent from the default seed.
                cover.enabled = false;
                foreach (var enemy in run.Enemies) enemy.gameObject.SetActive(false);
                for (int seed = 100; seed < 120 && captured.Count < 5; seed++)
                {
                    run.Config.randomSeed = seed;
                    run.Map.Generate(run, 2);
                    yield return null;
                    foreach (var room in run.Map.Rooms)
                        if (room.Role == RoomRole.Middle && captured.Add(room.Shape))
                        {
                            yield return CaptureRoom(room, "02-" + room.Shape);
                        }
                }
                Assert.AreEqual(5, captured.Count);
            }
            finally { run.Config.randomSeed = originalSeed; cover.enabled = true; }
        }

        private IEnumerator CaptureRoom(DungeonMap.Room room, string name)
        {
            Move(room.Center);
            var camera = Camera.main;
            var follow = camera.GetComponent<CameraFollow>();
            var pixel = camera.GetComponents<Behaviour>().First(component => component.GetType().Name == "PixelPerfectCamera");
            bool wasFollowing = follow.enabled, wasPixelPerfect = pixel.enabled;
            float previousSize = camera.orthographicSize;
            try
            {
                follow.enabled = false;
                pixel.enabled = false;
                camera.orthographicSize = Mathf.Max(room.Bounds.height * .55f,
                    room.Bounds.width * .55f / (1600f / 900));
                camera.transform.position = new Vector3(room.Bounds.center.x, room.Bounds.center.y, -10);
                yield return Capture(name);
            }
            finally
            {
                camera.orthographicSize = previousSize;
                pixel.enabled = wasPixelPerfect;
                follow.enabled = wasFollowing;
            }
        }

        private void Move(Vector2 position)
        {
            run.Player.PlaceAt(position);
            Camera.main.transform.position = new Vector3(position.x, position.y, -10);
            Physics2D.SyncTransforms();
        }

        private static IEnumerator Capture(string name)
        {
            yield return null;
            if (Application.isBatchMode) RuntimeSmokeTests.Capture(CaptureRoot + name + ".png");
            else
            {
                ScreenCapture.CaptureScreenshot(CaptureRoot + name + ".png");
                yield return new WaitForEndOfFrame();
            }
        }

        private static void ConfigureGameView()
        {
#if UNITY_EDITOR
            if (Application.isBatchMode) return;
            var gameView = UnityEditor.EditorWindow.GetWindow(System.Type.GetType("UnityEditor.GameView,UnityEditor"));
            gameView.maximized = true;
            // Reuse the existing fixed 1600x900 capture setup.
            typeof(StartStallTests).GetMethod("SetCaptureResolution", System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { gameView });
            gameView.Focus();
#endif
        }
    }
}
