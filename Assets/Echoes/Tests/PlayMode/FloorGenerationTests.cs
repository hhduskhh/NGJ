using System.Collections;
using System.Linq;
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
            Assert.AreEqual(36, DungeonMap.CellWidth);
            Assert.AreEqual(36, DungeonMap.CellHeight);
            Assert.AreEqual(3, run.Map.GridSize);
            Assert.AreEqual(3 * DungeonMap.CellWidth, run.Map.Width);
            Assert.AreEqual(3 * DungeonMap.CellHeight, run.Map.Height);
            Assert.AreEqual("Floor1/", GameArt.Floor);
            Assert.GreaterOrEqual(run.Map.Rooms.Count, 4);
            Assert.LessOrEqual(run.Map.Rooms.Count, 9);

            var start = run.Map.Rooms[0];
            var boss = run.Map.Rooms[run.Map.Rooms.Count - 1];
            Assert.AreEqual(RoomRole.Start, start.Role);
            Assert.AreEqual(RoomRole.Boss, boss.Role);
            Assert.AreEqual(1, start.CellCount);
            Assert.AreEqual(1, boss.CellCount);
            Assert.AreEqual(1, start.DoorOpenings.Count);
            Assert.AreEqual(1, boss.DoorOpenings.Count);
            Assert.AreSame(boss, run.Map.RoomAtPosition(run.Map.AltarPosition));
            Assert.LessOrEqual(Vector2.Distance(run.Map.AltarPosition, boss.DoorOpenings[0].center), run.Config.altarEntranceDistance + 1);
            Assert.AreEqual(FloorTheme.StartName(1), start.Name);
            Assert.AreEqual(FloorTheme.BossName(1), boss.Name);

            var multiCell = new System.Collections.Generic.HashSet<RoomShape>();
            foreach (var room in run.Map.Rooms)
            {
                Assert.GreaterOrEqual(room.DoorOpenings.Count, 1);
                if (room.Role == RoomRole.Middle && room.Shape != RoomShape.Single) multiCell.Add(room.Shape);
                if (room.Role == RoomRole.Middle)
                {
                    Assert.GreaterOrEqual(run.Map.SolidCount(room), 6, "Middle room needs cover");
                    Assert.GreaterOrEqual(run.Map.SolidCoverage(room), .05f, "Middle room cover coverage");
                    Assert.LessOrEqual(run.Map.SolidCoverage(room), .22f, "Middle room is not overfilled");
                }
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
        public IEnumerator MinimapRevealsRoomFootprintsAndRealDoorsOnEntry()
        {
            yield return Load();
            var run = Object.FindObjectOfType<RunController>();
            var map = run.Map;
            var room = map.Rooms.Find(r => r.CellCount > 1);
            var texture = (Texture2D)GameObject.Find("GameHUD/MiniMap/RoomMap")
                .GetComponent<UnityEngine.UI.RawImage>().texture;
            foreach (var cell in room.Cells)
                Assert.AreEqual(0f, texture.GetPixel((cell.x * DungeonMap.CellWidth) + DungeonMap.CellWidth / 2, run.Map.CellOriginY(cell.y) + DungeonMap.CellHeight / 2).a);
            run.Player.PlaceAt(room.Center);
            yield return null;
            yield return null;
            Assert.IsTrue(room.Visited);
            foreach (var cell in room.Cells)
                Assert.AreEqual(1f, texture.GetPixel((cell.x * DungeonMap.CellWidth) + DungeonMap.CellWidth / 2, run.Map.CellOriginY(cell.y) + DungeonMap.CellHeight / 2).a);
            foreach (var door in room.DoorOpenings)
                Assert.Greater(texture.GetPixel(door.x, door.y).r, .7f);
        }

        [UnityTest]
        public IEnumerator MinimapTracksMovementAndDistinguishesFrontierDoors()
        {
            yield return Load();
            var run = Object.FindObjectOfType<RunController>();
            var image = GameObject.Find("GameHUD/MiniMap/RoomMap").GetComponent<UnityEngine.UI.RawImage>();
            var marker = (RectTransform)image.transform.Find("PlayerMarker");
            var texture = (Texture2D)image.texture;
            var start = run.Map.Rooms[0];
            var door = run.Map.Doors.Find(d => d.A == start || d.B == start);
            var center = Vector2Int.FloorToInt(door.Center);
            var direction = door.Opening.width == 2 ? Vector2Int.right : Vector2Int.up;
            if (run.Map.RoomAtPosition(center + direction * 2) == start) direction = -direction;
            var tip = center + direction * 6;
            Assert.AreEqual((Color32)run.Config.minimapFrontierDoor, (Color32)texture.GetPixel(tip.x, tip.y));

            Vector2 near = center - direction * 3;
            run.Player.PlaceAt(near);
            yield return null;
            yield return null;
            Vector2 before = marker.anchoredPosition;
            run.Player.PlaceAt(near + (Vector2)direction * .5f);
            yield return null;
            yield return null;
            float scale = image.rectTransform.rect.width / run.Map.Width;
            Assert.Less(Vector2.Distance(marker.anchoredPosition - before, (Vector2)direction * (.5f * scale)), .02f);
            Assert.Less(Vector2.Distance(marker.anchoredPosition, ((Vector2)run.Player.transform.position + Vector2.one * .5f) * scale), .02f);

            run.Player.PlaceAt(center + direction * 3);
            yield return null;
            yield return null;
            Assert.AreEqual((Color32)run.Config.minimapExploredDoor, (Color32)texture.GetPixel(center.x, center.y));
            Assert.AreNotEqual((Color32)run.Config.minimapFrontierDoor, (Color32)texture.GetPixel(tip.x, tip.y));
            run.Player.PlaceAt(near);
            yield return null;
            yield return null;
            Assert.AreEqual((Color32)run.Config.minimapExploredDoor, (Color32)texture.GetPixel(center.x, center.y));
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
                    Assert.AreEqual(map.Floor == 1 ? RoomRole.Middle : RoomRole.Start,
                        map.RoomAtPosition(map.ShopCounterPosition).Role);
                    Assert.Greater(map.FindPath(map.StartPosition, map.ShopCounterPosition).Count, 0);
                    Assert.AreEqual(RoomRole.Start, map.RoomAtPosition(map.SignPosition).Role);
                    foreach (var room in map.Rooms)
                    {
                        Assert.IsTrue(map.IsWalkable(Vector2Int.RoundToInt(room.Center)),
                            "Room centre must be on its own floor: " + room.Name);
                        Assert.Greater(map.SpawnCells(room.Index).Count, 0,
                            "Every room has at least one valid spawn cell: " + room.Name);
                        if (room.Role == RoomRole.Middle)
                        {
                            Assert.GreaterOrEqual(map.SolidCount(room), 5,
                                "Middle room needs cover: " + room.Name);
                            Assert.GreaterOrEqual(map.SolidCoverage(room), .04f,
                                "Middle room cover coverage: " + room.Name);
                        }
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
        public IEnumerator BossBanksRemainingGoldAndRestartChangesSeed()
        {
            yield return Load();
            var run = Object.FindObjectOfType<RunController>();
            int seed = run.Config.randomSeed;
            GoldPickup.Drop(run, run.Map.BossPosition, 12);
            GoldPickup.Drop(run, run.Player.transform.position, 6);
            yield return null;
            run.ActivateBoss();
            run.Boss.TakeDamage(run.Boss.Health);
            Assert.AreEqual(18, run.Shop.Gold);
            run.AdvanceFloor();
            yield return null;
            Assert.AreEqual(18, run.Shop.Gold);
            run.Restart();
            yield return null;
            yield return null;
            run = Object.FindObjectOfType<RunController>();
            Assert.AreNotEqual(seed, run.Config.randomSeed);
            Assert.AreEqual(1, run.Floor);
            Assert.AreEqual(0, run.Shop.Gold);
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
            run.Player.PlaceAt(run.Shop.Position);
            Assert.IsTrue(run.Shop.Open());
            Assert.IsTrue(run.Shop.RefreshStock());
            int gold = run.Shop.Gold;
            Assert.Greater(run.Shop.Refreshes, 0);
            run.Shop.Close();
            Assert.IsTrue(player.Weapons.Add(0, 0));
            int weapons = player.Weapons.Slots.Count;
            int level = run.Level;
            Projectile.Create(run, player.transform.position + Vector3.right, Vector2.right, 1, 1, false);

            var bossBar = GameObject.Find("GameHUD").transform.Find("Bar").gameObject;
            Assert.IsFalse(bossBar.activeInHierarchy);
            run.ActivateBoss();
            yield return null;
            Assert.IsTrue(bossBar.activeInHierarchy);
            run.Boss.TakeDamage(99999);
            Assert.AreEqual(RunState.FloorCleared, run.State);
            int roomsFloor1 = run.Map.Rooms.Count;
            run.AdvanceFloor();
            yield return null;

            Assert.AreEqual(2, run.Floor);
            Assert.IsFalse(bossBar.activeInHierarchy);
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
            yield return null;
            Assert.IsTrue(bossBar.activeInHierarchy);
            run.Boss.TakeDamage(99999);
            Assert.AreEqual(RunState.FloorCleared, run.State);
            run.AdvanceFloor();
            yield return null;
            Assert.AreEqual(3, run.Floor);
            Assert.IsFalse(bossBar.activeInHierarchy);
            Assert.AreEqual("Floor3/", GameArt.Floor);
            Assert.AreEqual(RunState.Playing, run.State);

            run.ActivateBoss();
            run.Boss.TakeDamage(99999);
            Assert.AreEqual(RunState.Won, run.State, "Only the third boss ends the run");
        }

        [UnityTest]
        public IEnumerator MiddleRoomsUsePerFloorWeightedSpawnDraws()
        {
            yield return Load();
            var run = Object.FindObjectOfType<RunController>();
            Assert.AreEqual(1, run.Floor);
            for (int floor = 1; floor <= 3; floor++)
            {
                int index = floor - 1;
                int draws = run.Config.spawnCellDraws[index];
                int min = run.Config.spawnAmountMin[index];
                int max = run.Config.spawnAmountMax[index];
                var normal = run.Enemies.Where(e => !e.IsBoss).ToArray();
                Assert.GreaterOrEqual(normal.Length, draws * min,
                    "Every spawn draw adds at least its minimum amount on floor " + floor);
                Assert.LessOrEqual(normal.Length, draws * max,
                    "Spawn draws stay within the configured maximum on floor " + floor);
                Assert.AreEqual(1, run.Enemies.Count(e => e.IsBoss), "Each floor has one boss");
                foreach (var enemy in normal)
                {
                    var room = run.Map.RoomAtPosition(enemy.transform.position);
                    Assert.IsNotNull(room, "Enemies spawn on the floor");
                    Assert.AreEqual(RoomRole.Middle, room.Role,
                        "Normal enemies spawn only in middle rooms");
                    Assert.IsTrue(run.Map.IsWalkable(Vector2Int.RoundToInt(enemy.transform.position)),
                        "Enemies spawn on walkable tiles");
                }
                if (floor == 3) break;
                run.SetState(RunState.FloorCleared);
                run.AdvanceFloor();
                yield return null;
            }
        }
    }
}
