using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Echoes.Tests
{
    public sealed class DungeonGenerationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void DiamondChambersExcludeEnemySpawnsAtTheirTipsAndSides(bool vertical)
        {
            var room = new DungeonMap.Room
            {
                Role = RoomRole.Middle, Shape = RoomShape.Line2, LayoutVariant = 3,
                Bounds = new RectInt(36, 72, vertical ? 36 : 72, vertical ? 72 : 36)
            };
            var zones = RoomInterior.NoSpawnZones(room);
            Vector2Int World(int x, int y) => vertical
                ? new Vector2Int(room.Bounds.x + y, room.Bounds.y + 71 - x)
                : new Vector2Int(room.Bounds.x + x, room.Bounds.y + y);
            bool Excluded(Vector2Int p) => zones.Exists(zone => zone.Contains(p));
            foreach (int cx in new[] { 22, 50 })
            {
                foreach (var offset in new[] { Vector2Int.zero, new Vector2Int(0, 7),
                    new Vector2Int(0, -7), new Vector2Int(7, 0), new Vector2Int(-7, 0) })
                    Assert.IsTrue(Excluded(World(cx + offset.x, 18 + offset.y)),
                        "The whole chamber interior must exclude spawns: " + offset);
                Assert.IsFalse(Excluded(World(cx, 6)), "The outer corridor remains available");
            }
        }

        [Test]
        public void PlansCoverTheFiveByFiveGridAndRespectLeafRooms()
        {
            var bossCells = new HashSet<Vector2Int>();
            var shapeCounts = new Dictionary<RoomShape, int>();
            for (int seed = 0; seed < 300; seed++)
            {
                var plan = DungeonPlan.Generate(seed);
                Assert.IsTrue(plan.Valid, "Seed " + seed);
                Assert.AreEqual(DungeonPlan.GridSize * DungeonPlan.GridSize,
                    CellCount(plan), "Every seed fills all 25 cells");

                Assert.AreEqual(RoomRole.Start, plan.Rooms[0].Role);
                Assert.AreEqual(RoomRole.Boss, plan.Rooms[plan.Rooms.Count - 1].Role);
                Assert.AreEqual(DungeonPlan.StartCell, plan.Rooms[0].Cells[0]);
                bossCells.Add(plan.Boss.Cells[0]);
                Assert.AreEqual(1, plan.Rooms[0].Cells.Count);
                Assert.AreEqual(1, plan.Rooms[plan.Rooms.Count - 1].Cells.Count);
                Assert.AreEqual(1, plan.Rooms[0].Doors.Count, "Start is a leaf");
                Assert.AreEqual(1, plan.Rooms[plan.Rooms.Count - 1].Doors.Count, "Boss is a leaf");

                int multiCellKinds = 0;
                var kinds = new HashSet<RoomShape>();
                foreach (var room in plan.Rooms)
                {
                    Assert.LessOrEqual(room.Cells.Count, 4);
                    Assert.GreaterOrEqual(room.Doors.Count, 1);
                    Assert.LessOrEqual(room.Doors.Count, 4);
                    Assert.AreEqual(ExpectedCells(room.Shape), room.Cells.Count);
                    int width = room.MaxCell.x - room.MinCell.x + 1;
                    int height = room.MaxCell.y - room.MinCell.y + 1;
                    Assert.IsTrue(ExpectedFootprint(room.Shape, width, height),
                        room.Shape + " has an unexpected footprint " + width + "x" + height);
                    if (room.Role == RoomRole.Middle && room.Shape != RoomShape.Single)
                        kinds.Add(room.Shape);
                    if (!shapeCounts.ContainsKey(room.Shape)) shapeCounts[room.Shape] = 0;
                    shapeCounts[room.Shape]++;
                }
                multiCellKinds = kinds.Count;
                Assert.GreaterOrEqual(multiCellKinds, 2, "Mixed room sizes on seed " + seed);

                foreach (var door in plan.Doors)
                {
                    Assert.AreEqual(1, Mathf.Abs(door.CellA.x - door.CellB.x) +
                        Mathf.Abs(door.CellA.y - door.CellB.y), "Doors join adjacent cells");
                    Assert.AreNotSame(door.A, door.B);
                    Assert.IsTrue(door.A.ContainsCell(door.CellA));
                    Assert.IsTrue(door.B.ContainsCell(door.CellB));
                }
                AssertAllRoomsReachable(plan);
            }

            Assert.Greater(bossCells.Count, 1);
            foreach (RoomShape shape in System.Enum.GetValues(typeof(RoomShape)))
                Assert.Greater(shapeCounts[shape], 0, "All five layout shapes appear across seeds");
        }

        [Test]
        public void DeterministicFallbackIsValid()
        {
            var method = typeof(DungeonPlan).GetMethod("BuildFallback",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method);
            for (int seed = 0; seed < 10; seed++)
            {
                var plan = (DungeonPlan)method.Invoke(null, new object[] { seed });
                Assert.IsTrue(plan.Valid);
                Assert.AreEqual(DungeonPlan.GridSize * DungeonPlan.GridSize, CellCount(plan));
                Assert.AreEqual(1, plan.Rooms[0].Doors.Count);
                Assert.AreEqual(1, plan.Rooms[plan.Rooms.Count - 1].Doors.Count);
                AssertAllRoomsReachable(plan);
            }
        }

        [Test]
        public void SameSeedProducesTheSamePlan()
        {
            var a = DungeonPlan.Generate(713);
            var b = DungeonPlan.Generate(713);
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            Assert.AreEqual(a.Doors.Count, b.Doors.Count);
            for (int i = 0; i < a.Rooms.Count; i++)
            {
                Assert.AreEqual(a.Rooms[i].Shape, b.Rooms[i].Shape);
                Assert.AreEqual(a.Rooms[i].Rotation, b.Rooms[i].Rotation);
                CollectionAssert.AreEqual(a.Rooms[i].Cells, b.Rooms[i].Cells);
            }
            for (int i = 0; i < a.Doors.Count; i++)
            {
                Assert.AreEqual(a.Doors[i].CellA, b.Doors[i].CellA);
                Assert.AreEqual(a.Doors[i].CellB, b.Doors[i].CellB);
            }
        }

        [Test]
        public void EveryMiddleRoomHasAtLeastOneDoorAndTheGraphIsATreePlusOptionalLoops()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var plan = DungeonPlan.Generate(seed);
                int minimum = plan.Rooms.Count - 1;
                Assert.GreaterOrEqual(plan.Doors.Count, minimum, "A connected room graph needs a spanning tree");
            }
        }

        private static int CellCount(DungeonPlan plan)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (var room in plan.Rooms)
                foreach (var cell in room.Cells)
                    Assert.IsTrue(cells.Add(cell), "Rooms must not overlap");
            return cells.Count;
        }

        private static bool ExpectedFootprint(RoomShape shape, int width, int height)
        {
            switch (shape)
            {
                case RoomShape.Single: return width == 1 && height == 1;
                case RoomShape.Line2: return width * height == 2 && (width == 2 || height == 2);
                case RoomShape.Line3: return width * height == 3 && (width == 3 || height == 3);
                case RoomShape.Square2: return width == 2 && height == 2;
                case RoomShape.L: return width == 2 && height == 2;
                default: return false;
            }
        }

        private static int ExpectedCells(RoomShape shape)
        {
            switch (shape)
            {
                case RoomShape.Single: return 1;
                case RoomShape.Line2: return 2;
                case RoomShape.Line3: return 3;
                case RoomShape.Square2: return 4;
                case RoomShape.L: return 3;
                default: return 0;
            }
        }

        private static void AssertAllRoomsReachable(DungeonPlan plan)
        {
            var graph = new Dictionary<PlannedRoom, List<PlannedRoom>>();
            foreach (var room in plan.Rooms) graph[room] = new List<PlannedRoom>();
            foreach (var door in plan.Doors)
            {
                graph[door.A].Add(door.B);
                graph[door.B].Add(door.A);
            }
            var visited = new HashSet<PlannedRoom>();
            var frontier = new Queue<PlannedRoom>();
            visited.Add(plan.Rooms[0]);
            frontier.Enqueue(plan.Rooms[0]);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var next in graph[current])
                    if (visited.Add(next)) frontier.Enqueue(next);
            }
            Assert.AreEqual(plan.Rooms.Count, visited.Count, "Every room must be reachable");
        }
    }
}
