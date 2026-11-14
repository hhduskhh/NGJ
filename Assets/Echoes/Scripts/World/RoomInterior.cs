using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public struct RoomTrapAnchor
    {
        public Vector2Int Tile;
        public bool Bomb;

        public RoomTrapAnchor(Vector2Int tile, bool bomb)
        {
            Tile = tile;
            Bomb = bomb;
        }
    }

    // Interior flow plans traced from Docs/新建 PPTX 演示文稿.pptx:
    // blue = room frame, gray = walls, red = trap pads, white = wall openings.
    // Templates are authored in 36-unit cells, then rotated/mirrored to fit the
    // generated room. Every plan leaves at least one route between all doors.
    public static class RoomInterior
    {
        public static int VariantCount(RoomShape shape)
        {
            switch (shape)
            {
                case RoomShape.Single: return 3;
                case RoomShape.Line2: return 4;
                case RoomShape.Line3: return 2;
                default: return 1;
            }
        }

        // Planned partitions in world tiles. DungeonMap clips these away from
        // doorways and drops any piece that would split the room.
        public static List<RectInt> Partitions(DungeonMap.Room room)
        {
            var result = new List<RectInt>();
            if (room == null || room.Role == RoomRole.Start || room.Role == RoomRole.Boss)
                return result;
            switch (room.Shape)
            {
                case RoomShape.Single:
                    BuildSingle(room, result);
                    break;
                case RoomShape.Line2:
                    BuildLine2(room, result);
                    break;
                case RoomShape.Line3:
                    BuildLine3(room, result);
                    break;
                case RoomShape.Square2:
                    BuildSquare2(room, result);
                    break;
                case RoomShape.L:
                    BuildL(room, result);
                    break;
            }
            return result;
        }

        public static List<RoomTrapAnchor> TrapAnchors(DungeonMap.Room room)
        {
            var result = new List<RoomTrapAnchor>();
            if (room == null || room.Role == RoomRole.Start) return result;
            if (room.Role == RoomRole.Boss)
            {
                AddBossTraps(room, result);
                return result;
            }
            switch (room.Shape)
            {
                case RoomShape.Single:
                    AddSingleTraps(room, result);
                    break;
                case RoomShape.Line2:
                    AddLineTraps(room, result, true);
                    break;
                case RoomShape.Line3:
                    AddLineTraps(room, result, false);
                    break;
                case RoomShape.Square2:
                    AddSquare2Traps(room, result);
                    break;
                case RoomShape.L:
                    AddLTraps(room, result);
                    break;
            }
            return result;
        }

        // The double-diamond plan explicitly marks its two chambers as safe
        // interior pockets ("里面不刷怪" in the reference deck).
        public static List<RectInt> NoSpawnZones(DungeonMap.Room room)
        {
            var result = new List<RectInt>();
            if (room == null || room.Role != RoomRole.Middle) return result;
            if (room.Shape != RoomShape.Line2 || room.LayoutVariant % 4 != 3) return result;
            // Cover the complete chamber up to its two openings, including the
            // narrow tips missed by the former central 10x10 rectangles.
            var local = new List<RectInt>();
            foreach (int cx in new[] { 22, 50 })
                for (int dy = -9; dy <= 9; dy++)
                {
                    int halfWidth = 10 - 7 * Mathf.Abs(dy) / 9;
                    local.Add(new RectInt(cx - halfWidth, 18 + dy, halfWidth * 2 + 1, 1));
                }
            bool vertical = room.Bounds.height > room.Bounds.width;
            int sourceWidth = vertical ? room.Bounds.height : room.Bounds.width;
            foreach (var rect in local)
                result.Add(EmitRect(room, rect, vertical, false, sourceWidth));
            return result;
        }

        public static void PlaceTraps(DungeonMap map, DungeonMap.Room room, RunController run,
            Transform parent)
        {
            if (room.Role == RoomRole.Start) return;
            var used = new HashSet<Vector2Int>();
            foreach (var anchor in TrapAnchors(room))
            {
                var tile = FindTrapTile(map, room, anchor.Tile, used, run.Config);
                if (!tile.HasValue)
                    throw new System.InvalidOperationException(
                        "No safe trap position near " + anchor.Tile + " in " + room.Name);
                used.Add(tile.Value);
                RoomTrap.Create(run, map, tile.Value, anchor, parent);
            }
        }

        private static Vector2Int? FindTrapTile(DungeonMap map, DungeonMap.Room room,
            Vector2Int anchor, HashSet<Vector2Int> used, GameConfig config)
        {
            int max = Mathf.Max(0, config.trapAnchorSearchRadius);
            for (int radius = 0; radius <= max; radius++)
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (radius > 0 && Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                            continue;
                        var tile = new Vector2Int(anchor.x + dx, anchor.y + dy);
                        if (used.Contains(tile)) continue;
                        if (!map.IsWalkable(tile)) continue;
                        if (map.RoomAtPosition(new Vector2(tile.x, tile.y)) != room) continue;
                        bool nearDoor = false;
                        int clearance = Mathf.Max(0, config.trapDoorClearance);
                        foreach (var door in room.DoorOpenings)
                            if (new RectInt(door.x - clearance, door.y - clearance,
                                door.width + clearance * 2, door.height + clearance * 2).Contains(tile))
                            { nearDoor = true; break; }
                        if (nearDoor) continue;
                        return tile;
                    }
            return null;
        }

        private static void BuildSingle(DungeonMap.Room room, List<RectInt> result)
        {
            switch (room.LayoutVariant % 3)
            {
                case 0: // Donut gallery with two side gates and a trapped core.
                    Ring(result, room, 7, 7, 22, 7);
                    break;
                case 1: // Staggered vertical baffles: one from each long wall.
                    Add(result, room, 8, 12, 2, 24);
                    Add(result, room, 23, 0, 2, 24);
                    break;
                default: // Free-standing central screen splits left/right routes.
                    Add(result, room, 17, 5, 3, 25);
                    break;
            }
        }

        private static void AddSingleTraps(DungeonMap.Room room, List<RoomTrapAnchor> result)
        {
            if (room.LayoutVariant % 3 != 0) return;
            AddTrap(room, result, 15, 18);
            AddTrap(room, result, 21, 18);
            AddTrap(room, result, 18, 15);
            AddTrap(room, result, 18, 21);
        }

        private static void BuildLine2(DungeonMap.Room room, List<RectInt> result)
        {
            bool vertical = room.Bounds.height > room.Bounds.width;
            int sourceWidth = vertical ? room.Bounds.height : room.Bounds.width;
            var local = new List<RectInt>();
            switch (room.LayoutVariant % 4)
            {
                case 0: Line2CrossHall(local); break;
                case 1: Line2Pinwheel(local); break;
                case 2: Line2Maze(local); break;
                default: Line2Diamonds(local); break;
            }
            foreach (var rect in local)
                result.Add(EmitRect(room, rect, vertical, false, sourceWidth));
        }

        private static void Line2CrossHall(List<RectInt> local)
        {
            // Narrow north/south service lanes and three staggered columns.
            local.Add(new RectInt(7, 32, 58, 1));
            local.Add(new RectInt(7, 3, 58, 1));
            local.Add(new RectInt(12, 9, 1, 18));
            local.Add(new RectInt(34, 9, 1, 18));
            local.Add(new RectInt(57, 9, 1, 18));
        }

        private static void Line2Pinwheel(List<RectInt> local)
        {
            // Four angled screens orbit a solid diamond dais.
            FillDiamond(local, 36, 18, 5);
            Line(local, 2, 2, 18, 18);
            Line(local, 2, 24, 10, 32);
            Line(local, 54, 18, 70, 2);
            Line(local, 62, 32, 70, 24);
        }

        private static void Line2Maze(List<RectInt> local)
        {
            // Grid of four upright screens and two long horizontal screens,
            // each with wide white openings traced from the deck.
            local.Add(new RectInt(11, 2, 1, 7));
            local.Add(new RectInt(11, 15, 1, 8));
            local.Add(new RectInt(11, 29, 1, 6));
            local.Add(new RectInt(25, 2, 1, 7));
            local.Add(new RectInt(25, 15, 1, 8));
            local.Add(new RectInt(49, 15, 1, 8));
            local.Add(new RectInt(49, 29, 1, 6));
            local.Add(new RectInt(61, 2, 1, 7));
            local.Add(new RectInt(61, 15, 1, 8));
            local.Add(new RectInt(61, 29, 1, 6));
            local.Add(new RectInt(0, 27, 17, 1));
            local.Add(new RectInt(20, 27, 11, 1));
            local.Add(new RectInt(33, 27, 22, 1));
            local.Add(new RectInt(58, 27, 14, 1));
            local.Add(new RectInt(0, 11, 17, 1));
            local.Add(new RectInt(20, 11, 22, 1));
            local.Add(new RectInt(45, 11, 10, 1));
            local.Add(new RectInt(58, 11, 14, 1));
        }

        private static void Line2Diamonds(List<RectInt> local)
        {
            // Twin diamond chambers with gaps at top and bottom; both interiors
            // are safe pockets that never spawn enemies.
            DiamondChamber(local, 22, 18);
            DiamondChamber(local, 50, 18);
        }

        private static void DiamondChamber(List<RectInt> result, int cx, int cy)
        {
            Line(result, cx - 10, cy, cx - 3, cy + 9);
            Line(result, cx + 3, cy + 9, cx + 10, cy);
            Line(result, cx + 10, cy, cx + 3, cy - 9);
            Line(result, cx - 3, cy - 9, cx - 10, cy);
        }

        private static void AddLineTraps(DungeonMap.Room room, List<RoomTrapAnchor> result, bool line2)
        {
            bool vertical = room.Bounds.height > room.Bounds.width;
            int sourceWidth = vertical ? room.Bounds.height : room.Bounds.width;
            var local = new List<Vector2Int>();
            if (line2 && room.LayoutVariant % 4 == 0)
            {
                // Four red bars beside the cross-hall columns.
                local.Add(new Vector2Int(22, 24));
                local.Add(new Vector2Int(22, 27));
                local.Add(new Vector2Int(22, 7));
                local.Add(new Vector2Int(22, 10));
                local.Add(new Vector2Int(48, 24));
                local.Add(new Vector2Int(48, 27));
                local.Add(new Vector2Int(48, 7));
                local.Add(new Vector2Int(48, 10));
            }
            else if (line2 && room.LayoutVariant % 4 == 3)
            {
                // Corner pads and the two centre notches of the diamond plan.
                local.Add(new Vector2Int(3, 3));
                local.Add(new Vector2Int(69, 3));
                local.Add(new Vector2Int(3, 33));
                local.Add(new Vector2Int(69, 33));
                local.Add(new Vector2Int(36, 33));
                local.Add(new Vector2Int(36, 3));
            }
            else if (!line2 && room.LayoutVariant % 2 == 0)
            {
                local.Add(new Vector2Int(7, 25));
                local.Add(new Vector2Int(7, 11));
                local.Add(new Vector2Int(40, 25));
                local.Add(new Vector2Int(40, 11));
                local.Add(new Vector2Int(74, 25));
                local.Add(new Vector2Int(74, 11));
                local.Add(new Vector2Int(101, 25));
                local.Add(new Vector2Int(101, 11));
            }
            bool mirror = !line2 && room.LayoutVariant % 2 == 1;
            foreach (var tile in local)
                result.Add(new RoomTrapAnchor(
                    EmitPoint(room, tile, vertical, mirror, sourceWidth),
                    result.Count % 2 == 0));
        }

        private static void BuildLine3(DungeonMap.Room room, List<RectInt> result)
        {
            bool vertical = room.Bounds.height > room.Bounds.width;
            int sourceWidth = vertical ? room.Bounds.height : room.Bounds.width;
            bool mirror = room.LayoutVariant % 2 == 1;
            var local = new List<RectInt>
            {
                // Four trap columns with a wall core between each red pair.
                new RectInt(7, 12, 1, 12),
                new RectInt(40, 12, 1, 12),
                new RectInt(74, 12, 1, 12),
                new RectInt(101, 12, 1, 12),
                // Long baffles alternate north/south to force a slalom.
                new RectInt(12, 32, 22, 1),
                new RectInt(84, 32, 23, 1),
                new RectInt(45, 3, 24, 1),
                new RectInt(23, 31, 2, 2),
                new RectInt(55, 3, 2, 2)
            };
            foreach (var rect in local)
                result.Add(EmitRect(room, rect, vertical, mirror, sourceWidth));
        }

        private static void BuildSquare2(DungeonMap.Room room, List<RectInt> result)
        {
            // Three concentric rings with cardinal gates and a sealed core.
            Ring(result, room, 6, 6, 60, 8);
            Ring(result, room, 13, 13, 46, 8);
            Ring(result, room, 20, 20, 32, 8);
            Add(result, room, 32, 32, 8, 8);
        }

        private static void AddSquare2Traps(DungeonMap.Room room, List<RoomTrapAnchor> result)
        {
            AddTrap(room, result, 3, 36);
            AddTrap(room, result, 36, 3);
            AddTrap(room, result, 69, 36);
            AddTrap(room, result, 36, 69);
            AddTrap(room, result, 18, 18);
            AddTrap(room, result, 54, 18);
            AddTrap(room, result, 18, 54);
            AddTrap(room, result, 54, 54);
            AddTrap(room, result, 28, 36);
            AddTrap(room, result, 44, 36);
            AddTrap(room, result, 36, 28);
            AddTrap(room, result, 36, 44);
            AddTrap(room, result, 9, 36);
            AddTrap(room, result, 63, 36);
            AddTrap(room, result, 36, 9);
            AddTrap(room, result, 36, 63);
        }

        private static void BuildL(DungeonMap.Room room, List<RectInt> result)
        {
            if (room.Cells.Count < 3) return;
            var elbow = FindElbow(room);
            if (!elbow.HasValue) return;
            foreach (var cell in room.Cells)
            {
                if (cell == elbow.Value) continue;
                int ax = cell.x * DungeonMap.CellWidth;
                int ay = room.CellOriginY(cell.y);
                int dx = cell.x - elbow.Value.x;
                int dy = cell.y - elbow.Value.y;
                if (dx > 0)
                {
                    Line(result, ax + 20, ay + 4, ax + 28, ay + 12);
                    Line(result, ax + 20, ay + 31, ax + 28, ay + 23);
                }
                else if (dx < 0)
                {
                    Line(result, ax + 8, ay + 4, ax + 16, ay + 12);
                    Line(result, ax + 8, ay + 31, ax + 16, ay + 23);
                }
                else if (dy < 0)
                {
                    Line(result, ax + 4, ay + 20, ax + 12, ay + 28);
                    Line(result, ax + 31, ay + 20, ax + 23, ay + 28);
                }
                else
                {
                    Line(result, ax + 4, ay + 16, ax + 12, ay + 8);
                    Line(result, ax + 31, ay + 16, ax + 23, ay + 8);
                }
            }
        }

        private static void AddLTraps(DungeonMap.Room room, List<RoomTrapAnchor> result)
        {
            if (room.Cells.Count < 3) return;
            var elbow = FindElbow(room);
            if (!elbow.HasValue) return;
            foreach (var cell in room.Cells)
            {
                if (cell == elbow.Value) continue;
                int ax = cell.x * DungeonMap.CellWidth - room.Bounds.x;
                int ay = room.CellOriginY(cell.y) - room.Bounds.y;
                int dx = cell.x - elbow.Value.x;
                int dy = cell.y - elbow.Value.y;
                if (dx > 0)
                {
                    AddTrap(room, result, ax + 32, ay + 14);
                    AddTrap(room, result, ax + 32, ay + 22);
                }
                else if (dx < 0)
                {
                    AddTrap(room, result, ax + 3, ay + 14);
                    AddTrap(room, result, ax + 3, ay + 22);
                }
                else if (dy < 0)
                {
                    AddTrap(room, result, ax + 14, ay + 32);
                    AddTrap(room, result, ax + 22, ay + 32);
                }
                else
                {
                    AddTrap(room, result, ax + 14, ay + 3);
                    AddTrap(room, result, ax + 22, ay + 3);
                }
            }
        }

        private static Vector2Int? FindElbow(DungeonMap.Room room)
        {
            foreach (var cell in room.Cells)
            {
                int neighbours = 0;
                if (room.Cells.Contains(cell + Vector2Int.left)) neighbours++;
                if (room.Cells.Contains(cell + Vector2Int.right)) neighbours++;
                if (room.Cells.Contains(cell + Vector2Int.up)) neighbours++;
                if (room.Cells.Contains(cell + Vector2Int.down)) neighbours++;
                if (neighbours >= 2) return cell;
            }
            return null;
        }

        private static void AddBossTraps(DungeonMap.Room room, List<RoomTrapAnchor> result)
        {
            // Two red danger rings around the open duel floor.
            foreach (var p in new[]
            {
                new Vector2Int(4, 4), new Vector2Int(18, 4), new Vector2Int(32, 4),
                new Vector2Int(4, 18), new Vector2Int(32, 18),
                new Vector2Int(4, 32), new Vector2Int(18, 32), new Vector2Int(32, 32)
            })
                AddTrap(room, result, p.x, p.y);
            foreach (var p in new[]
            {
                new Vector2Int(11, 11), new Vector2Int(25, 11),
                new Vector2Int(11, 25), new Vector2Int(25, 25)
            })
                AddTrap(room, result, p.x, p.y);
        }

        private static void AddTrap(DungeonMap.Room room, List<RoomTrapAnchor> result,
            int x, int y)
        {
            result.Add(new RoomTrapAnchor(
                new Vector2Int(room.Bounds.x + x, room.Bounds.y + y),
                result.Count % 2 == 0));
        }

        private static void Ring(List<RectInt> result, DungeonMap.Room room,
            int left, int bottom, int size, int gap)
        {
            Ring(result, room.Bounds.x + left, room.Bounds.y + bottom, size, gap);
        }

        private static void Ring(List<RectInt> result, int left, int bottom, int size, int gap)
        {
            int right = left + size - 1;
            int top = bottom + size - 1;
            int gapStart = left + (size - gap) / 2;
            int gapEnd = gapStart + gap;
            AddH(result, left, bottom, gapStart - left);
            AddH(result, gapEnd, bottom, right - gapEnd + 1);
            AddH(result, left, top, gapStart - left);
            AddH(result, gapEnd, top, right - gapEnd + 1);
            int vGapStart = bottom + (size - gap) / 2;
            int vGapEnd = vGapStart + gap;
            AddV(result, left, bottom, vGapStart - bottom);
            AddV(result, left, vGapEnd, top - vGapEnd + 1);
            AddV(result, right, bottom, vGapStart - bottom);
            AddV(result, right, vGapEnd, top - vGapEnd + 1);
        }

        private static void Add(List<RectInt> result, DungeonMap.Room room,
            int x, int y, int width, int height)
        {
            AddH(result, room.Bounds.x + x, room.Bounds.y + y, width, height);
        }

        private static void AddH(List<RectInt> result, int x, int y, int width, int height = 1)
        {
            if (width > 0 && height > 0) result.Add(new RectInt(x, y, width, height));
        }

        private static void AddV(List<RectInt> result, int x, int y, int height)
        {
            if (height > 0) result.Add(new RectInt(x, y, 1, height));
        }

        private static void FillDiamond(List<RectInt> result, int cx, int cy, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int half = radius - Mathf.Abs(dy);
                AddH(result, cx - half, cy + dy, half * 2 + 1);
            }
        }

        // Rasterizes a 1-tile diagonal wall and merges every horizontal run.
        private static void Line(List<RectInt> result, int x0, int y0, int x1, int y1)
        {
            var rows = new Dictionary<int, List<int>>();
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                if (!rows.TryGetValue(y0, out var xs))
                    rows[y0] = xs = new List<int>();
                xs.Add(x0);
                if (x0 == x1 && y0 == y1) break;
                int doubled = error * 2;
                if (doubled >= dy)
                {
                    error += dy;
                    x0 += sx;
                }
                if (doubled <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
            foreach (var row in rows)
            {
                var xs = row.Value;
                xs.Sort();
                int start = xs[0], previous = xs[0];
                for (int i = 1; i < xs.Count; i++)
                {
                    if (xs[i] == previous + 1)
                    {
                        previous = xs[i];
                        continue;
                    }
                    AddH(result, start, row.Key, previous - start + 1);
                    start = previous = xs[i];
                }
                AddH(result, start, row.Key, previous - start + 1);
            }
        }

        private static RectInt EmitRect(DungeonMap.Room room, RectInt rect,
            bool vertical, bool mirror, int sourceWidth)
        {
            int x = mirror ? sourceWidth - rect.x - rect.width : rect.x;
            if (!vertical)
                return new RectInt(room.Bounds.x + x, room.Bounds.y + rect.y,
                    rect.width, rect.height);
            return new RectInt(room.Bounds.x + rect.y,
                room.Bounds.y + sourceWidth - x - rect.width, rect.height, rect.width);
        }

        private static Vector2Int EmitPoint(DungeonMap.Room room, Vector2Int point,
            bool vertical, bool mirror, int sourceWidth)
        {
            int x = mirror ? sourceWidth - 1 - point.x : point.x;
            if (!vertical)
                return new Vector2Int(room.Bounds.x + x, room.Bounds.y + point.y);
            return new Vector2Int(room.Bounds.x + point.y,
                room.Bounds.y + sourceWidth - 1 - x);
        }
    }
}
