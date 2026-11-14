using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class SecondFloorRoomArtTests
    {
        private RunController run;

        [UnitySetUp]
        public IEnumerator EnterSecondFloor()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            run = Object.FindObjectOfType<RunController>();
            run.ActivateBoss(); run.Boss.TakeDamage(99999); run.AdvanceFloor();
            yield return null;
            Assert.AreEqual(2, run.Floor);
            Assert.AreEqual(2, run.Map.Floor);
        }

        [UnityTest]
        public IEnumerator NativeFactoryPropsMatchPhysicsAndKeepCounterReachable()
        {
            var entries = RoomArt.ForFloor(2, false);
            Assert.AreEqual(12, entries.Length, "Ten categories plus two long-axis variants");
            foreach (var entry in entries)
            {
                var sprite = RoomArt.Sprite(entry.Kind);
                Assert.AreEqual(entry.Canvas.x, sprite.rect.width, entry.Kind);
                Assert.AreEqual(entry.Canvas.y, sprite.rect.height, entry.Kind);
                Assert.AreEqual(16, sprite.pixelsPerUnit);
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
            }
            Physics2D.SyncTransforms();
            int props = 0;
            var used = new HashSet<string>();
            foreach (var art in run.Map.GetComponentsInChildren<ArtBinding>())
            {
                used.Add(art.Kind);
                if (!art.name.StartsWith("Obstacle_")) continue;
                props++;
                Assert.IsTrue(art.Kind.StartsWith("room_f2_") || art.Kind == StartStall.Kind, art.Kind);
                Assert.AreEqual(Vector3.one, art.transform.localScale);
                Assert.AreEqual(Quaternion.identity, art.transform.rotation, "Standing machines remain upright");
                var bounds = art.GetComponent<BoxCollider2D>().bounds;
                for (int x = Mathf.CeilToInt(bounds.min.x); x <= Mathf.FloorToInt(bounds.max.x); x++)
                    for (int y = Mathf.CeilToInt(bounds.min.y); y <= Mathf.FloorToInt(bounds.max.y); y++)
                    {
                        var tile = new Vector2Int(x, y);
                        Assert.IsTrue(run.Map.IsFloorTile(tile));
                        Assert.IsFalse(run.Map.IsWalkable(tile), art.name + " blocks a walkable tile");
                    }
            }
            Assert.Greater(props, 30);
            foreach (var entry in entries) Assert.IsTrue(used.Contains(entry.Kind), "Live factory uses " + entry.Kind);
            foreach (var room in run.Map.Rooms)
            {
                Assert.Greater(run.Map.FindPath(run.Map.StartPosition, room.Center).Count +
                    (room.Role == RoomRole.Start ? 1 : 0), 0);
                if (room.Role != RoomRole.Middle) continue;
                float coverage = run.Map.SolidCoverage(room);
                Debug.Log("ECHOES_F2_COVERAGE " + room.Shape + " " + coverage);
                Assert.That(coverage, Is.InRange(.08f, .18f), "Factory cover remains usable");
            }
            Assert.IsTrue(run.Map.IsWalkable(Vector2Int.RoundToInt(run.Shop.Position)));
            Assert.Greater(run.Map.FindPath(run.Map.StartPosition, run.Shop.Position).Count, 0);
            Move(run.Shop.Position);
            Assert.IsTrue(run.Shop.Open());
            run.Shop.Close();
            var tank = System.Array.Find(run.Map.GetComponentsInChildren<ArtBinding>(), a => a.Kind == "room_f2_tank");
            var tankCollider = tank.GetComponent<BoxCollider2D>();
            Vector2 front = new Vector2(tankCollider.bounds.center.x, tankCollider.bounds.min.y - 2);
            Assert.IsTrue(run.Map.IsWalkable(Vector2Int.RoundToInt(front)));
            Assert.IsFalse(run.Map.HasLineOfSight(front, tankCollider.bounds.center));
            Assert.AreSame(tankCollider, Physics2D.Raycast(front, Vector2.up, 3).collider);
            Projectile.Create(run, front, Vector2.up, 20, 10, false, 10);
            var shot = Object.FindObjectOfType<Projectile>();
            Assert.IsNotNull(shot);
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(!shot, "Factory receiver stops a real projectile at its solid base");
        }

        [UnityTest]
        public IEnumerator CaptureFiveFactoryShapesAndNormalVision()
        {
            foreach (var enemy in run.Enemies) enemy.enabled = false;
            Directory.CreateDirectory("TestResults/SecondFloorArt");
            var cover = run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>();
            cover.enabled = false;
            float elapsed = run.Elapsed;
            yield return new WaitForSeconds(.1f);
            Assert.Greater(run.Elapsed, elapsed);
            RuntimeSmokeTests.Capture("TestResults/SecondFloorArt/01-start.png");
            var captured = new HashSet<RoomShape>();
            foreach (var room in run.Map.Rooms)
                if (room.Role == RoomRole.Middle && captured.Add(room.Shape))
                {
                    Move(room.Center); yield return null;
                    RuntimeSmokeTests.Capture("TestResults/SecondFloorArt/02-" + room.Shape + ".png");
                }
            Move(run.Map.AltarPosition); yield return null;
            RuntimeSmokeTests.Capture("TestResults/SecondFloorArt/03-boss.png");
            cover.enabled = true;
            var boiler = System.Array.Find(run.Map.GetComponentsInChildren<ArtBinding>(), a => a.Kind == "room_f2_boiler");
            Move(run.Map.NearestFloor((Vector2)boiler.transform.position + Vector2.down * 3));
            run.Visibility.ClearPulses();
            Assert.IsTrue(run.Player.TryFire((Vector2)run.Player.transform.position + Vector2.right * 10, true));
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .02f);
            run.Visibility.RefreshMask();
            RuntimeSmokeTests.Capture("TestResults/SecondFloorArt/04-gameplay-vision.png");

            int originalSeed = run.Config.randomSeed;
            try
            {
                cover.enabled = false;
                foreach (var enemy in run.Enemies) enemy.gameObject.SetActive(false);
                for (int seed = 100; seed < 120 && captured.Count < 5; seed++)
                {
                    run.Config.randomSeed = seed;
                    run.Map.Generate(run, 1);
                    foreach (var room in run.Map.Rooms)
                        if (room.Role == RoomRole.Middle && captured.Add(room.Shape))
                        {
                            Move(room.Center); yield return null;
                            RuntimeSmokeTests.Capture("TestResults/SecondFloorArt/02-" + room.Shape + ".png");
                        }
                }
                Assert.AreEqual(5, captured.Count);
            }
            finally { run.Config.randomSeed = originalSeed; cover.enabled = true; }
        }

        private void Move(Vector2 position)
        {
            run.Player.GetComponent<Rigidbody2D>().position = position;
            run.Player.transform.position = position;
            Camera.main.transform.position = new Vector3(position.x, position.y, -10);
            Physics2D.SyncTransforms();
        }
    }
}
