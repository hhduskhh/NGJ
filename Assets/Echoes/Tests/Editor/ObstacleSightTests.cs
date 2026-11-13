using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Echoes.Tests
{
    public sealed class ObstacleSightTests
    {
        private DungeonMap map;
        private bool[,] cells;

        [SetUp]
        public void SetUp()
        {
            map = new GameObject("SightTestMap").AddComponent<DungeonMap>();
            cells = (bool[,])typeof(DungeonMap).GetField("walkable", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(map);
            for (int x = 1; x < DungeonMap.Width - 1; x++)
                for (int y = 1; y < DungeonMap.Height - 1; y++) cells[x, y] = true;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(map.gameObject);

        [TestCase(2, 2, 8, 2)]
        [TestCase(8, 2, 2, 2)]
        [TestCase(2, 2, 2, 8)]
        [TestCase(2, 8, 2, 2)]
        [TestCase(2, 2, 8, 8)]
        public void OpenFloorIsVisibleInBothAxisDirections(int x, int y, int targetX, int targetY)
        {
            Assert.IsTrue(map.HasLineOfSight(new Vector2(x, y), new Vector2(targetX, targetY)));
        }

        [Test]
        public void FirstWallCanBeLitButCannotBeTargetedOrSeenThrough()
        {
            cells[4, 2] = false;
            Assert.IsTrue(map.HasLineOfSight(new Vector2(2, 2), new Vector2(4, 2), true));
            Assert.IsFalse(map.HasLineOfSight(new Vector2(2, 2), new Vector2(4, 2)));
            Assert.IsFalse(map.HasLineOfSight(new Vector2(2, 2), new Vector2(5, 2), true));
            Assert.IsFalse(map.HasLineOfSight(new Vector2(5, 2), new Vector2(2, 2), true));
        }

        [Test]
        public void DoorwaysPassSightWhileTheAdjacentWallBlocksIt()
        {
            for (int y = 1; y < DungeonMap.Height - 1; y++) cells[4, y] = y == 5;
            Assert.IsTrue(map.HasLineOfSight(new Vector2(2, 5), new Vector2(6, 5)));
            Assert.IsFalse(map.HasLineOfSight(new Vector2(2, 4), new Vector2(6, 4), true));
        }

        [Test]
        public void DiagonalSightDoesNotLeakThroughWallCorners()
        {
            cells[3, 2] = false;
            Assert.IsFalse(map.HasLineOfSight(new Vector2(2, 2), new Vector2(4, 4), true));
            Assert.IsFalse(map.HasLineOfSight(new Vector2(4, 4), new Vector2(2, 2), true));
            // Less than one old sampling interval, but the segment still crosses the corner.
            Assert.IsFalse(map.HasLineOfSight(new Vector2(2.49f, 2.49f), new Vector2(2.51f, 2.51f), true));
        }

        [Test]
        public void InvalidSourcesAndMapExteriorStayHidden()
        {
            Assert.IsTrue(map.HasLineOfSight(new Vector2(2, 2), new Vector2(2, 2)));
            Assert.IsFalse(map.HasLineOfSight(Vector2.zero, Vector2.zero, true));
            Assert.IsFalse(map.HasLineOfSight(new Vector2(1, 2), new Vector2(-1, 2), true));
        }
    }
}
