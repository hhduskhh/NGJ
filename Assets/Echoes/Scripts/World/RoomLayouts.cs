using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public enum RoomPropLayer { FloorDecal, FloorObject, WallObject, Overhead }

    public sealed class RoomPropSpec
    {
        public string Kind;
        public int X, Y;
        public int Width = 1, Height = 1;
        public float Scale = 1f;
        public bool Solid;
        public RoomPropLayer Layer;
        public Color Color = Color.white;
        public DungeonMap.Room Owner;

        public Vector2 ArtPosition => new Vector2(X + (Width - 1) * .5f, Y - (Kind == StartStall.Kind ? .5f : 0));
        public Rect CollisionBounds { get; private set; }
        public RectInt NavigationBounds { get; private set; }

        public void MeasureBase()
        {
            var entry = RoomArt.Definition(Kind == StartStall.Kind ? "room_stall" : Kind);
            var sprite = RoomArt.Sprite(entry.Kind);
            var pixels = entry.BasePixels;
            float units = Scale / sprite.pixelsPerUnit;
            CollisionBounds = new Rect(ArtPosition + ((Vector2)pixels.position - sprite.pivot) * units,
                (Vector2)pixels.size * units);
            // Keep enough clearance at grid centres for the moving actors.
            const float clearance = .33f;
            var min = Vector2Int.CeilToInt(CollisionBounds.min - Vector2.one * clearance);
            var max = Vector2Int.FloorToInt(CollisionBounds.max + Vector2.one * clearance);
            NavigationBounds = new RectInt(min, max - min + Vector2Int.one);
        }
    }

    // P0 layout library: uses the existing 16 prop slices in denser clusters and
    // several room archetypes. P1 art can replace kinds/scales without changing rules.
    public static partial class RoomLayouts
    {
        private static readonly string[][] SolidPools =
        {
            new[] { "crate", "pillar", "station", "rack", "bench" },
            new[] { "crate", "station", "pipe", "pipeBend", "panel", "conduit" },
            new[] { "pillar", "crystal", "fork", "panel", "station", "conduit" }
        };
        private static readonly string[][] ClutterPools =
        {
            new[] { "spares", "pipeBend" },
            new[] { "spares", "pipeBend", "conduit" },
            new[] { "crystal", "fork", "spares" }
        };
        private static readonly string[][] WallPools =
        {
            new[] { "lamp", "signature" },
            new[] { "lamp", "panel" },
            new[] { "lamp", "signature" }
        };

        public static List<RoomPropSpec> Build(DungeonMap.Room room, int theme, System.Random random)
        {
            theme = Mathf.Clamp(theme, 0, 2);
            if (theme == 0)
                return BuildFirstFloor(room, random);
            if (theme == 1) return BuildSecondFloor(room, random);
            if (theme == 2) return BuildThirdFloor(room, random);
            var result = new List<RoomPropSpec>();
            if (room.Role == RoomRole.Boss) BuildBoss(result, room, theme, random);
            else if (room.Role == RoomRole.Middle) BuildMiddle(result, room, theme, random);
            AddWallDecor(result, room, theme, random);
            int decalCount = room.Role == RoomRole.Boss ? 0 : room.CellCount *
                (room.Role == RoomRole.Middle ? 6 : 3);
            int clutterCount = room.Role == RoomRole.Boss ? 0 : room.CellCount *
                (room.Role == RoomRole.Middle ? 12 : 6);
            AddFloorDecals(result, room, theme, random, decalCount);
            AddClutter(result, room, theme, random, clutterCount);
            foreach (var spec in result) spec.Owner = room;
            return result;
        }

        private static List<RoomPropSpec> BuildFirstFloor(DungeonMap.Room room, System.Random random)
        {
            var result = new List<RoomPropSpec>();
            var b = room.Bounds;
            if (room.Role == RoomRole.Boss)
            {
                foreach (float x in new[] { .17f, .83f })
                    foreach (float y in new[] { .17f, .83f })
                        Native(result, "boss_pillar", b, x, y);
            }
            else if (room.Role == RoomRole.Start)
            {
                Native(result, "pew", b, .25f, .80f);
                Native(result, "crate", b, .80f, .80f);
            }
            else
            {
                int variant = random.Next(3);
                foreach (var cell in room.Cells)
                {
                    int x0 = cell.x * DungeonMap.CellWidth;
                    int y0 = DungeonMap.CellOriginY(cell.y);
                    var cellBounds = new RectInt(x0, y0, DungeonMap.CellWidth, DungeonMap.CellHeight);
                    // The room shape supplies the outer plan; each cell gets a
                    // different station vignette with four broad bypasses.
                    // Three different silhouettes: bell-ring courtyard, staggered
                    // cargo lane, and broken-column court. Do not repeat a bell
                    // in every station room or rotate vertical architecture.
                    Native(result, variant == 0 ? "bell" : variant == 1 ? "crate" : "rubble",
                        cellBounds, .50f, .50f);
                    Native(result, "crate", cellBounds, .22f, .22f);
                    Native(result, "rubble", cellBounds, .78f, .78f);
                    Native(result, variant == 2 ? "rubble" : "crate", cellBounds, .22f, .78f);
                    Native(result, "crate", cellBounds, .78f, .22f);
                    bool vertical = room.Bounds.height > room.Bounds.width;
                    Native(result, vertical ? "pew_ns" : "pew", cellBounds, .28f, variant == 1 ? .36f : .40f);
                    Native(result, vertical ? "pew_ns" : "pew", cellBounds, .72f, variant == 1 ? .64f : .60f);
                    Native(result, "column", cellBounds, .16f, .50f);
                    Native(result, "column", cellBounds, .84f, .50f);
                    Native(result, "rubble", cellBounds, .50f, .20f);
                    Native(result, "crate", cellBounds, .50f, .80f);
                    if (variant == 2)
                    {
                        Native(result, "column", cellBounds, .36f, .36f);
                        Native(result, "column", cellBounds, .64f, .64f);
                    }
                }
            }
            foreach (var cell in room.Cells)
            {
                int x0 = cell.x * DungeonMap.CellWidth;
                int y0 = DungeonMap.CellOriginY(cell.y);
                // Upright wall sconces mount on north-facing walls only.
                // No sideways/flipped lighting fixtures on other walls.
                foreach (int dx in new[] { 6, 13, 23, 30 })
                    result.Add(new RoomPropSpec { Kind = "room_sconce", X = x0 + dx,
                        Y = y0 + DungeonMap.CellHeight - 1, Layer = RoomPropLayer.WallObject });
                if (room.Role == RoomRole.Boss) continue;
                for (int i = 0; i < 16; i++)
                    result.Add(new RoomPropSpec { Kind = new[] { "room_crack", "room_moss", "room_coins" }[i % 3],
                        X = x0 + 3 + random.Next(DungeonMap.CellWidth - 6),
                        Y = y0 + 3 + random.Next(DungeonMap.CellHeight - 6),
                        Layer = RoomPropLayer.FloorDecal, Color = new Color(1, 1, 1, .55f) });
            }
            AddInteriorWallDecor(result, room, 0, random);
            foreach (var spec in result) spec.Owner = room;
            return result;
        }

        private static void Native(List<RoomPropSpec> result, string kind, RectInt bounds, float x, float y)
        {
            var art = RoomArt.Definition("room_" + kind);
            Place(result, art.Kind, bounds, x, y, art.Footprint.x, art.Footprint.y, 1);
        }

        private static List<RoomPropSpec> BuildSecondFloor(DungeonMap.Room room, System.Random random)
        {
            var result = new List<RoomPropSpec>();
            var b = room.Bounds;
            if (room.Role == RoomRole.Start)
            {
                Native(result, "f2_tank", b, .22f, .80f);
                Native(result, "f2_pipes", b, .80f, .80f);
            }
            else if (room.Role == RoomRole.Boss)
            {
                foreach (float x in new[] { .17f, .83f })
                    foreach (float y in new[] { .17f, .83f })
                        Native(result, "f2_boss_pillar", b, x, y);
            }
            else
            {
                bool vertical = b.height > b.width;
                int focus = random.Next(room.Cells.Count);
                int variant = random.Next(3);
                for (int i = 0; i < room.Cells.Count; i++)
                {
                    var cell = room.Cells[i];
                    var area = new RectInt(cell.x * DungeonMap.CellWidth,
                        DungeonMap.CellOriginY(cell.y), DungeonMap.CellWidth, DungeonMap.CellHeight);
                    if (i == focus) Native(result, "f2_boiler", area, .50f, .50f);
                    else if (vertical)
                    {
                        Native(result, "f2_tank", area, .38f, .50f);
                        Native(result, "f2_tank", area, .62f, .50f);
                    }
                    else
                    {
                        Native(result, "f2_tank", area, .50f, .38f);
                        Native(result, "f2_tank", area, .50f, .62f);
                    }
                    // Boiler islands, double receiver bays and broken drive
                    // assemblies share clear side routes, but different outlines.
                    foreach (float x in new[] { .22f, .78f })
                        foreach (float y in new[] { .22f, .78f })
                            Native(result, (variant == 2 && x == y) ? "f2_gears" : "f2_tank", area, x, y);
                    Native(result, vertical ? "f2_conveyor_ns" : "f2_conveyor", area, .15f, .50f);
                    Native(result, vertical ? "f2_pipes_ns" : "f2_pipes", area, .85f, .50f);
                    Native(result, "f2_gears", area, .50f, .20f);
                    Native(result, "f2_gears", area, .50f, .80f);
                    if (room.Shape == RoomShape.L && i != focus)
                        Native(result, "f2_pipes", area, .28f, .62f);
                }
            }
            foreach (var cell in room.Cells)
            {
                int x0 = cell.x * DungeonMap.CellWidth, y0 = DungeonMap.CellOriginY(cell.y);
                foreach (int dx in new[] { 6, 13, 23, 30 })
                    result.Add(new RoomPropSpec { Kind = "room_f2_wall_pipes", X = x0 + dx,
                        Y = y0 + DungeonMap.CellHeight - 1, Layer = RoomPropLayer.WallObject });
                if (room.Role == RoomRole.Boss) continue;
                for (int i = 0; i < 16; i++)
                    result.Add(new RoomPropSpec { Kind = new[] { "room_f2_oil", "room_f2_water", "room_f2_scrap" }[i % 3],
                        X = x0 + 3 + random.Next(DungeonMap.CellWidth - 6),
                        Y = y0 + 3 + random.Next(DungeonMap.CellHeight - 6),
                        Layer = RoomPropLayer.FloorDecal, Color = new Color(1, 1, 1, .55f) });
            }
            AddInteriorWallDecor(result, room, 1, random);
            foreach (var spec in result) spec.Owner = room;
            return result;
        }

        private static void BuildBoss(List<RoomPropSpec> result, DungeonMap.Room room, int theme,
            System.Random random)
        {
            var kind = SolidPools[theme][random.Next(SolidPools[theme].Length)];
            // Four corner cover blocks only; the central arena stays open.
            Place(result, kind, room.Bounds, .16f, .16f, 3, 3, 1.5f);
            Place(result, kind, room.Bounds, .84f, .16f, 3, 3, 1.5f);
            Place(result, kind, room.Bounds, .16f, .84f, 3, 3, 1.5f);
            Place(result, kind, room.Bounds, .84f, .84f, 3, 3, 1.5f);
        }

        private static void BuildMiddle(List<RoomPropSpec> result, DungeonMap.Room room, int theme,
            System.Random random)
        {
            bool horizontal = room.Bounds.width >= room.Bounds.height;
            switch (room.Shape)
            {
                case RoomShape.Single:
                    BuildSingle(result, room, theme, random);
                    break;
                case RoomShape.Line2:
                    BuildLine2(result, room, theme, random, horizontal);
                    break;
                case RoomShape.Line3:
                    BuildLine3(result, room, theme, random, horizontal);
                    break;
                case RoomShape.Square2:
                    BuildSquare2(result, room, theme, random);
                    break;
                case RoomShape.L:
                    BuildL(result, room, theme, random);
                    break;
            }
        }

        private static void BuildSingle(List<RoomPropSpec> result, DungeonMap.Room room, int theme,
            System.Random random)
        {
            var b = room.Bounds;
            var pool = SolidPools[theme];
            var hero = Pick(pool, random);
            var crate = theme == 1 ? "crate" : Pick(pool, random);
            switch (random.Next(3))
            {
                case 0: // donut
                    Place(result, hero, b, .50f, .50f, 8, 8, 4f);
                    Place(result, crate, b, .18f, .18f, 3, 3, 1.5f);
                    Place(result, crate, b, .82f, .18f, 3, 3, 1.5f);
                    Place(result, crate, b, .18f, .82f, 3, 3, 1.5f);
                    Place(result, crate, b, .82f, .82f, 3, 3, 1.5f);
                    Place(result, "bench", b, .50f, .14f, 5, 1, 1f);
                    Place(result, "bench", b, .50f, .86f, 5, 1, 1f);
                    Place(result, "pillar", b, .15f, .50f, 2, 2, 1f);
                    Place(result, "pillar", b, .85f, .50f, 2, 2, 1f);
                    break;
                case 1: // cargo S
                    Place(result, crate, b, .20f, .18f, 4, 4, 2f);
                    Place(result, crate, b, .80f, .18f, 4, 4, 2f);
                    Place(result, crate, b, .38f, .50f, 4, 4, 2f);
                    Place(result, crate, b, .62f, .50f, 4, 4, 2f);
                    Place(result, crate, b, .20f, .82f, 4, 4, 2f);
                    Place(result, crate, b, .80f, .82f, 4, 4, 2f);
                    Place(result, "bench", b, .50f, .15f, 5, 1, 1f);
                    Place(result, "bench", b, .50f, .85f, 5, 1, 1f);
                    Place(result, "pillar", b, .50f, .36f, 2, 2, 1f);
                    Place(result, "pillar", b, .50f, .64f, 2, 2, 1f);
                    break;
                default: // broken corners
                    Place(result, "pillar", b, .16f, .16f, 3, 3, 1.5f);
                    Place(result, "pillar", b, .84f, .16f, 3, 3, 1.5f);
                    Place(result, crate, b, .16f, .84f, 4, 4, 2f);
                    Place(result, crate, b, .84f, .84f, 3, 3, 1.5f);
                    Place(result, "pillar", b, .50f, .50f, 3, 3, 1.5f);
                    Place(result, crate, b, .50f, .18f, 4, 2, 2f);
                    Place(result, "bench", b, .22f, .50f, 4, 1, 1f);
                    Place(result, "bench", b, .78f, .50f, 4, 1, 1f);
                    Place(result, crate, b, .40f, .78f, 3, 3, 1.5f);
                    break;
            }
        }

        private static void BuildLine2(List<RoomPropSpec> result, DungeonMap.Room room, int theme,
            System.Random random, bool horizontal)
        {
            var b = room.Bounds;
            var pool = SolidPools[theme];
            var hero = Pick(pool, random);
            var crate = theme == 1 ? "crate" : Pick(pool, random);
            switch (random.Next(3))
            {
                case 0: // double hall
                    Place(result, hero, b, .50f, .50f, 8, 8, 4f);
                    Place(result, "pillar", b, .20f, .50f, 3, 3, 1.5f);
                    Place(result, "pillar", b, .80f, .50f, 3, 3, 1.5f);
                    Place(result, crate, b, .16f, .20f, 3, 3, 1.5f);
                    Place(result, crate, b, .84f, .20f, 3, 3, 1.5f);
                    Place(result, crate, b, .16f, .80f, 3, 3, 1.5f);
                    Place(result, crate, b, .84f, .80f, 3, 3, 1.5f);
                    Place(result, "bench", b, .50f, .16f, 6, 1, 1f);
                    Place(result, "bench", b, .50f, .84f, 6, 1, 1f);
                    break;
                case 1: // pillar hall
                    for (int i = 0; i < 4; i++)
                    {
                        float f = .16f + i * .226f;
                        Place(result, "pillar", b, f, .22f, 3, 3, 1.5f);
                        Place(result, "pillar", b, f, .78f, 3, 3, 1.5f);
                        Place(result, crate, b, f, .50f, 3, 3, 1.5f);
                    }
                    Place(result, hero, b, .50f, .50f, 6, 6, 3f);
                    break;
                default: // cargo lanes
                    for (int i = 0; i < 4; i++)
                    {
                        float f = .18f + i * .213f;
                        Place(result, crate, b, f, i % 2 == 0 ? .28f : .72f, 4, 4, 2f);
                        Place(result, "bench", b, f, i % 2 == 0 ? .50f : .50f, 4, 1, 1f);
                    }
                    Place(result, "pillar", b, .50f, .50f, 3, 3, 1.5f);
                    Place(result, hero, b, .80f, .50f, 5, 5, 2.5f);
                    break;
            }
            Place(result, crate, b, .30f, .35f, 4, 4, 2f);
            Place(result, crate, b, .70f, .65f, 4, 4, 2f);
            Place(result, "bench", b, .30f, .65f, 5, 1, 1.2f);
            Place(result, "bench", b, .70f, .35f, 5, 1, 1.2f);
            Place(result, crate, b, .18f, .50f, 5, 5, 2.5f);
            Place(result, crate, b, .82f, .50f, 5, 5, 2.5f);
            Place(result, crate, b, .50f, .22f, 5, 5, 2.5f);
            Place(result, crate, b, .50f, .78f, 5, 5, 2.5f);
        }

        private static void BuildLine3(List<RoomPropSpec> result, DungeonMap.Room room, int theme,
            System.Random random, bool horizontal)
        {
            var b = room.Bounds;
            var pool = SolidPools[theme];
            var hero = Pick(pool, random);
            var crate = theme == 1 ? "crate" : Pick(pool, random);
            switch (random.Next(3))
            {
                case 0: // long hall
                    for (int i = 0; i < 5; i++)
                    {
                        float f = .12f + i * .19f;
                        Place(result, "pillar", b, horizontal ? f : .24f, horizontal ? .24f : f, 3, 3, 1.5f);
                        Place(result, "pillar", b, horizontal ? f : .76f, horizontal ? .76f : f, 3, 3, 1.5f);
                    }
                    Place(result, hero, b, horizontal ? .50f : .50f, horizontal ? .50f : .50f, 8, 8, 4f);
                    Place(result, "bench", b, horizontal ? .30f : .50f, horizontal ? .50f : .30f, 5, 1, 1f);
                    Place(result, "bench", b, horizontal ? .70f : .50f, horizontal ? .50f : .70f, 5, 1, 1f);
                    break;
                case 1: // side alcoves
                    for (int i = 0; i < 4; i++)
                    {
                        float f = .18f + i * .21f;
                        Place(result, "pillar", b, horizontal ? f : .16f, horizontal ? .16f : f, 3, 3, 1.5f);
                        Place(result, "pillar", b, horizontal ? f : .84f, horizontal ? .84f : f, 3, 3, 1.5f);
                    }
                    Place(result, hero, b, .50f, .50f, 9, 9, 4.5f);
                    Place(result, "bench", b, .50f, .50f, 6, 1, 1f);
                    Place(result, "crate", b, .28f, .50f, 4, 4, 2f);
                    Place(result, "crate", b, .72f, .50f, 4, 4, 2f);
                    break;
                default: // broken hall
                    Place(result, hero, b, .50f, .50f, 10, 10, 5f);
                    Place(result, "crate", b, .22f, .30f, 5, 5, 2.5f);
                    Place(result, "crate", b, .78f, .70f, 5, 5, 2.5f);
                    for (int i = 0; i < 4; i++)
                    {
                        float f = .16f + i * .226f;
                        Place(result, "pillar", b, f, .16f, 3, 3, 1.5f);
                        Place(result, "pillar", b, f, .84f, 3, 3, 1.5f);
                    }
                    break;
            }
            for (int i = 0; i < 4; i++)
            {
                float f = .18f + i * .21f;
                Place(result, crate, b, horizontal ? .30f : f, horizontal ? f : .30f, 4, 4, 2f);
                Place(result, crate, b, horizontal ? .70f : f, horizontal ? f : .70f, 4, 4, 2f);
            }
            for (int i = 0; i < 4; i++)
            {
                float f = .18f + i * .21f;
                Place(result, crate, b, horizontal ? .18f : f, horizontal ? f : .18f, 5, 5, 2.5f);
                Place(result, crate, b, horizontal ? .82f : f, horizontal ? f : .82f, 5, 5, 2.5f);
            }
        }

        private static void BuildSquare2(List<RoomPropSpec> result, DungeonMap.Room room, int theme,
            System.Random random)
        {
            var b = room.Bounds;
            var pool = SolidPools[theme];
            var hero = Pick(pool, random);
            var crate = theme == 1 ? "crate" : Pick(pool, random);
            switch (random.Next(3))
            {
                case 0: // courtyard
                    Place(result, hero, b, .50f, .50f, 9, 9, 4.5f);
                    Place(result, crate, b, .18f, .18f, 5, 5, 2.5f);
                    Place(result, crate, b, .82f, .18f, 5, 5, 2.5f);
                    Place(result, crate, b, .18f, .82f, 5, 5, 2.5f);
                    Place(result, crate, b, .82f, .82f, 5, 5, 2.5f);
                    Place(result, "pillar", b, .50f, .15f, 3, 3, 1.5f);
                    Place(result, "pillar", b, .50f, .85f, 3, 3, 1.5f);
                    Place(result, "pillar", b, .15f, .50f, 3, 3, 1.5f);
                    Place(result, "pillar", b, .85f, .50f, 3, 3, 1.5f);
                    break;
                case 1: // crossroads
                    Place(result, hero, b, .50f, .50f, 6, 6, 3f);
                    Place(result, crate, b, .28f, .28f, 5, 4, 2.5f);
                    Place(result, crate, b, .72f, .28f, 5, 4, 2.5f);
                    Place(result, crate, b, .28f, .72f, 5, 4, 2.5f);
                    Place(result, crate, b, .72f, .72f, 5, 4, 2.5f);
                    Place(result, "bench", b, .50f, .16f, 6, 1, 1f);
                    Place(result, "bench", b, .50f, .84f, 6, 1, 1f);
                    Place(result, "bench", b, .16f, .50f, 6, 1, 1f);
                    Place(result, "bench", b, .84f, .50f, 6, 1, 1f);
                    break;
                default: // ring
                    Place(result, hero, b, .50f, .50f, 10, 10, 5f);
                    Place(result, "pillar", b, .20f, .20f, 4, 4, 2f);
                    Place(result, "pillar", b, .80f, .20f, 4, 4, 2f);
                    Place(result, "pillar", b, .20f, .80f, 4, 4, 2f);
                    Place(result, "pillar", b, .80f, .80f, 4, 4, 2f);
                    Place(result, crate, b, .50f, .16f, 4, 3, 2f);
                    Place(result, crate, b, .50f, .84f, 4, 3, 2f);
                    break;
            }
            Place(result, crate, b, .32f, .32f, 4, 4, 2f);
            Place(result, crate, b, .68f, .32f, 4, 4, 2f);
            Place(result, crate, b, .32f, .68f, 4, 4, 2f);
            Place(result, crate, b, .68f, .68f, 4, 4, 2f);
            Place(result, "pillar", b, .50f, .28f, 3, 3, 1.5f);
            Place(result, "pillar", b, .50f, .72f, 3, 3, 1.5f);
            Place(result, crate, b, .20f, .20f, 6, 6, 3f);
            Place(result, crate, b, .80f, .20f, 6, 6, 3f);
            Place(result, crate, b, .20f, .80f, 6, 6, 3f);
            Place(result, crate, b, .80f, .80f, 6, 6, 3f);
        }

        private static void BuildL(List<RoomPropSpec> result, DungeonMap.Room room, int theme,
            System.Random random)
        {
            var pool = SolidPools[theme];
            var hero = Pick(pool, random);
            var crate = theme == 1 ? "crate" : Pick(pool, random);
            var cells = room.Cells;
            if (cells.Count == 0) return;
            var shrine = cells[random.Next(cells.Count)];
            foreach (var cell in cells)
            {
                var center = CellCenter(cell);
                if (cell == shrine) PlaceAtTile(result, hero, center.x, center.y, 8, 8, 4f);
                else PlaceAtTile(result, crate, center.x, center.y, 5, 5, 2.5f);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        PlaceAtTile(result, "pillar", center.x + sx * 10, center.y + sy * 10,
                            3, 3, 1.5f);
                PlaceAtTile(result, "bench", center.x, center.y - 12, 6, 1, 1.2f);
                PlaceAtTile(result, "bench", center.x, center.y + 12, 6, 1, 1.2f);
            }
            // One asymmetric collapsed cluster on a valid cell.
            var extra = cells[random.Next(cells.Count)];
            var extraCenter = CellCenter(extra);
            PlaceAtTile(result, crate, extraCenter.x + 6, extraCenter.y - 6, 5, 5, 2.5f);
            var extra2 = cells[random.Next(cells.Count)];
            var extra2Center = CellCenter(extra2);
            PlaceAtTile(result, crate, extra2Center.x - 7, extra2Center.y + 7, 5, 5, 2.5f);
        }

        private static Vector2Int CellCenter(Vector2Int cell) => new Vector2Int(
            cell.x * DungeonMap.CellWidth + DungeonMap.CellWidth / 2,
            DungeonMap.CellOriginY(cell.y) + DungeonMap.CellHeight / 2);

        private static void PlaceAtTile(List<RoomPropSpec> result, string kind,
            int cx, int cy, int w, int h, float scale)
        {
            if (kind == "bench") w = Mathf.Min(w, Mathf.CeilToInt(2 * scale));
            result.Add(new RoomPropSpec
            {
                Kind = kind, X = cx - w / 2, Y = cy - h / 2,
                Width = w, Height = h, Scale = scale,
                Solid = true, Layer = RoomPropLayer.FloorObject
            });
        }

        // Dress the new interior partitions with the floor's wall fixtures.
        // Sampling every few tiles keeps long screens readable without
        // covering them end to end.
        private static void AddInteriorWallDecor(List<RoomPropSpec> result,
            DungeonMap.Room room, int theme, System.Random random)
        {
            string kind = theme == 0 ? "room_sconce" :
                theme == 1 ? "room_f2_wall_pipes" : "room_f3_wall_shelf";
            var taken = new HashSet<Vector2Int>();
            int placed = 0;
            foreach (var wall in room.Walls)
            {
                bool horizontal = wall.width >= wall.height;
                int length = horizontal ? wall.width : wall.height;
                if (length < 4) continue;
                for (int offset = 2; offset < length - 1; offset += 7)
                {
                    var tile = horizontal
                        ? new Vector2Int(wall.xMin + offset, wall.yMin + wall.height / 2)
                        : new Vector2Int(wall.xMin + wall.width / 2, wall.yMin + offset);
                    if (!taken.Add(tile)) continue;
                    result.Add(new RoomPropSpec
                    {
                        Kind = kind, X = tile.x, Y = tile.y,
                        Layer = RoomPropLayer.WallObject
                    });
                    if (++placed >= 24) return;
                }
            }
        }

        private static void AddWallDecor(List<RoomPropSpec> result, DungeonMap.Room room,
            int theme, System.Random random)
        {
            var pool = WallPools[theme];
            foreach (var cell in room.Cells)
            {
                int x0 = cell.x * DungeonMap.CellWidth;
                int y0 = DungeonMap.CellOriginY(cell.y);
                int top = y0 + DungeonMap.CellHeight - 1;
                int right = x0 + DungeonMap.CellWidth - 1;
                AddWall(result, pool, x0 + 5, top, random);
                AddWall(result, pool, x0 + 18, top, random);
                AddWall(result, pool, x0 + 31, top, random);
                AddWall(result, pool, x0, y0 + 8, random);
                AddWall(result, pool, x0, y0 + 27, random);
                AddWall(result, pool, right, y0 + 8, random);
                AddWall(result, pool, right, y0 + 27, random);
                AddWall(result, pool, x0 + 9, y0, random);
            }
        }

        private static void AddWall(List<RoomPropSpec> result, string[] pool,
            int x, int y, System.Random random)
        {
            result.Add(new RoomPropSpec
            {
                Kind = pool[random.Next(pool.Length)],
                X = x, Y = y, Layer = RoomPropLayer.WallObject
            });
        }

        private static void AddFloorDecals(List<RoomPropSpec> result, DungeonMap.Room room,
            int theme, System.Random random, int count)
        {
            var color = theme == 0 ? new Color(.30f, .24f, .38f, .30f) :
                theme == 1 ? new Color(.20f, .14f, .10f, .38f) :
                new Color(.28f, .40f, .55f, .28f);
            for (int i = 0; i < count; i++)
            {
                var cell = room.Cells[random.Next(room.Cells.Count)];
                int x0 = cell.x * DungeonMap.CellWidth;
                int y0 = DungeonMap.CellOriginY(cell.y);
                result.Add(new RoomPropSpec
                {
                    Kind = random.Next(3) == 0 ? "floorDetail" : "floorAlt",
                    X = x0 + 3 + random.Next(DungeonMap.CellWidth - 6),
                    Y = y0 + 3 + random.Next(DungeonMap.CellHeight - 6),
                    Layer = RoomPropLayer.FloorDecal,
                    Scale = 1f,
                    Color = color
                });
            }
        }

        private static void AddClutter(List<RoomPropSpec> result, DungeonMap.Room room,
            int theme, System.Random random, int count)
        {
            var pool = ClutterPools[theme];
            for (int i = 0; i < count; i++)
            {
                var cell = room.Cells[random.Next(room.Cells.Count)];
                int x0 = cell.x * DungeonMap.CellWidth;
                int y0 = DungeonMap.CellOriginY(cell.y);
                result.Add(new RoomPropSpec
                {
                    Kind = pool[random.Next(pool.Length)],
                    X = x0 + 3 + random.Next(DungeonMap.CellWidth - 6),
                    Y = y0 + 3 + random.Next(DungeonMap.CellHeight - 6),
                    Layer = RoomPropLayer.FloorObject,
                    Scale = .7f + (float)random.NextDouble() * .5f
                });
            }
        }

        private static string Pick(string[] pool, System.Random random) =>
            pool[random.Next(pool.Length)];

        private static void Place(List<RoomPropSpec> result, string kind, RectInt bounds,
            float fx, float fy, int w, int h, float scale)
        {
            // P0 benches are a 2-unit-wide sprite, not a 4-6-unit furniture asset.
            // Use the same corrected footprint for placement, movement and vision.
            if (kind == "bench") w = Mathf.Min(w, Mathf.CeilToInt(2 * scale));
            int cx = bounds.x + Mathf.RoundToInt(bounds.width * fx);
            int cy = bounds.y + Mathf.RoundToInt(bounds.height * fy);
            result.Add(new RoomPropSpec
            {
                Kind = kind,
                X = cx - w / 2,
                Y = cy - h / 2,
                Width = w,
                Height = h,
                Scale = scale,
                Solid = true,
                Layer = RoomPropLayer.FloorObject
            });
        }
    }
}
