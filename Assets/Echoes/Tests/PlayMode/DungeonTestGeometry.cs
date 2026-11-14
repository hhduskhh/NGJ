using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Echoes.Tests
{
    public sealed class DoorGeometry
    {
        public DungeonMap.Door Door;
        public Vector2Int Source;
        public Vector2Int Through;
        public Vector2Int Around;
        public Vector2Int Wall;
        public bool Vertical;
    }

    // Finds a door plus four probe tiles: inside one room, through the opening,
    // around the jamb, and the facing wall tile. All PlayMode visibility tests use it.
    public static class DungeonTestGeometry
    {
        public static Vector2 World(Vector2Int tile) => new Vector2(tile.x, tile.y);

        public static DoorGeometry FindDoor(RunController run)
        {
            foreach (var door in run.Map.Doors)
            {
                var geometry = Search(run, door);
                if (geometry != null) return geometry;
            }
            Assert.Fail("No usable door geometry found");
            return null;
        }

        private static DoorGeometry Search(RunController run, DungeonMap.Door door)
        {
            var opening = door.Opening;
            bool vertical = opening.width == 2;
            var sourceTiles = new List<Vector2Int>();
            var throughTiles = new List<Vector2Int>();
            var aroundTiles = new List<Vector2Int>();
            int minX = Mathf.Max(0, opening.x - 7);
            int maxX = Mathf.Min(run.Map.Width - 1, opening.xMax + 6);
            int minY = Mathf.Max(0, opening.y - 7);
            int maxY = Mathf.Min(run.Map.Height - 1, opening.yMax + 6);
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                {
                    var tile = new Vector2Int(x, y);
                    if (!run.Map.IsWalkable(tile)) continue;
                    bool aligned = vertical ? y >= opening.y && y < opening.yMax :
                        x >= opening.x && x < opening.xMax;
                    bool behind = vertical ? x <= opening.x - 2 : y <= opening.y - 2;
                    bool beyond = vertical ? x >= opening.x + opening.width + 1 :
                        y >= opening.y + opening.height + 1;
                    bool beside = vertical ? y <= opening.y - 3 || y >= opening.yMax + 2 :
                        x <= opening.x - 3 || x >= opening.xMax + 2;
                    if (behind && aligned) sourceTiles.Add(tile);
                    else if (beyond && aligned) throughTiles.Add(tile);
                    else if (beyond && beside) aroundTiles.Add(tile);
                }
            var center = door.Center;
            SortAndTrim(sourceTiles, center);
            SortAndTrim(throughTiles, center);
            SortAndTrim(aroundTiles, center);

            foreach (var source in sourceTiles)
                foreach (var through in throughTiles)
                {
                    float throughDistance = Vector2.Distance(World(source), World(through));
                    if (throughDistance < 2.5f || throughDistance > 7f) continue;
                    if (!run.Map.HasClearInteractionPath(World(source), World(through))) continue;
                    foreach (var around in aroundTiles)
                    {
                        float aroundDistance = Vector2.Distance(World(source), World(around));
                        if (aroundDistance < 2.5f || aroundDistance > 7f) continue;
                        if (run.Map.HasLineOfSight(World(source), World(around))) continue;
                        var wall = FindVisibleWall(run, source, opening);
                        if (!wall.HasValue) continue;
                        return new DoorGeometry
                        {
                            Door = door, Source = source, Through = through,
                            Around = around, Wall = wall.Value, Vertical = vertical
                        };
                    }
                }
            return null;
        }

        private static void SortAndTrim(List<Vector2Int> tiles, Vector2 center)
        {
            tiles.Sort((a, b) => Vector2.Distance(World(a), center).CompareTo(Vector2.Distance(World(b), center)));
            if (tiles.Count > 16) tiles.RemoveRange(16, tiles.Count - 16);
        }

        private static Vector2Int? FindVisibleWall(RunController run, Vector2Int source, RectInt opening)
        {
            for (int x = opening.x - 3; x < opening.xMax + 3; x++)
                for (int y = opening.y - 3; y < opening.yMax + 3; y++)
                {
                    var tile = new Vector2Int(x, y);
                    if (x < 0 || y < 0 || x >= run.Map.Width || y >= run.Map.Height) continue;
                    if (run.Map.IsFloorTile(tile)) continue;
                    if (!run.Map.HasLineOfSight(World(source), World(tile), true)) continue;
                    return tile;
                }
            return null;
        }
    }
}
