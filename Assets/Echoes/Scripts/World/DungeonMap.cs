using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Echoes
{
    // Builds the playable per-floor cell grid from a pure DungeonPlan and owns all walkability queries.
    public sealed class DungeonMap : MonoBehaviour
    {
        public sealed class Room
        {
            public RectInt Bounds;
            public string Name;
            public bool Visited;
            public Vector2 Center { get; set; }
            public readonly List<Vector2Int> Cells = new List<Vector2Int>();
            public readonly List<RectInt> DoorOpenings = new List<RectInt>();
            public int Index;
            public RoomRole Role;
            public RoomShape Shape;
            public int Rotation;
            public int LayoutVariant;
            public int GridSize;
            public readonly List<RectInt> Walls = new List<RectInt>();
            public int CellCount { get { return Cells.Count; } }
            public int CellOriginY(int row) => (GridSize - 1 - row) * DungeonMap.CellHeight;
        }

        public sealed class Door
        {
            public Room A;
            public Room B;
            public RectInt Opening;
            public Vector2 Center { get { return new Vector2(
                Opening.x + Opening.width * .5f, Opening.y + Opening.height * .5f); } }
        }

        public const int CellWidth = 36;
        public const int CellHeight = 36;
        public const int DoorWidth = 6;
        public int GridSize { get; private set; } = DungeonPlan.MaxGridSize;
        public int Width { get; private set; } = DungeonPlan.MaxGridSize * CellWidth;
        public int Height { get; private set; } = DungeonPlan.MaxGridSize * CellHeight;

        private static readonly Vector2Int GridRight = new Vector2Int(1, 0);
        private static readonly Vector2Int GridDown = new Vector2Int(0, 1);
        private static readonly Vector2Int[] Steps =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 0),
            new Vector2Int(0, -1), new Vector2Int(-1, 0)
        };
        private bool[,] walkable = new bool[DungeonPlan.MaxGridSize * CellWidth,
            DungeonPlan.MaxGridSize * CellHeight];
        private bool[,] floor = new bool[DungeonPlan.MaxGridSize * CellWidth,
            DungeonPlan.MaxGridSize * CellHeight];
        private Room[,] cellRooms = new Room[DungeonPlan.MaxGridSize, DungeonPlan.MaxGridSize];
        private readonly List<RoomPropSpec> obstacleSpecs = new List<RoomPropSpec>();
        private readonly List<RoomPropSpec> decorSpecs = new List<RoomPropSpec>();
        private readonly Dictionary<string, TileBase> floorTiles = new Dictionary<string, TileBase>();
        private Transform generatedRoot, wallRoot;
        public bool BlocksProjectile(Collider2D collider) => !collider.isTrigger && collider.transform.IsChildOf(generatedRoot);
        private readonly List<RaycastHit2D> aimHits = new List<RaycastHit2D>(16);

        public bool HasFiringLine(Vector2 from, Vector2 to, Transform target)
        {
            Vector2 delta = to - from;
            float targetDistance = delta.magnitude;
            float blockerDistance = float.PositiveInfinity;
            Physics2D.CircleCast(from, Projectile.Radius, delta.normalized,
                new ContactFilter2D().NoFilter(), aimHits, targetDistance);
            foreach (var hit in aimHits)
            {
                if (hit.transform == target) targetDistance = Mathf.Min(targetDistance, hit.distance);
                else if (BlocksProjectile(hit.collider)) blockerDistance = Mathf.Min(blockerDistance, hit.distance);
            }
            return targetDistance < blockerDistance;
        }
        private Tilemap floorMap;
        private TilemapRenderer floorRenderer;
        private RunController run;

        public List<Room> Rooms { get; } = new List<Room>();
        public List<Door> Doors { get; } = new List<Door>();
        public int CurrentRoom { get; private set; }
        public Vector2Int CurrentCell { get; private set; }
        public int Floor { get; private set; } = 1;
        public int MapWidth { get { return Width; } }
        public int MapHeight { get { return Height; } }
        internal Transform GeometryRoot => generatedRoot;

        public Vector2 ShopCounterPosition { get; private set; }
        public Vector2 SignPosition { get; private set; }

        public Vector2 BossPosition
        {
            get
            {
                if (Rooms.Count == 0) return Vector2.zero;
                var boss = Rooms[Rooms.Count - 1];
                return FindWalkableInRoom(boss.Center + Vector2.up * 4f, boss);
            }
        }

        public Vector2 AltarPosition { get; private set; }

        public void Generate(RunController controller, int floorIndex)
        {
            run = controller;
            Floor = Mathf.Clamp(floorIndex + 1, 1, FloorTheme.Count);
            GameArt.SetTheme(floorIndex);
            ClearGenerated();
            Rooms.Clear();
            Doors.Clear();
            obstacleSpecs.Clear();
            decorSpecs.Clear();
            CurrentRoom = 0;

            var config = controller.Config;
            int gridSize = GridSizeForFloor(config);
            var plan = DungeonPlan.Generate(config.randomSeed + floorIndex * 1009, gridSize,
                config.generationAttempts, config.extraDoorChance);
            if (!plan.Valid) throw new System.InvalidOperationException("Dungeon layout generation failed.");

            GridSize = plan.GridSize;
            Width = GridSize * CellWidth;
            Height = GridSize * CellHeight;
            walkable = new bool[Width, Height];
            floor = new bool[Width, Height];
            cellRooms = new Room[GridSize, GridSize];

            BuildWorldRooms(plan);
            CarveRooms(plan);
            CarveDoors(plan);
            ApplyRoomWalls();
            ApplyTerrain(plan);
            PlaceShop(new System.Random(config.randomSeed + Floor * 3253));
            ComputeCenters();
            var bossRoom = Rooms[Rooms.Count - 1];
            Vector2 entrance = bossRoom.DoorOpenings[0].center;
            Vector2 inward = (bossRoom.Bounds.center - entrance).normalized;
            AltarPosition = FindWalkableInRoom(entrance + inward * config.altarEntranceDistance, bossRoom);
            TrackCell(StartPosition);
            RenderDungeon();
        }

        private void BuildWorldRooms(DungeonPlan plan)
        {
            int middleIndex = 0;
            var shapeVariants = new Dictionary<RoomShape, int>();
            foreach (var planned in plan.Rooms)
            {
                string name = planned.Role == RoomRole.Start ? FloorTheme.StartName(Floor) :
                    planned.Role == RoomRole.Boss ? FloorTheme.BossName(Floor) :
                    FloorTheme.MiddleName(Floor, middleIndex++);
                // Each shape walks its variant list in placement order, so the
                // first room of a shape always shows the deck's trap-bearing plan.
                int variant = 0;
                if (planned.Role == RoomRole.Middle)
                {
                    shapeVariants.TryGetValue(planned.Shape, out variant);
                    int count = RoomInterior.VariantCount(planned.Shape);
                    shapeVariants[planned.Shape] = (variant + 1) % count;
                    variant %= count;
                }
                var room = new Room
                {
                    Index = planned.Index,
                    Role = planned.Role,
                    Shape = planned.Shape,
                    Rotation = planned.Rotation,
                    LayoutVariant = variant,
                    Bounds = WorldBounds(planned),
                    Name = name
                };
                room.GridSize = GridSize;
                room.Cells.AddRange(planned.Cells);
                Rooms.Add(room);
                foreach (var cell in planned.Cells) cellRooms[cell.x, cell.y] = room;
            }
        }

        private RectInt WorldBounds(PlannedRoom room)
        {
            int minX = room.MinCell.x * CellWidth;
            int maxX = (room.MaxCell.x + 1) * CellWidth;
            int minY = (GridSize - 1 - room.MaxCell.y) * CellHeight;
            int maxY = (GridSize - room.MinCell.y) * CellHeight;
            return new RectInt(minX, minY, maxX - minX, maxY - minY);
        }

        public int CellOriginY(int row) => (GridSize - 1 - row) * CellHeight;
        private int WorldCellY(int row) => CellOriginY(row);

        private int GridSizeForFloor(GameConfig config)
        {
            if (config.floorGridSizes == null || config.floorGridSizes.Length == 0)
                return DungeonPlan.MaxGridSize;
            int value = config.floorGridSizes[Mathf.Clamp(Floor - 1, 0, config.floorGridSizes.Length - 1)];
            return Mathf.Clamp(value, DungeonPlan.MinGridSize, DungeonPlan.MaxGridSize);
        }

        private void CarveRooms(DungeonPlan plan)
        {
            foreach (var room in plan.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    int x0 = cell.x * CellWidth;
                    int y0 = WorldCellY(cell.y);
                    SetFloorRect(x0 + 1, y0 + 1, CellWidth - 2, CellHeight - 2);
                }
                foreach (var cell in room.Cells)
                {
                    int x0 = cell.x * CellWidth;
                    int y0 = WorldCellY(cell.y);
                    if (room.ContainsCell(cell + GridRight))
                        SetFloorRect(x0 + CellWidth - 1, y0 + 1, 2, CellHeight - 2);
                    if (room.ContainsCell(cell + GridDown))
                        SetFloorRect(x0 + 1, y0 - 1, CellWidth - 2, 2);
                    // The crossing of two internal seams is a 2x2 wall block in a 2x2 room.
                    // Open it when all four grid cells belong to the same room.
                    if (room.ContainsCell(cell + GridRight) && room.ContainsCell(cell + GridDown) &&
                        room.ContainsCell(cell + GridRight + GridDown))
                        SetFloorRect(x0 + CellWidth - 1, y0 - 1, 2, 2);
                }
            }
        }

        private void CarveDoors(DungeonPlan plan)
        {
            foreach (var planned in plan.Doors)
            {
                Vector2Int cellA = planned.CellA;
                Vector2Int cellB = planned.CellB;
                RectInt opening;
                if (cellA.x != cellB.x)
                {
                    int left = Mathf.Min(cellA.x, cellB.x);
                    int row = cellA.y;
                    opening = new RectInt(left * CellWidth + CellWidth - 1,
                        WorldCellY(row) + (CellHeight - DoorWidth) / 2, 2, DoorWidth);
                }
                else
                {
                    int above = Mathf.Min(cellA.y, cellB.y);
                    int below = Mathf.Max(cellA.y, cellB.y);
                    int col = cellA.x;
                    opening = new RectInt(col * CellWidth + (CellWidth - DoorWidth) / 2,
                        WorldCellY(below) + CellHeight - 1, DoorWidth, 2);
                }
                SetFloorRect(opening.x, opening.y, opening.width, opening.height);
                var roomA = Rooms[planned.A.Index];
                var roomB = Rooms[planned.B.Index];
                Doors.Add(new Door { A = roomA, B = roomB, Opening = opening });
                roomA.DoorOpenings.Add(opening);
                roomB.DoorOpenings.Add(opening);
            }
        }

        // Interior flow walls are applied after doors and before furniture.
        // Door clearances are subtracted, and any piece that would split the
        // room is skipped so every anchor layout stays fully connected.
        private void ApplyRoomWalls()
        {
            foreach (var room in Rooms)
            {
                var pieces = new List<RectInt>();
                foreach (var wall in RoomInterior.Partitions(room))
                {
                    var parts = new List<RectInt> { wall };
                    foreach (var door in room.DoorOpenings)
                    {
                        var clearance = new RectInt(door.x - 2, door.y - 2,
                            door.width + 4, door.height + 4);
                        parts = Subtract(parts, clearance);
                    }
                    foreach (var part in parts)
                        if (part.width > 0 && part.height > 0) pieces.Add(part);
                }
                var applied = new List<RectInt>();
                var appliedTiles = new List<List<Vector2Int>>();
                foreach (var piece in pieces)
                {
                    var tiles = BlockWall(room, piece);
                    if (tiles == null || tiles.Count == 0) continue;
                    applied.Add(piece);
                    appliedTiles.Add(tiles);
                }
                // The templates are already verified connected; this guard only
                // drops a screen when an unusual door cut/seam seals a pocket.
                for (int i = applied.Count - 1; i >= 0 && !RoomConnected(room); i--)
                {
                    RestoreTiles(appliedTiles[i]);
                    applied.RemoveAt(i);
                    appliedTiles.RemoveAt(i);
                }
                room.Walls.AddRange(applied);
            }
        }

        private List<Vector2Int> BlockWall(Room room, RectInt piece)
        {
            var tiles = new List<Vector2Int>();
            for (int x = piece.xMin; x < piece.xMax; x++)
                for (int y = piece.yMin; y < piece.yMax; y++)
                {
                    if (!InBounds(x, y) || !IsTileInRoom(x, y, room))
                    {
                        RestoreTiles(tiles);
                        return null;
                    }
                    if (!floor[x, y]) continue;
                    floor[x, y] = false;
                    walkable[x, y] = false;
                    tiles.Add(new Vector2Int(x, y));
                }
            return tiles;
        }

        private void RestoreTiles(List<Vector2Int> tiles)
        {
            foreach (var tile in tiles)
            {
                floor[tile.x, tile.y] = true;
                walkable[tile.x, tile.y] = true;
            }
        }

        private static List<RectInt> Subtract(List<RectInt> pieces, RectInt cut)
        {
            var result = new List<RectInt>();
            foreach (var piece in pieces)
            {
                if (!piece.Overlaps(cut))
                {
                    result.Add(piece);
                    continue;
                }
                int xMin = Mathf.Max(piece.xMin, cut.xMin);
                int xMax = Mathf.Min(piece.xMax, cut.xMax);
                int yMin = Mathf.Max(piece.yMin, cut.yMin);
                int yMax = Mathf.Min(piece.yMax, cut.yMax);
                if (yMax < piece.yMax)
                    result.Add(new RectInt(piece.xMin, yMax, piece.width, piece.yMax - yMax));
                if (yMin > piece.yMin)
                    result.Add(new RectInt(piece.xMin, piece.yMin, piece.width, yMin - piece.yMin));
                if (xMin > piece.xMin)
                    result.Add(new RectInt(piece.xMin, yMin, xMin - piece.xMin, yMax - yMin));
                if (xMax < piece.xMax)
                    result.Add(new RectInt(xMax, yMin, piece.xMax - xMax, yMax - yMin));
            }
            return result;
        }

        private void SetFloorRect(int x, int y, int width, int height)
        {
            for (int ix = x; ix < x + width; ix++)
                for (int iy = y; iy < y + height; iy++)
                    if (ix >= 0 && iy >= 0 && ix < Width && iy < Height)
                    {
                        floor[ix, iy] = true;
                        walkable[ix, iy] = true;
                    }
        }

        private void ApplyTerrain(DungeonPlan plan)
        {
            int theme = Mathf.Clamp(Floor - 1, 0, 2);
            var random = new System.Random(run.Config.randomSeed + Floor * 2003 + 17);
            foreach (var room in Rooms) BuildRoomObstacles(room, theme, random);
            if (AllWalkableConnected()) return;
            RestoreObstacles();
            if (!AllWalkableConnected())
                throw new System.InvalidOperationException("Dungeon floor is disconnected.");
        }

        private void BuildRoomObstacles(Room room, int theme, System.Random random)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var specs = RoomLayouts.Build(room, theme, random);
                var blocked = new List<Vector2Int>();
                var accepted = new List<RoomPropSpec>();
                foreach (var spec in specs)
                    if (spec.Solid && TryBlockObstacle(spec, room, blocked)) accepted.Add(spec);
                bool connected = RoomConnected(room);
                if (connected)
                {
                    obstacleSpecs.AddRange(accepted);
                    foreach (var spec in specs)
                        if (!spec.Solid) decorSpecs.Add(spec);
                    EnsureRoomCover(room, theme, accepted);
                    return;
                }
                foreach (var tile in blocked) walkable[tile.x, tile.y] = true;
            }
            // Keep the room playable even if every solid layout attempt is too dense.
            foreach (var spec in RoomLayouts.Build(room, theme, random))
                if (!spec.Solid) decorSpecs.Add(spec);
        }

        private void PlaceShop(System.Random random)
        {
            var shopRole = Floor == 1 ? RoomRole.Middle : RoomRole.Start;
            var candidates = Rooms.FindAll(room => room.Role == shopRole);
            Shuffle(candidates, random);
            foreach (var room in candidates)
            {
                var traps = RoomInterior.TrapAnchors(room);
                var tiles = new List<Vector2Int>();
                foreach (var cell in room.Cells)
                {
                    for (int x = 4; x < CellWidth - 6; x += 2)
                        for (int y = 4; y < CellHeight - 4; y += 2)
                            tiles.Add(new Vector2Int(cell.x * CellWidth + x, CellOriginY(cell.y) + y));
                }
                Shuffle(tiles, random);
                foreach (var tile in tiles)
                {
                    var spec = StartStall.Layout(room, tile);
                    var counter = Vector2Int.RoundToInt(StartStall.CounterPosition(spec));
                    // Leave the spawn point and its exit sign clear of the kiosk.
                    if (room.Role == RoomRole.Start && Vector2.Distance(counter, room.Bounds.center) < 7f) continue;
                    if (!IsWalkable(counter) || !IsWalkable(counter + Vector2Int.down)) continue;
                    if (traps.Exists(t => Vector2Int.Distance(t.Tile, counter) <= run.Config.trapAnchorSearchRadius + 6)) continue;
                    var blocked = new List<Vector2Int>();
                    if (!TryBlockObstacle(spec, room, blocked)) continue;
                    if (RoomConnected(room))
                    {
                        obstacleSpecs.Add(spec);
                        ShopCounterPosition = counter;
                        return;
                    }
                    foreach (var p in blocked) walkable[p.x, p.y] = true;
                }
            }
            throw new System.InvalidOperationException("No reachable shop site on this floor.");
        }

        // Dense flow walls reject some planned furniture. Top the room back up
        // with a few native props so cover stays useful without overfilling.
        private void EnsureRoomCover(Room room, int theme, List<RoomPropSpec> accepted)
        {
            if (room.Role != RoomRole.Middle || room.CellCount == 0) return;
            int area = room.CellCount * (CellWidth - 2) * (CellHeight - 2);
            if (area <= 0) return;
            float target = Floor == 1 ? .06f : .095f;
            const int minSolids = 6;
            int tiles = 0;
            foreach (var spec in accepted) tiles += SolidTiles(spec);
            if (tiles / (float)area >= target && accepted.Count >= minSolids) return;
            var kinds = theme == 0
                ? new[] { "room_rubble", "room_crate" }
                : theme == 1
                    ? new[] { "room_f2_pipes", "room_f2_pipes_ns", "room_f2_gears", "room_f2_tank" }
                    : new[] { "room_f3_organ", "room_f3_crystal", "room_f3_reliquary" };
            // Scan the room on coarse-to-tight grids. Wall-aware scanning packs
            // the ring corridors far better than random attempts.
            for (int pass = 0; pass < 3; pass++)
            {
                int step = pass == 0 ? 4 : pass == 1 ? 2 : 1;
                foreach (var cell in room.Cells)
                {
                    int x0 = cell.x * CellWidth, y0 = CellOriginY(cell.y);
                    for (int ly = 2; ly < CellHeight - 2; ly += step)
                        for (int lx = 2; lx < CellWidth - 2; lx += step)
                        {
                            if (tiles / (float)area >= target && accepted.Count >= minSolids)
                                return;
                            var spec = TryFillerProp(room, kinds, x0 + lx, y0 + ly);
                            if (spec == null) continue;
                            accepted.Add(spec);
                            obstacleSpecs.Add(spec);
                            tiles += SolidTiles(spec);
                            break;
                        }
                }
            }
        }

        private static int SolidTiles(RoomPropSpec spec)
        {
            var bounds = spec.NavigationBounds;
            if (bounds.width > 0 && bounds.height > 0) return bounds.width * bounds.height;
            return Mathf.Max(1, spec.Width) * Mathf.Max(1, spec.Height);
        }

        private RoomPropSpec TryFillerProp(Room room, string[] kinds, int x, int y)
        {
            foreach (var kind in kinds)
            {
                var art = RoomArt.Definition(kind);
                var spec = new RoomPropSpec
                {
                    Kind = art.Kind, X = x, Y = y,
                    Width = Mathf.Max(1, art.Footprint.x),
                    Height = Mathf.Max(1, art.Footprint.y),
                    Scale = 1, Solid = true,
                    Layer = RoomPropLayer.FloorObject, Owner = room
                };
                var blocked = new List<Vector2Int>();
                if (!TryBlockObstacle(spec, room, blocked)) continue;
                if (!RoomConnected(room))
                {
                    RestoreTiles(blocked);
                    continue;
                }
                return spec;
            }
            return null;
        }

        private bool TryBlockObstacle(RoomPropSpec spec, Room room, List<Vector2Int> blocked)
        {
            spec.MeasureBase();
            // Keep one floor tile of clearance around the placement envelope.
            for (int x = spec.X - 1; x <= spec.X + spec.Width; x++)
                for (int y = spec.Y - 1; y <= spec.Y + spec.Height; y++)
                    if (!InBounds(x, y) || !floor[x, y]) return false;
            for (int x = spec.NavigationBounds.xMin; x < spec.NavigationBounds.xMax; x++)
                for (int y = spec.NavigationBounds.yMin; y < spec.NavigationBounds.yMax; y++)
                {
                    if (!walkable[x, y] || !IsTileInRoom(x, y, room)) return false;
                    foreach (var door in room.DoorOpenings)
                    {
                        var clearance = new RectInt(door.x - 3, door.y - 3,
                            door.width + 6, door.height + 6);
                        if (clearance.Contains(new Vector2Int(x, y))) return false;
                    }
                }
            for (int x = spec.NavigationBounds.xMin; x < spec.NavigationBounds.xMax; x++)
                for (int y = spec.NavigationBounds.yMin; y < spec.NavigationBounds.yMax; y++)
                {
                    walkable[x, y] = false;
                    blocked.Add(new Vector2Int(x, y));
                }
            return true;
        }

        private void RestoreObstacles()
        {
            foreach (var spec in obstacleSpecs)
                for (int x = spec.NavigationBounds.xMin; x < spec.NavigationBounds.xMax; x++)
                    for (int y = spec.NavigationBounds.yMin; y < spec.NavigationBounds.yMax; y++)
                        if (InBounds(x, y)) walkable[x, y] = true;
            obstacleSpecs.Clear();
        }

        private bool RoomConnected(Room room)
        {
            var start = FindAnyWalkableTile(room);
            if (!start.HasValue) return false;
            var visited = new HashSet<Vector2Int>();
            var frontier = new Queue<Vector2Int>();
            visited.Add(start.Value);
            frontier.Enqueue(start.Value);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var step in Steps)
                {
                    var next = current + step;
                    if (!InBounds(next.x, next.y) || visited.Contains(next) ||
                        !walkable[next.x, next.y] || !IsTileInRoom(next.x, next.y, room)) continue;
                    visited.Add(next);
                    frontier.Enqueue(next);
                }
            }
            int total = 0;
            foreach (var cell in room.Cells)
            {
                int x0 = cell.x * CellWidth;
                int y0 = WorldCellY(cell.y);
                for (int x = x0; x < x0 + CellWidth; x++)
                    for (int y = y0; y < y0 + CellHeight; y++)
                        if (walkable[x, y] && IsTileInRoom(x, y, room)) total++;
            }
            return visited.Count == total;
        }

        private bool AllWalkableConnected()
        {
            if (Rooms.Count == 0) return false;
            var start = FindAnyWalkableTile(Rooms[0]);
            if (!start.HasValue) return false;
            var visited = new HashSet<Vector2Int>();
            var frontier = new Queue<Vector2Int>();
            visited.Add(start.Value);
            frontier.Enqueue(start.Value);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var step in Steps)
                {
                    var next = current + step;
                    if (!InBounds(next.x, next.y) || visited.Contains(next) ||
                        !walkable[next.x, next.y]) continue;
                    visited.Add(next);
                    frontier.Enqueue(next);
                }
            }
            int total = 0;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (walkable[x, y]) total++;
            return visited.Count == total;
        }

        private Vector2Int? FindAnyWalkableTile(Room room)
        {
            foreach (var cell in room.Cells)
            {
                int x0 = cell.x * CellWidth;
                int y0 = WorldCellY(cell.y);
                for (int x = x0 + 1; x < x0 + CellWidth - 1; x++)
                    for (int y = y0 + 1; y < y0 + CellHeight - 1; y++)
                        if (walkable[x, y]) return new Vector2Int(x, y);
            }
            return null;
        }

        private void ComputeCenters()
        {
            foreach (var room in Rooms)
            {
                var b = room.Bounds;
                var desired = new Vector2(b.x + b.width * .5f, b.y + b.height * .5f);
                room.Center = FindWalkableInRoom(desired, room);
            }
        }

        private Vector2 FindWalkableInRoom(Vector2 desired, Room room)
        {
            var origin = new Vector2Int(Mathf.FloorToInt(desired.x), Mathf.FloorToInt(desired.y));
            int max = Mathf.Max(Width, Height);
            for (int radius = 0; radius <= max; radius++)
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;
                        int x = origin.x + dx;
                        int y = origin.y + dy;
                        if (InBounds(x, y) && walkable[x, y] && IsTileInRoom(x, y, room))
                            return new Vector2(x, y);
                    }
            var any = FindAnyWalkableTile(room);
            return any.HasValue ? new Vector2(any.Value.x, any.Value.y) : desired;
        }

        private void RenderDungeon()
        {
            var root = new GameObject("DungeonTiles");
            generatedRoot = root.transform;
            generatedRoot.SetParent(transform);
            wallRoot = new GameObject("Walls").transform;
            wallRoot.SetParent(generatedRoot, false);
            RenderFloorTiles();
            foreach (var room in Rooms)
                foreach (var cell in room.Cells)
                {
                    int x0 = cell.x * CellWidth;
                    int y0 = WorldCellY(cell.y);
                    for (int x = x0; x < x0 + CellWidth; x++)
                        for (int y = y0; y < y0 + CellHeight; y++)
                        {
                            if (floor[x, y]) continue;
                            var renderer = PixelArt.Create("Wall", WallKind(x, y),
                                new Vector2(x, y), GameArt.FootOrder(y), wallRoot);
                            renderer.gameObject.AddComponent<BoxCollider2D>();
                        }
                }
            RenderDoorposts();
            RenderNativeDoorDetails();
            RenderObstacles();
            RenderDecorations();
            Vector2 exit = Rooms[0].DoorOpenings[0].center - StartPosition;
            SignPosition = FindWalkableInRoom(StartPosition + new Vector2(1, 1), Rooms[0]);
            int order = GameArt.FootOrder(SignPosition.y);
            var sign = PixelArt.Create("ExitSign", "signpost", SignPosition, order, generatedRoot);
            var arrow = PixelArt.Create("ExitArrow", "signArrow", SignPosition + Vector2.up * .94f, order + 1, sign.transform);
            arrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Round(Mathf.Atan2(exit.y, exit.x) / (Mathf.PI / 2)) * 90);
            foreach (var room in Rooms)
                RoomInterior.PlaceTraps(this, room, run, generatedRoot);
            PixelArt.Create("ChallengeAltar", "altar", AltarPosition, -120, generatedRoot);
        }

        private void RenderFloorTiles()
        {
            if (!floorMap)
            {
                var gridObject = new GameObject("DungeonGrid", typeof(Grid));
                gridObject.transform.SetParent(generatedRoot, false);
                // Tilemap cell centres sit at +0.5; shift the grid so cell (x,y)
                // matches our integer tile-centre convention.
                gridObject.transform.localPosition = new Vector3(-.5f, -.5f, 0);
                var tileObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
                tileObject.transform.SetParent(gridObject.transform, false);
                floorMap = tileObject.GetComponent<Tilemap>();
                floorRenderer = tileObject.GetComponent<TilemapRenderer>();
                floorRenderer.sortingLayerName = "Default";
                // Y-sorted walls/props reach much lower than -200 on the 180x180 map.
                floorRenderer.sortingOrder = -1000;
                if (PixelArt.LineMaterial) floorRenderer.sharedMaterial = PixelArt.LineMaterial;
            }
            floorMap.ClearAllTiles();
            var tiles = new TileBase[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (floor[x, y]) tiles[y * Width + x] = GetFloorTile(FloorKind(x, y));
            floorMap.SetTilesBlock(new BoundsInt(0, 0, 0, Width, Height, 1), tiles);
        }

        private TileBase GetFloorTile(string kind)
        {
            string key = GameArt.Floor + kind;
            if (floorTiles.TryGetValue(key, out var tile)) return tile;
            var created = ScriptableObject.CreateInstance<Tile>();
            created.name = key;
            created.sprite = GameArt.Get(kind);
            created.colliderType = Tile.ColliderType.None;
            floorTiles[key] = created;
            return created;
        }

        private static string FloorKind(int x, int y) =>
            (x * 7 + y * 11) % 9 == 0 ? "floorDetail" : (x + y) % 4 == 0 ? "floorAlt" : "floor";

        private static string WallKind(int x, int y) => (x + y) % 3 == 0 ? "wallAlt" : "wall";

        private void RenderDoorposts()
        {
            foreach (var door in Doors)
            {
                var o = door.Opening;
                if (o.width == 2)
                {
                    float x = o.x + .5f;
                    PlaceDoorpost(new Vector2(x, o.y - 1));
                    PlaceDoorpost(new Vector2(x, o.y + o.height));
                }
                else
                {
                    float y = o.y + .5f;
                    PlaceDoorpost(new Vector2(o.x - 1, y));
                    PlaceDoorpost(new Vector2(o.x + o.width, y));
                }
            }
        }

        private void PlaceDoorpost(Vector2 position) =>
            PixelArt.Create("Dressing_doorpost", "doorpost", position,
                GameArt.FootOrder(position.y) + 1, generatedRoot);

        private void RenderNativeDoorDetails()
        {
            foreach (var door in Doors)
            {
                bool vertical = door.Opening.height > door.Opening.width;
                var threshold = PixelArt.Create("DoorThreshold", "room_threshold", door.Center,
                    -899, generatedRoot);
                // These two assets are flat plan-view masonry/metal strips;
                // only these ground/top-cap sprites may rotate in the plane.
                threshold.transform.rotation = Quaternion.Euler(0, 0, vertical ? 90 : 0);
                var cap = PixelArt.Create("DoorLintel", "room_lintel", door.Center,
                    GameArt.FootOrder(door.Center.y) + 2, generatedRoot);
                cap.transform.rotation = threshold.transform.rotation;
            }
        }

        private void RenderObstacles()
        {
            foreach (var spec in obstacleSpecs)
            {
                float scale = spec.Scale > .01f ? spec.Scale : 1f;
                Vector2 position = spec.ArtPosition;
                var renderer = PixelArt.Create("Obstacle_" + spec.Kind, spec.Kind, position,
                    GameArt.FootOrder(position.y) + 1, generatedRoot, scale);
                var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
                collider.size = spec.CollisionBounds.size / scale;
                collider.offset = (spec.CollisionBounds.center - position) / scale;
            }
        }

        private void RenderDecorations()
        {
            foreach (var spec in decorSpecs)
            {
                if (!InBounds(spec.X, spec.Y)) continue;
                if (spec.Owner == null || !IsTileInRoom(spec.X, spec.Y, spec.Owner)) continue;
                bool isFloor = floor[spec.X, spec.Y];
                if (spec.Layer == RoomPropLayer.WallObject ? isFloor : !isFloor) continue;
                float scale = spec.Scale > .01f ? spec.Scale : 1f;
                var position = new Vector2(spec.X, spec.Y);
                if (spec.Layer == RoomPropLayer.FloorDecal)
                {
                    var decal = PixelArt.Create("Decal_" + spec.Kind, spec.Kind, position,
                        -900, generatedRoot, scale);
                    decal.color = spec.Color;
                }
                else
                {
                    PixelArt.Create((spec.Layer == RoomPropLayer.WallObject ? "WallDecor_" : "Decor_") + spec.Kind,
                        spec.Kind, position, GameArt.FootOrder(position.y) + 1, generatedRoot, scale);
                }
            }
        }

        private void ClearGenerated()
        {
            if (!generatedRoot) return;
            generatedRoot.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(generatedRoot.gameObject);
            else DestroyImmediate(generatedRoot.gameObject);
            generatedRoot = null;
            floorMap = null;
            floorRenderer = null;
        }

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T value = list[i]; list[i] = list[j]; list[j] = value;
            }
        }

        private bool InBounds(int x, int y) =>
            x >= 0 && y >= 0 && x < Width && y < Height;

        private bool IsTileInRoom(int x, int y, Room room)
        {
            if (!InBounds(x, y)) return false;
            int col = x / CellWidth;
            int row = GridSize - 1 - y / CellHeight;
            if (col < 0 || row < 0 || col >= GridSize || row >= GridSize) return false;
            return cellRooms[col, row] == room;
        }

        public Room RoomAtCell(int col, int row)
        {
            if (col < 0 || row < 0 || col >= GridSize || row >= GridSize) return null;
            return cellRooms[col, row];
        }

        private Vector2Int CellAtPosition(Vector2 position)
        {
            int col = Mathf.FloorToInt((position.x + .5f) / CellWidth);
            int row = GridSize - 1 - Mathf.FloorToInt((position.y + .5f) / CellHeight);
            return new Vector2Int(col, row);
        }

        public Room RoomAtPosition(Vector2 position)
        {
            var cell = CellAtPosition(position);
            return RoomAtCell(cell.x, cell.y);
        }

        public Vector2 StartPosition
        {
            get { return Rooms.Count == 0 ? Vector2.zero : Rooms[0].Center; }
        }

        // Art review only: re-skins the existing floor without moving rooms or actors.
        public void ApplyArtTheme(int theme)
        {
            GameArt.SetTheme(theme);
            if (generatedRoot) RenderFloorTiles();
            foreach (var binding in FindObjectsOfType<ArtBinding>()) binding.Refresh();
        }

        private void Update()
        {
            if (!run || !run.Player) return;
            TrackCell(run.Player.transform.position);
        }

        private void TrackCell(Vector2 position)
        {
            var cell = CellAtPosition(position);
            var room = RoomAtCell(cell.x, cell.y);
            if (room == null) return;
            CurrentCell = cell;
            CurrentRoom = room.Index;
            room.Visited = true;
        }

        public bool IsWalkable(Vector2Int p) =>
            p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height && walkable[p.x, p.y];

        public bool IsFloorTile(Vector2Int p) =>
            p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height && floor[p.x, p.y];

        public int SolidCount(Room room)
        {
            int count = 0;
            foreach (var spec in obstacleSpecs) if (spec.Owner == room) count++;
            return count;
        }

        public int SolidTileCount(Room room)
        {
            int count = 0;
            foreach (var spec in obstacleSpecs)
                if (spec.Owner == room) count += spec.NavigationBounds.width * spec.NavigationBounds.height;
            return count;
        }

        public float SolidCoverage(Room room)
        {
            int area = room.CellCount * (CellWidth - 2) * (CellHeight - 2);
            return area == 0 ? 0f : (float)SolidTileCount(room) / area;
        }

        // Traverse every crossed cell, including both sides of an exact corner.
        // Keep this traversal in sync with DungeonDarkness.shader.
        public bool HasLineOfSight(Vector2 from, Vector2 to, bool revealBlockingWall = false) =>
            ClearSegment(from, to, floor, revealBlockingWall);

        public bool HasClearInteractionPath(Vector2 from, Vector2 to) =>
            ClearSegment(from, to, walkable, false);

        private bool ClearSegment(Vector2 from, Vector2 to, bool[,] grid, bool revealBlockingWall)
        {
            bool Open(Vector2Int p) => InBounds(p.x, p.y) && grid[p.x, p.y];
            var cell = Vector2Int.FloorToInt(from + Vector2.one * .5f);
            var goal = Vector2Int.FloorToInt(to + Vector2.one * .5f);
            if (!Open(cell)) return false;
            Vector2 direction = to - from;
            int stepX = direction.x > 0 ? 1 : -1;
            int stepY = direction.y > 0 ? 1 : -1;
            float deltaX = direction.x == 0 ? 1e20f : Mathf.Abs(1 / direction.x);
            float deltaY = direction.y == 0 ? 1e20f : Mathf.Abs(1 / direction.y);
            float nextX = direction.x == 0 ? 1e20f : (cell.x + stepX * .5f - from.x) / direction.x;
            float nextY = direction.y == 0 ? 1e20f : (cell.y + stepY * .5f - from.y) / direction.y;
            for (int i = 0; i < Width + Height; i++)
            {
                if (cell == goal) return true;
                // At a cell-edge endpoint, banker's rounding can select a cell that
                // the next diagonal DDA step skips. Stop at the segment end.
                if (Mathf.Min(nextX, nextY) >= 1 - .000001f)
                {
                    if (Mathf.Abs(nextX - nextY) <= .000001f &&
                        (!Open(cell + new Vector2Int(stepX, 0)) || !Open(cell + new Vector2Int(0, stepY)))) return false;
                    return Open(goal) || (revealBlockingWall && goal.x >= 0 && goal.y >= 0 && goal.x < Width && goal.y < Height);
                }
                if (Mathf.Abs(nextX - nextY) <= .000001f)
                {
                    if (!Open(cell + new Vector2Int(stepX, 0)) ||
                        !Open(cell + new Vector2Int(0, stepY))) return false;
                    cell += new Vector2Int(stepX, stepY);
                    nextX += deltaX; nextY += deltaY;
                }
                else if (nextX < nextY) { cell.x += stepX; nextX += deltaX; }
                else { cell.y += stepY; nextY += deltaY; }
                if (cell == goal && revealBlockingWall)
                    return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
                if (!Open(cell)) return false;
            }
            return false;
        }

        public Vector2 NearestFloor(Vector2 point)
        {
            var p = Vector2Int.RoundToInt(point);
            if (IsWalkable(p)) return p;
            for (int r = 1; r < Width; r++)
                for (int x = -r; x <= r; x++) for (int y = -r; y <= r; y++)
                    if (Mathf.Abs(x) == r || Mathf.Abs(y) == r)
                    { var q = p + new Vector2Int(x, y); if (IsWalkable(q)) return q; }
            return Rooms.Count > 0 ? Rooms[0].Center : Vector2.zero;
        }

        public sealed class SpawnArea
        {
            public Vector2Int Cell;
            public readonly List<Vector2> Points = new List<Vector2>();
        }

        // Eligible spawn points grouped by room cell so selection can weight cells.
        public List<SpawnArea> SpawnAreas(int roomIndex)
        {
            var room = Rooms[roomIndex];
            var noSpawn = RoomInterior.NoSpawnZones(room);
            int stride = Mathf.Max(1, run.Config.spawnCellStride);
            var result = new List<SpawnArea>();
            foreach (var cell in room.Cells)
            {
                var area = new SpawnArea { Cell = cell };
                int x0 = cell.x * CellWidth;
                int y0 = WorldCellY(cell.y);
                for (int x = x0 + 2; x < x0 + CellWidth - 1; x += stride)
                    for (int y = y0 + 2; y < y0 + CellHeight - 1; y += stride)
                        if (IsSpawnTile(room, x, y) && !InNoSpawnZone(noSpawn, x, y))
                            area.Points.Add(new Vector2(x, y));
                if (area.Points.Count > 0) result.Add(area);
            }
            return result;
        }

        // Flat shuffled list kept for generation tests and callers that need every point.
        public List<Vector2> SpawnCells(int roomIndex)
        {
            var result = new List<Vector2>();
            foreach (var area in SpawnAreas(roomIndex)) result.AddRange(area.Points);
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = run.Random.Next(i + 1);
                var value = result[i]; result[i] = result[j]; result[j] = value;
            }
            return result;
        }

        private static bool InNoSpawnZone(List<RectInt> zones, int x, int y)
        {
            var tile = new Vector2Int(x, y);
            foreach (var zone in zones)
                if (zone.Contains(tile)) return true;
            return false;
        }

        private bool IsSpawnTile(Room room, int x, int y)
        {
            if (!InBounds(x, y) || !walkable[x, y] || !floor[x, y] || !IsTileInRoom(x, y, room))
                return false;
            foreach (var step in Steps)
                if (!InBounds(x + step.x, y + step.y) || !walkable[x + step.x, y + step.y]) return false;
            foreach (var door in room.DoorOpenings)
            {
                var clearance = new RectInt(door.x - 3, door.y - 3,
                    door.width + 6, door.height + 6);
                if (clearance.Contains(new Vector2Int(x, y))) return false;
            }
            return true;
        }

        // Breadth-first paths on this small, unweighted floor handle doors and cross-room pursuit.
        public List<Vector2> FindPath(Vector2 from, Vector2 to)
        {
            var start = Vector2Int.RoundToInt(NearestFloor(from));
            var goal = Vector2Int.RoundToInt(NearestFloor(to));
            var frontier = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            frontier.Enqueue(start); previous[start] = start;
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (current == goal) break;
                foreach (var step in Steps)
                {
                    var next = current + step;
                    if (!IsWalkable(next) || previous.ContainsKey(next)) continue;
                    previous[next] = current; frontier.Enqueue(next);
                }
            }
            var path = new List<Vector2>();
            if (!previous.ContainsKey(goal)) return path;
            for (var p = goal; p != start; p = previous[p]) path.Add(p);
            path.Reverse();
            return path;
        }
    }
}
