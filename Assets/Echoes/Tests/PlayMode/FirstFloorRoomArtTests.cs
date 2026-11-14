using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class FirstFloorRoomArtTests
    {
        [UnityTest]
        public IEnumerator NativeArtFootprintsDoorsAndKioskAreIntegrated()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            foreach (var entry in RoomArt.ForFloor(1))
            {
                var sprite = RoomArt.Sprite(entry.Kind);
                Assert.AreEqual(entry.Canvas.x, sprite.rect.width, entry.Kind);
                Assert.AreEqual(entry.Canvas.y, sprite.rect.height, entry.Kind);
                Assert.AreEqual(16, sprite.pixelsPerUnit);
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
            }
            var used = new HashSet<string>();
            foreach (var art in run.Map.GetComponentsInChildren<ArtBinding>())
            {
                used.Add(art.Kind);
                if (!art.name.StartsWith("Obstacle_")) continue;
                Assert.AreEqual(Vector3.one, art.transform.localScale, art.name);
                Assert.IsTrue(art.Kind.StartsWith("room_") || art.Kind == StartStall.Kind);
                Assert.AreEqual(Quaternion.identity, art.transform.rotation, "Standing objects stay upright");
            }
            foreach (var kind in new[] { "room_bell", "room_crate", "room_rubble", "room_column",
                "room_pew", "room_pew_ns", "room_boss_pillar", "room_crack", "room_moss",
                "room_coins", "room_sconce", "room_lintel", "room_threshold", StartStall.Kind })
                Assert.IsTrue(used.Contains(kind), "First floor actually uses " + kind);
            Assert.AreEqual(run.Map.Doors.Count, FindNamed(run.Map, "DoorThreshold"));
            Assert.AreEqual(run.Map.Doors.Count, FindNamed(run.Map, "DoorLintel"));
            Assert.AreEqual(1, FindNamed(run.Map, "Obstacle_" + StartStall.Kind));
            Assert.IsTrue(run.Map.IsWalkable(Vector2Int.RoundToInt(run.Shop.Position)));
            Assert.Greater(run.Map.FindPath(run.Player.transform.position, run.Shop.Position).Count, 0);

            foreach (var enemy in run.Enemies) enemy.enabled = false;
            var cover = run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>();
            cover.enabled = false;
            Directory.CreateDirectory("TestResults/FirstFloorArt");
            float elapsed = run.Elapsed;
            yield return new WaitForSeconds(.1f);
            Assert.Greater(run.Elapsed, elapsed, "Capture comes from advancing gameplay");
            Capture(run, "01-start", run.Map.Rooms[0]);
            var captured = new HashSet<RoomShape>();
            foreach (var room in run.Map.Rooms)
                if (room.Role == RoomRole.Middle && captured.Add(room.Shape))
                {
                    Debug.Log("ECHOES_P1_DENSITY " + room.Shape + " " + run.Map.SolidCoverage(room));
                    Move(run, room.Center);
                    yield return null;
                    Capture(run, "02-" + room.Shape, room);
                }
            Move(run, run.Map.AltarPosition);
            yield return null;
            Capture(run, "03-boss", run.Map.Rooms[run.Map.Rooms.Count - 1]);
            cover.enabled = true;
            var bell = System.Array.Find(run.Map.GetComponentsInChildren<ArtBinding>(), a => a.Kind == "room_bell");
            Move(run, run.Map.NearestFloor((Vector2)bell.transform.position + Vector2.down * 3));
            run.Visibility.ClearPulses();
            Assert.IsTrue(run.Player.TryFire((Vector2)run.Player.transform.position + Vector2.right * 10, true));
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .02f);
            run.Visibility.RefreshMask();
            RuntimeSmokeTests.Capture("TestResults/FirstFloorArt/04-gameplay-vision.png");
            // The generator can omit any shape on a particular seed.
            if (captured.Count < 5)
            {
                int originalSeed = run.Config.randomSeed;
                cover.enabled = false;
                foreach (var enemy in run.Enemies) enemy.gameObject.SetActive(false);
                try
                {
                    for (int seed = 100; seed < 120 && captured.Count < 5; seed++)
                    {
                        run.Config.randomSeed = seed;
                        run.Map.Generate(run, 0);
                        foreach (var room in run.Map.Rooms)
                        {
                            if (room.Role != RoomRole.Middle || !captured.Add(room.Shape)) continue;
                            Move(run, room.Center);
                            yield return null;
                            Capture(run, "05-" + room.Shape, room);
                        }
                    }
                }
                finally { run.Config.randomSeed = originalSeed; cover.enabled = true; }
            }
            Assert.AreEqual(5, captured.Count, "All five first-floor room shapes have an art capture");
        }

        private static int FindNamed(DungeonMap map, string name)
        {
            int count = 0;
            foreach (var t in map.GetComponentsInChildren<Transform>()) if (t.name == name) count++;
            return count;
        }
        private static void Move(RunController run, Vector2 position)
        {
            run.Player.GetComponent<Rigidbody2D>().position = position;
            run.Player.transform.position = position;
            Camera.main.transform.position = new Vector3(position.x, position.y, -10);
            Physics2D.SyncTransforms();
        }
        private static void Capture(RunController run, string name, DungeonMap.Room room)
        {
            RuntimeSmokeTests.Capture("TestResults/FirstFloorArt/" + name + ".png");
        }
    }
}
