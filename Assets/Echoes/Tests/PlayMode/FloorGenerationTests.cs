using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class FloorGenerationTests
    {
        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedFloorHasStartBossAndReachableRooms()
        {
            yield return Load();
            var run = Object.FindObjectOfType<RunController>();
            Assert.AreEqual(1, run.Floor);
            Assert.AreEqual(1, run.Map.Floor);
            Assert.AreEqual("Floor1/", GameArt.Floor);
            Assert.GreaterOrEqual(run.Map.Rooms.Count, 10);
            Assert.LessOrEqual(run.Map.Rooms.Count, 25);

            var start = run.Map.Rooms[0];
            var boss = run.Map.Rooms[run.Map.Rooms.Count - 1];
            Assert.AreEqual(RoomRole.Start, start.Role);
            Assert.AreEqual(RoomRole.Boss, boss.Role);
            Assert.AreEqual(1, start.CellCount);
            Assert.AreEqual(1, boss.CellCount);
            Assert.AreEqual(1, start.DoorOpenings.Count);
            Assert.AreEqual(1, boss.DoorOpenings.Count);
            Assert.AreEqual(FloorTheme.StartName(1), start.Name);
            Assert.AreEqual(FloorTheme.BossName(1), boss.Name);

            var multiCell = new System.Collections.Generic.HashSet<RoomShape>();
            foreach (var room in run.Map.Rooms)
            {
                Assert.GreaterOrEqual(room.DoorOpenings.Count, 1);
                if (room.Role == RoomRole.Middle && room.Shape != RoomShape.Single) multiCell.Add(room.Shape);
                Assert.GreaterOrEqual(run.Map.SpawnCells(room.Index).Count, 1);
                foreach (var door in room.DoorOpenings)
                    Assert.IsTrue(door.width == 2 || door.height == 2);
            }
            Assert.GreaterOrEqual(multiCell.Count, 2, "The floor mixes several multi-cell room shapes");
            foreach (var room in run.Map.Rooms)
                Assert.Greater(run.Map.FindPath(run.Player.transform.position, room.Center).Count +
                    (room == start ? 1 : 0), 0, "Every room is reachable");
            foreach (var door in run.Map.Doors)
            {
                var center = Vector2Int.RoundToInt(door.Center);
                Assert.IsTrue(run.Map.IsWalkable(center), "Door centre is walkable");
            }
        }

        [UnityTest]
        public IEnumerator ManySeedsProduceConnectedWalkableFloors()
        {
            yield return Load();
            var run = Object.FindObjectOfType<RunController>();
            int originalSeed = run.Config.randomSeed;
            var mapObject = new GameObject("SeedMap");
            var map = mapObject.AddComponent<DungeonMap>();
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    run.Config.randomSeed = 100 + i;
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    map.Generate(run, i % FloorTheme.Count);
                    watch.Stop();
                    Debug.Log("ECHOES_GEN_MS seed=" + run.Config.randomSeed + " ms=" + watch.ElapsedMilliseconds);
                    Assert.Less(watch.ElapsedMilliseconds, 3000, "Floor generation stays responsive");
                    Assert.AreEqual(1, map.Rooms[0].DoorOpenings.Count);
                    Assert.AreEqual(1, map.Rooms[map.Rooms.Count - 1].DoorOpenings.Count);
                    Assert.IsTrue(map.IsWalkable(Vector2Int.RoundToInt(map.StartPosition)));
                    Assert.IsTrue(map.IsWalkable(Vector2Int.RoundToInt(map.BossPosition)));
                    Assert.IsTrue(map.IsWalkable(Vector2Int.RoundToInt(map.AltarPosition)));
                    foreach (var room in map.Rooms)
                    {
                        Assert.IsTrue(map.IsWalkable(Vector2Int.RoundToInt(room.Center)),
                            "Room centre must be on its own floor: " + room.Name);
                        Assert.Greater(map.SpawnCells(room.Index).Count, 0,
                            "Every room has at least one valid spawn cell: " + room.Name);
                        if (room != map.Rooms[0])
                            Assert.Greater(map.FindPath(map.StartPosition, room.Center).Count, 0,
                                "Every room is reachable: " + room.Name);
                    }
                }
            }
            finally
            {
                run.Config.randomSeed = originalSeed;
                Object.Destroy(mapObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedSceneRunsAtInteractiveFrameRate()
        {
            yield return Load();
            yield return new WaitForSeconds(.5f);
            int startFrames = Time.frameCount;
            float startTime = Time.realtimeSinceStartup;
            yield return new WaitForSeconds(2f);
            float fps = (Time.frameCount - startFrames) / (Time.realtimeSinceStartup - startTime);
            Debug.Log("ECHOES_FPS=" + fps.ToString("0.0"));
            Assert.Greater(fps, 30f, "The generated 90x70 scene stays interactive");
        }

        [UnityTest]
        public IEnumerator BossDefeatAdvancesThroughThreeFloorsAndPreservesRunState()
        {
            yield return Load();
            var run = Object.FindObjectOfType<RunController>();
            var player = run.Player;
            player.TakeDamage(20);
            float health = player.Health;
            run.Shop.Credit(50);
            Assert.IsTrue(run.Shop.Open());
            Assert.IsTrue(run.Shop.RefreshStock());
            int gold = run.Shop.Gold;
            Assert.Greater(run.Shop.Refreshes, 0);
            run.Shop.Close();
            Assert.IsTrue(player.Weapons.Add(0, 0));
            int weapons = player.Weapons.Slots.Count;
            int level = run.Level;
            Projectile.Create(run, player.transform.position + Vector3.right, Vector2.right, 1, 1, false);

            run.ActivateBoss();
            run.Boss.TakeDamage(99999);
            Assert.AreEqual(RunState.FloorCleared, run.State);
            int roomsFloor1 = run.Map.Rooms.Count;
            run.AdvanceFloor();
            yield return null;

            Assert.AreEqual(2, run.Floor);
            Assert.AreEqual(2, run.Map.Floor);
            Assert.AreEqual("Floor2/", GameArt.Floor);
            Assert.AreEqual(FloorTheme.Title(2), run.FloorTitle);
            Assert.AreEqual(FloorTheme.StartName(2), run.Map.Rooms[0].Name);
            Assert.AreEqual(FloorTheme.BossName(2), run.Map.Rooms[run.Map.Rooms.Count - 1].Name);
            Assert.AreEqual(Mathf.Min(player.MaxHealth, health + 30), player.Health, .01f,
                "Entry heal restores 30% of maximum health");
            Assert.AreEqual(gold, run.Shop.Gold, "Gold persists across floors");
            Assert.AreEqual(weapons, player.Weapons.Slots.Count, "Weapons persist across floors");
            Assert.AreEqual(level, run.Level);
            Assert.AreEqual(0, run.Shop.Refreshes, "Shop refresh count resets per floor");
            Assert.IsFalse(run.BossActivated);
            Assert.Less(Vector2.Distance(player.transform.position, run.Map.StartPosition), .01f);
            Assert.AreEqual(0, run.Visibility.Pulses.Count);
            Assert.AreEqual(0, Object.FindObjectsOfType<Projectile>().Length, "Old projectiles are cleared");
            Assert.AreEqual(run.Enemies.Count, Object.FindObjectsOfType<EnemyController>().Length);
            Assert.Greater(run.Enemies.Count, 1);
            Assert.AreEqual(1, run.Map.Rooms[0].DoorOpenings.Count);
            Assert.AreEqual(1, run.Map.Rooms[run.Map.Rooms.Count - 1].DoorOpenings.Count);
            Assert.Greater(roomsFloor1, 0);

            run.ActivateBoss();
            run.Boss.TakeDamage(99999);
            Assert.AreEqual(RunState.FloorCleared, run.State);
            run.AdvanceFloor();
            yield return null;
            Assert.AreEqual(3, run.Floor);
            Assert.AreEqual("Floor3/", GameArt.Floor);
            Assert.AreEqual(RunState.Playing, run.State);

            run.ActivateBoss();
            run.Boss.TakeDamage(99999);
            Assert.AreEqual(RunState.Won, run.State, "Only the third boss ends the run");
        }
    }
}
