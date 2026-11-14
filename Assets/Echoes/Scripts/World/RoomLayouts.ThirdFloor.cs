using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public static partial class RoomLayouts
    {
        private static List<RoomPropSpec> BuildThirdFloor(DungeonMap.Room room, System.Random random)
        {
            var result = new List<RoomPropSpec>();
            var bounds = room.Bounds;
            if (room.Role == RoomRole.Start)
            {
                result.Add(StartStall.Layout(room));
                Native(result, "f3_reliquary", bounds, .23f, .80f);
                Native(result, "f3_crystal", bounds, .80f, .80f);
            }
            else if (room.Role == RoomRole.Boss)
            {
                foreach (float x in new[] { .17f, .83f })
                    foreach (float y in new[] { .17f, .83f })
                        Native(result, "f3_boss_pillar", bounds, x, y);
            }
            else
            {
                int focus = random.Next(room.Cells.Count);
                int variant = random.Next(3);
                for (int i = 0; i < room.Cells.Count; i++)
                {
                    var cell = room.Cells[i];
                    var area = new RectInt(cell.x * DungeonMap.CellWidth,
                        room.CellOriginY(cell.y), DungeonMap.CellWidth, DungeonMap.CellHeight);
                    // One tall shrine per room. Other cells use hollow shells or sealed bays.
                    if (i == focus) Native(result, "f3_shrine", area, .50f, .50f);
                    else
                    {
                        Native(result, "f3_shell", area, .40f, .50f);
                        Native(result, "f3_reliquary", area, .65f, .50f);
                    }
                    foreach (float x in new[] { .20f, .80f })
                        foreach (float y in new[] { .20f, .80f })
                            Native(result, "f3_shell", area, x, y);
                    Native(result, "f3_shell", area, .50f, .23f);
                    Native(result, "f3_shell", area, .50f, .80f);
                    Native(result, "f3_reliquary", area, .23f, variant == 1 ? .43f : .50f);
                    Native(result, "f3_reliquary", area, .77f, variant == 1 ? .57f : .50f);
                    foreach (float x in new[] { .35f, .65f })
                        foreach (float y in new[] { .35f, .68f })
                            Native(result, "f3_crystal", area, x, y);
                    // Pipes stay upright and face south even in vertical/L-shaped rooms.
                    // These are isolated ranks, not rotated copies of a long horizontal prop.
                    Native(result, "f3_organ", area, .15f, .64f);
                    Native(result, "f3_organ", area, .85f, .36f);
                }
            }
            foreach (var cell in room.Cells)
            {
                int x0 = cell.x * DungeonMap.CellWidth, y0 = room.CellOriginY(cell.y);
                foreach (int dx in new[] { 6, 13, 23, 30 })
                    result.Add(new RoomPropSpec { Kind = "room_f3_wall_shelf", X = x0 + dx,
                        Y = y0 + DungeonMap.CellHeight - 1, Layer = RoomPropLayer.WallObject });
                if (room.Role == RoomRole.Boss) continue;
                for (int i = 0; i < 20; i++)
                    result.Add(new RoomPropSpec { Kind = new[] { "room_f3_shards", "room_f3_gold", "room_f3_inlay" }[i % 3],
                        X = x0 + 3 + random.Next(DungeonMap.CellWidth - 6),
                        Y = y0 + 3 + random.Next(DungeonMap.CellHeight - 6),
                        Layer = RoomPropLayer.FloorDecal, Color = new Color(1, 1, 1, .45f) });
            }
            AddInteriorWallDecor(result, room, 2, random);
            foreach (var spec in result) spec.Owner = room;
            return result;
        }
    }
}
