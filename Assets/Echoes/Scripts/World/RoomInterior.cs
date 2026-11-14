using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public static class RoomInterior
    {
        // Templates are specified in local tiles inside each 36x36 cell.
        public static IEnumerable<RectInt> Partitions(DungeonMap.Room room)
        {
            foreach (var cell in room.Cells)
            {
                int x = cell.x * DungeonMap.CellWidth;
                int y = DungeonMap.CellOriginY(cell.y);
                if (room.Role == RoomRole.Start)
                {
                    yield return new RectInt(x + 9, y + 20, 1, 7);
                    yield return new RectInt(x + 26, y + 20, 1, 7);
                    continue;
                }
                if (room.Role == RoomRole.Boss)
                {
                    yield return new RectInt(x + 9, y + 12, 1, 9);
                    yield return new RectInt(x + 26, y + 15, 1, 9);
                    continue;
                }

                switch (room.Shape)
                {
                    case RoomShape.Single: // Offset screens either side of the central landmark.
                        yield return new RectInt(x + 3, y + 12, 8, 1);
                        yield return new RectInt(x + 25, y + 23, 8, 1);
                        break;
                    case RoomShape.Line2:
                    case RoomShape.Line3: // Alternate the baffles along the long axis.
                        bool horizontal = room.Bounds.width > room.Bounds.height;
                        bool reverse = ((cell.x + cell.y + room.Rotation) & 1) != 0;
                        int inset = reverse ? 22 : 3;
                        int length = room.Shape == RoomShape.Line3 ? 11 : 8;
                        yield return horizontal ? new RectInt(x + 12, y + inset, 1, length)
                            : new RectInt(x + inset, y + 12, length, 1);
                        break;
                    case RoomShape.Square2: // Four screens around the shared courtyard.
                        int side = x == room.Bounds.xMin ? 30 : 5;
                        yield return new RectInt(x + side, y + 10, 1, 14);
                        break;
                    case RoomShape.L: // Side galleries along the two arms.
                        bool hasHorizontalNeighbour = room.Cells.Contains(cell + Vector2Int.left)
                            || room.Cells.Contains(cell + Vector2Int.right);
                        yield return hasHorizontalNeighbour ? new RectInt(x + 11, y + 3, 1, 10)
                            : new RectInt(x + 3, y + 23, 10, 1);
                        break;
                }
            }
        }

        public static void PlaceTraps(DungeonMap map, DungeonMap.Room room, RunController run, Transform parent)
        {
            if (room.Role == RoomRole.Start) return;
            foreach (var cell in room.Cells)
            {
                var origin = new Vector2Int(cell.x * DungeonMap.CellWidth, DungeonMap.CellOriginY(cell.y));
                bool flip = ((cell.x + cell.y + map.Floor) & 1) != 0;
                Place(false, new Vector2Int(11, flip ? 24 : 11));
                Place(true, new Vector2Int(25, flip ? 11 : 24));

                void Place(bool bomb, Vector2Int target)
                {
                    // Pick the closest clear 3x3 pad on this flank after furniture placement.
                    int best = int.MaxValue;
                    Vector2Int position = default;
                    for (int dx = -5; dx <= 5; dx++)
                        for (int dy = -5; dy <= 5; dy++)
                        {
                            int distance = dx * dx + dy * dy;
                            if (distance >= best) continue;
                            var tile = origin + target + new Vector2Int(dx, dy);
                            bool clear = true;
                            for (int ix = -1; ix <= 1 && clear; ix++)
                                for (int iy = -1; iy <= 1 && clear; iy++)
                                    clear = map.IsWalkable(tile + new Vector2Int(ix, iy));
                            if (!clear) continue;
                            best = distance;
                            position = tile;
                        }
                    if (best != int.MaxValue) RoomTrap.Create(run, map, position, bomb, parent);
                }
            }
        }
    }
}
