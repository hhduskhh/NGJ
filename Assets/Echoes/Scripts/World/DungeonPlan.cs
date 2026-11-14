using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public enum RoomRole { Start, Middle, Boss }
    public enum RoomShape { Single, Line2, Line3, Square2, L }

    // Pure layout data: a 5x5 cell grid fully tiled by the five requested room shapes.
    // The rasterizer in DungeonMap turns this into world tiles, walls and doors.
    public sealed class PlannedRoom
    {
        public int Index;
        public RoomRole Role;
        public RoomShape Shape;
        public int Rotation;
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();
        public Vector2Int MinCell;
        public Vector2Int MaxCell;
        public readonly List<PlannedDoor> Doors = new List<PlannedDoor>();
        public int CellCount { get { return Cells.Count; } }

        public bool ContainsCell(Vector2Int cell)
        {
            for (int i = 0; i < Cells.Count; i++) if (Cells[i] == cell) return true;
            return false;
        }
    }

    public sealed class PlannedDoor
    {
        public PlannedRoom A;
        public PlannedRoom B;
        public Vector2Int CellA;
        public Vector2Int CellB;
    }

    public sealed class DungeonPlan
    {
        public const int GridSize = 5;
        public static readonly Vector2Int StartCell = new Vector2Int(2, 0);
        public static readonly Vector2Int BossCell = new Vector2Int(2, 4);

        public PlannedRoom[,] CellRooms { get; private set; }
        public List<PlannedRoom> Rooms { get; private set; }
        public List<PlannedDoor> Doors { get; private set; }
        public PlannedRoom Start { get { return Rooms[0]; } }
        public PlannedRoom Boss { get { return Rooms[Rooms.Count - 1]; } }
        public bool Valid { get; private set; }

        private sealed class ShapeDef
        {
            public RoomShape Shape;
            public float Weight;
            public readonly List<Vector2Int[]> Rotations = new List<Vector2Int[]>();
        }

        private sealed class Placement
        {
            public RoomShape Shape;
            public int Rotation;
            public Vector2Int Origin;
            public Vector2Int[] LocalCells;
            public float SortKey;
        }

        private sealed class CellPair
        {
            public Vector2Int A;
            public Vector2Int B;
        }

        private sealed class RoomEdge
        {
            public PlannedRoom A;
            public PlannedRoom B;
            public readonly List<CellPair> Candidates = new List<CellPair>();
            public Vector2Int CellA;
            public Vector2Int CellB;
        }

        private static readonly ShapeDef[] Shapes = BuildShapes();

        public DungeonPlan()
        {
            CellRooms = new PlannedRoom[GridSize, GridSize];
            Rooms = new List<PlannedRoom>();
            Doors = new List<PlannedDoor>();
        }

        public static DungeonPlan Generate(int seed, int maxAttempts = 100, float extraDoorChance = .15f)
        {
            for (int attempt = 0; attempt < Math.Max(1, maxAttempts); attempt++)
            {
                var plan = new DungeonPlan();
                var random = new System.Random(seed + attempt * 7919);
                if (plan.TryBuild(random, extraDoorChance)) return plan;
            }
            return BuildFallback(seed);
        }

        private bool TryBuild(System.Random random, float extraDoorChance)
        {
            var occupancy = new PlannedRoom[GridSize, GridSize];
            var start = CreateRoom(RoomRole.Start, RoomShape.Single, 0, new[] { StartCell });
            var boss = CreateRoom(RoomRole.Boss, RoomShape.Single, 0, new[] { BossCell });
            occupancy[StartCell.x, StartCell.y] = start;
            occupancy[BossCell.x, BossCell.y] = boss;

            var middles = new List<PlannedRoom>();
            int steps = 0;
            if (!Fill(occupancy, middles, random, ref steps)) return false;
            if (DistinctMultiCellShapes(middles) < 2) return false;
            if (!Assemble(occupancy, start, boss, middles)) return false;
            if (!BuildDoors(random, extraDoorChance)) return false;
            Valid = true;
            return true;
        }

        private bool Fill(PlannedRoom[,] occupancy, List<PlannedRoom> middles, System.Random random, ref int steps)
        {
            if (++steps > 30000) return false;
            int emptyCol = -1, emptyRow = -1;
            for (int row = 0; row < GridSize && emptyCol < 0; row++)
                for (int col = 0; col < GridSize; col++)
                    if (occupancy[col, row] == null) { emptyCol = col; emptyRow = row; break; }
            if (emptyCol < 0) return true;

            var placements = new List<Placement>();
            foreach (var shape in Shapes)
            {
                for (int rotation = 0; rotation < shape.Rotations.Count; rotation++)
                {
                    var cells = shape.Rotations[rotation];
                    for (int anchor = 0; anchor < cells.Length; anchor++)
                    {
                        var origin = new Vector2Int(emptyCol - cells[anchor].x, emptyRow - cells[anchor].y);
                        if (!Fits(occupancy, cells, origin)) continue;
                        placements.Add(new Placement
                        {
                            Shape = shape.Shape,
                            Rotation = rotation,
                            Origin = origin,
                            LocalCells = cells,
                            // Weighted sampling without replacement; larger shapes get lower keys.
                            SortKey = (float)(-Math.Log(1.0 - random.NextDouble()) / shape.Weight)
                        });
                    }
                }
            }
            placements.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));

            var seen = new HashSet<string>();
            foreach (var placement in placements)
            {
                string key = placement.Shape + ":" + placement.Rotation + ":" +
                    placement.Origin.x + "," + placement.Origin.y;
                if (!seen.Add(key)) continue;

                var absolute = new Vector2Int[placement.LocalCells.Length];
                for (int i = 0; i < absolute.Length; i++) absolute[i] = placement.Origin + placement.LocalCells[i];
                var room = CreateRoom(RoomRole.Middle, placement.Shape, placement.Rotation, absolute);
                for (int i = 0; i < absolute.Length; i++) occupancy[absolute[i].x, absolute[i].y] = room;
                middles.Add(room);

                if (Fill(occupancy, middles, random, ref steps)) return true;

                middles.RemoveAt(middles.Count - 1);
                for (int i = 0; i < absolute.Length; i++) occupancy[absolute[i].x, absolute[i].y] = null;
            }
            return false;
        }

        private static bool Fits(PlannedRoom[,] occupancy, Vector2Int[] cells, Vector2Int origin)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                int x = origin.x + cells[i].x;
                int y = origin.y + cells[i].y;
                if (x < 0 || y < 0 || x >= GridSize || y >= GridSize) return false;
                if (occupancy[x, y] != null) return false;
            }
            return true;
        }

        private static PlannedRoom CreateRoom(RoomRole role, RoomShape shape, int rotation, Vector2Int[] cells)
        {
            var room = new PlannedRoom { Role = role, Shape = shape, Rotation = rotation };
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int i = 0; i < cells.Length; i++)
            {
                room.Cells.Add(cells[i]);
                minX = Math.Min(minX, cells[i].x); maxX = Math.Max(maxX, cells[i].x);
                minY = Math.Min(minY, cells[i].y); maxY = Math.Max(maxY, cells[i].y);
            }
            room.MinCell = new Vector2Int(minX, minY);
            room.MaxCell = new Vector2Int(maxX, maxY);
            return room;
        }

        private bool Assemble(PlannedRoom[,] occupancy, PlannedRoom start, PlannedRoom boss,
            List<PlannedRoom> middles)
        {
            middles.Sort((a, b) => a.MinCell.y != b.MinCell.y ?
                a.MinCell.y.CompareTo(b.MinCell.y) : a.MinCell.x.CompareTo(b.MinCell.x));
            Rooms.Clear();
            Rooms.Add(start);
            Rooms.AddRange(middles);
            Rooms.Add(boss);
            for (int i = 0; i < Rooms.Count; i++) Rooms[i].Index = i;
            for (int row = 0; row < GridSize; row++)
                for (int col = 0; col < GridSize; col++)
                {
                    CellRooms[col, row] = occupancy[col, row];
                    if (CellRooms[col, row] == null) return false;
                }
            return true;
        }

        private static int DistinctMultiCellShapes(List<PlannedRoom> rooms)
        {
            var kinds = new HashSet<RoomShape>();
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Shape != RoomShape.Single) kinds.Add(rooms[i].Shape);
            return kinds.Count;
        }

        private bool BuildDoors(System.Random random, float extraDoorChance)
        {
            var edges = new List<RoomEdge>();
            var lookup = new Dictionary<long, RoomEdge>();
            for (int row = 0; row < GridSize; row++)
                for (int col = 0; col < GridSize; col++)
                {
                    var room = CellRooms[col, row];
                    if (col + 1 < GridSize)
                        AddEdge(edges, lookup, room, CellRooms[col + 1, row],
                            new Vector2Int(col, row), new Vector2Int(col + 1, row));
                    if (row + 1 < GridSize)
                        AddEdge(edges, lookup, room, CellRooms[col, row + 1],
                            new Vector2Int(col, row), new Vector2Int(col, row + 1));
                }
            for (int i = 0; i < edges.Count; i++)
                if (!ChooseCellPair(edges[i], random)) return false;

            var startEdge = ChooseLeafEdge(Start, new Vector2Int(0, 1), edges, random);
            var bossEdge = ChooseLeafEdge(Boss, new Vector2Int(0, -1), edges, random);
            if (startEdge == null || bossEdge == null || startEdge == bossEdge) return false;

            int count = Rooms.Count;
            var parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;
            Func<int, int> find = null;
            find = x => parent[x] == x ? x : (parent[x] = find(parent[x]));

            AddDoor(startEdge);
            parent[find(startEdge.A.Index)] = find(startEdge.B.Index);
            AddDoor(bossEdge);
            parent[find(bossEdge.A.Index)] = find(bossEdge.B.Index);

            var used = new HashSet<RoomEdge> { startEdge, bossEdge };
            var remaining = new List<RoomEdge>();
            for (int i = 0; i < edges.Count; i++)
                if (!used.Contains(edges[i]) && !Touches(edges[i], Start) && !Touches(edges[i], Boss))
                    remaining.Add(edges[i]);
            Shuffle(remaining, random);

            for (int i = 0; i < remaining.Count; i++)
            {
                var edge = remaining[i];
                int a = find(edge.A.Index), b = find(edge.B.Index);
                if (a == b || edge.A.Doors.Count >= 4 || edge.B.Doors.Count >= 4) continue;
                AddDoor(edge); used.Add(edge); parent[a] = b;
            }
            for (int i = 1; i < count; i++) if (find(i) != find(0)) return false;

            for (int i = 0; i < remaining.Count; i++)
            {
                var edge = remaining[i];
                if (used.Contains(edge)) continue;
                if (edge.A.Doors.Count >= 4 || edge.B.Doors.Count >= 4) continue;
                if (random.NextDouble() >= extraDoorChance) continue;
                AddDoor(edge); used.Add(edge);
            }
            Valid = true;
            return true;
        }

        private static void AddEdge(List<RoomEdge> edges, Dictionary<long, RoomEdge> lookup,
            PlannedRoom a, PlannedRoom b, Vector2Int cellA, Vector2Int cellB)
        {
            if (a == b) return;
            PlannedRoom first = a.Index < b.Index ? a : b;
            PlannedRoom second = a.Index < b.Index ? b : a;
            Vector2Int firstCell = a.Index < b.Index ? cellA : cellB;
            Vector2Int secondCell = a.Index < b.Index ? cellB : cellA;
            long key = (long)first.Index * 1000L + second.Index;
            RoomEdge edge;
            if (!lookup.TryGetValue(key, out edge))
            {
                edge = new RoomEdge { A = first, B = second };
                lookup[key] = edge;
                edges.Add(edge);
            }
            edge.Candidates.Add(new CellPair { A = firstCell, B = secondCell });
        }

        private static bool ChooseCellPair(RoomEdge edge, System.Random random)
        {
            if (edge.Candidates.Count == 0) return false;
            var pair = edge.Candidates[random.Next(edge.Candidates.Count)];
            edge.CellA = pair.A; edge.CellB = pair.B;
            return true;
        }

        private static RoomEdge ChooseLeafEdge(PlannedRoom room, Vector2Int preferredDirection,
            List<RoomEdge> edges, System.Random random)
        {
            RoomEdge fallback = null;
            var preferred = new List<RoomEdge>();
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge.A != room && edge.B != room) continue;
                if (fallback == null) fallback = edge;
                for (int c = 0; c < edge.Candidates.Count; c++)
                {
                    var candidate = edge.Candidates[c];
                    Vector2Int own = edge.A == room ? candidate.A : candidate.B;
                    Vector2Int other = edge.A == room ? candidate.B : candidate.A;
                    if (other - own != preferredDirection) continue;
                    edge.CellA = candidate.A; edge.CellB = candidate.B;
                    preferred.Add(edge);
                    break;
                }
            }
            if (preferred.Count > 0) return preferred[random.Next(preferred.Count)];
            return fallback;
        }

        private void AddDoor(RoomEdge edge)
        {
            var door = new PlannedDoor
            {
                A = edge.A, B = edge.B,
                CellA = edge.CellA, CellB = edge.CellB
            };
            edge.A.Doors.Add(door);
            edge.B.Doors.Add(door);
            Doors.Add(door);
        }

        private static bool Touches(RoomEdge edge, PlannedRoom room) =>
            edge.A == room || edge.B == room;

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T value = list[i]; list[i] = list[j]; list[j] = value;
            }
        }

        private static ShapeDef[] BuildShapes()
        {
            var defs = new List<ShapeDef>();
            AddShape(defs, RoomShape.Single, 12, new[] { new Vector2Int(0, 0) });
            AddShape(defs, RoomShape.Line2, 22, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) });
            AddShape(defs, RoomShape.Line3, 16, new[]
                { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) });
            AddShape(defs, RoomShape.Square2, 24, new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 1)
            });
            AddShape(defs, RoomShape.L, 20, new[]
                { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) });
            return defs.ToArray();
        }

        private static void AddShape(List<ShapeDef> defs, RoomShape shape, float weight, Vector2Int[] cells)
        {
            var def = new ShapeDef { Shape = shape, Weight = weight };
            var current = Normalize(cells);
            for (int rotation = 0; rotation < 4; rotation++)
            {
                bool duplicate = false;
                for (int i = 0; i < def.Rotations.Count; i++)
                    if (SameCells(def.Rotations[i], current)) { duplicate = true; break; }
                if (!duplicate) def.Rotations.Add(current);
                var rotated = new Vector2Int[current.Length];
                for (int i = 0; i < current.Length; i++)
                    rotated[i] = new Vector2Int(-current[i].y, current[i].x);
                current = Normalize(rotated);
            }
            defs.Add(def);
        }

        private static Vector2Int[] Normalize(Vector2Int[] cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            for (int i = 0; i < cells.Length; i++)
            {
                minX = Math.Min(minX, cells[i].x);
                minY = Math.Min(minY, cells[i].y);
            }
            var result = new Vector2Int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                result[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);
            Array.Sort(result, (a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return result;
        }

        private static bool SameCells(Vector2Int[] a, Vector2Int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        // A deterministic tiling used only if every randomized attempt fails the quotas.
        private static DungeonPlan BuildFallback(int seed)
        {
            var plan = new DungeonPlan();
            var occupancy = new PlannedRoom[GridSize, GridSize];
            var start = CreateRoom(RoomRole.Start, RoomShape.Single, 0, new[] { StartCell });
            var boss = CreateRoom(RoomRole.Boss, RoomShape.Single, 0, new[] { BossCell });
            var middles = new List<PlannedRoom>();
            AddFallbackRoom(occupancy, middles, RoomShape.Square2, 0, new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 1)
            });
            AddFallbackRoom(occupancy, middles, RoomShape.Square2, 0, new[]
            {
                new Vector2Int(3, 0), new Vector2Int(4, 0),
                new Vector2Int(3, 1), new Vector2Int(4, 1)
            });
            AddFallbackRoom(occupancy, middles, RoomShape.Line3, 1, new[]
                { new Vector2Int(2, 1), new Vector2Int(2, 2), new Vector2Int(2, 3) });
            AddFallbackRoom(occupancy, middles, RoomShape.Square2, 0, new[]
            {
                new Vector2Int(0, 2), new Vector2Int(1, 2),
                new Vector2Int(0, 3), new Vector2Int(1, 3)
            });
            AddFallbackRoom(occupancy, middles, RoomShape.Square2, 0, new[]
            {
                new Vector2Int(3, 2), new Vector2Int(4, 2),
                new Vector2Int(3, 3), new Vector2Int(4, 3)
            });
            AddFallbackRoom(occupancy, middles, RoomShape.Line2, 0, new[]
                { new Vector2Int(0, 4), new Vector2Int(1, 4) });
            AddFallbackRoom(occupancy, middles, RoomShape.Line2, 0, new[]
                { new Vector2Int(3, 4), new Vector2Int(4, 4) });
            occupancy[StartCell.x, StartCell.y] = start;
            occupancy[BossCell.x, BossCell.y] = boss;
            if (!plan.Assemble(occupancy, start, boss, middles)) return plan;
            var random = new System.Random(seed);
            if (plan.BuildDoors(random, 0)) { plan.Valid = true; return plan; }

            plan.Doors.Clear();
            for (int i = 0; i < plan.Rooms.Count; i++) plan.Rooms[i].Doors.Clear();
            AddManualDoor(plan, new Vector2Int(2, 0), new Vector2Int(2, 1));
            AddManualDoor(plan, new Vector2Int(2, 4), new Vector2Int(2, 3));
            AddManualDoor(plan, new Vector2Int(1, 2), new Vector2Int(2, 2));
            AddManualDoor(plan, new Vector2Int(3, 2), new Vector2Int(2, 2));
            AddManualDoor(plan, new Vector2Int(0, 1), new Vector2Int(0, 2));
            AddManualDoor(plan, new Vector2Int(3, 1), new Vector2Int(3, 2));
            AddManualDoor(plan, new Vector2Int(1, 3), new Vector2Int(1, 4));
            AddManualDoor(plan, new Vector2Int(3, 3), new Vector2Int(3, 4));
            plan.Valid = true;
            return plan;
        }

        private static void AddFallbackRoom(PlannedRoom[,] occupancy, List<PlannedRoom> middles,
            RoomShape shape, int rotation, Vector2Int[] cells)
        {
            var room = CreateRoom(RoomRole.Middle, shape, rotation, cells);
            for (int i = 0; i < cells.Length; i++) occupancy[cells[i].x, cells[i].y] = room;
            middles.Add(room);
        }

        private static void AddManualDoor(DungeonPlan plan, Vector2Int cellA, Vector2Int cellB)
        {
            var roomA = plan.CellRooms[cellA.x, cellA.y];
            var roomB = plan.CellRooms[cellB.x, cellB.y];
            if (roomA == null || roomB == null || roomA == roomB) return;
            if (roomA.Index > roomB.Index)
            {
                var room = roomA; roomA = roomB; roomB = room;
                var cell = cellA; cellA = cellB; cellB = cell;
            }
            var door = new PlannedDoor { A = roomA, B = roomB, CellA = cellA, CellB = cellB };
            roomA.Doors.Add(door);
            roomB.Doors.Add(door);
            plan.Doors.Add(door);
        }
    }
}
