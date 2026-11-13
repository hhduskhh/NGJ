using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public sealed class DungeonMap : MonoBehaviour
    {
        public sealed class Room
        {
            public RectInt Bounds;
            public string Name;
            public bool Visited;
            public Vector2 Center => new Vector2(Bounds.x + 8.5f, Bounds.y + 6.5f);
        }
        public const int Width = 54, Height = 42;
        private readonly bool[,] walkable = new bool[Width, Height];
        public List<Room> Rooms { get; } = new List<Room>();
        public Vector2 BossPosition => Rooms[Rooms.Count - 1].Center + Vector2.up * 2;
        public Vector2 AltarPosition => Rooms[Rooms.Count - 1].Center + Vector2.down * 3;
        public int CurrentRoom { get; private set; }
        private RunController run;
        private static readonly Vector2Int[] steps =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public void Generate(RunController controller)
        {
            run = controller;
            var locations = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(2, 2) };
            var names = new[] { "01 / 静默前厅", "02 / 碎石回廊", "03 / 遗忘兵库", "04 / 回声档案室",
                "05 / 共振中庭", "06 / 深层通道", "07 / 守关者圣所" };
            for (int i = 0; i < locations.Length; i++)
            {
                var bounds = new RectInt(locations[i].x * 18, locations[i].y * 14, 18, 14);
                Rooms.Add(new Room { Bounds = bounds, Name = names[i] });
                for (int x = bounds.x + 1; x < bounds.xMax - 1; x++)
                    for (int y = bounds.y + 1; y < bounds.yMax - 1; y++) walkable[x, y] = true;
            }
            // All adjacent rooms connect through permanently open, four-cell doorways.
            foreach (var room in Rooms) foreach (var other in Rooms)
            {
                if (other.Bounds.x == room.Bounds.xMax && other.Bounds.y == room.Bounds.y)
                    for (int y = room.Bounds.y + 5; y <= room.Bounds.y + 8; y++)
                    { walkable[room.Bounds.xMax - 1, y] = true; walkable[other.Bounds.x, y] = true; }
                if (other.Bounds.y == room.Bounds.yMax && other.Bounds.x == room.Bounds.x)
                    for (int x = room.Bounds.x + 7; x <= room.Bounds.x + 10; x++)
                    { walkable[x, room.Bounds.yMax - 1] = true; walkable[x, other.Bounds.y] = true; }
            }
            var tiles = new GameObject("DungeonTiles").transform;
            tiles.SetParent(transform);
            foreach (var room in Rooms)
                for (int x = room.Bounds.x; x < room.Bounds.xMax; x++)
                    for (int y = room.Bounds.y; y < room.Bounds.yMax; y++)
                    {
                        bool floor = walkable[x, y];
                        var tile = PixelArt.Create(floor ? "Floor" : "Wall", floor ?
                            ((x * 7 + y * 11) % 9 == 0 ? "floorDetail" : (x+y)%4==0 ? "floorAlt" : "floor") :
                            (x+y)%3==0 ? "wallAlt" : "wall",
                            new Vector2(x, y), floor ? -200 : GameArt.FootOrder(y), tiles);
                        if (!floor) tile.gameObject.AddComponent<BoxCollider2D>();
                    }
            PixelArt.Create("ChallengeAltar", "altar", AltarPosition, -120, tiles);
            foreach (var room in Rooms)
            {
                var b = room.Bounds;
                // Bases lie on existing blocked wall cells. Dressing cannot add invisible obstacles.
                Decorate("lamp", new Vector2(b.x + 3, b.yMax - 1), tiles);
                Decorate("signature", new Vector2(b.x + 12, b.yMax - 1), tiles);
                Decorate("pipe", new Vector2(b.x + 9, b.yMax - 1), tiles);
                Decorate("station", new Vector2(b.x, b.y + 3), tiles);
                Decorate("crate", new Vector2(b.x, b.y + 9), tiles);
                if (Rooms.IndexOf(room) % 2 == 0) Decorate("rack", new Vector2(b.x + 6, b.yMax - 1), tiles);
                if (Rooms.Exists(r => r.Bounds.x == b.xMax && r.Bounds.y == b.y))
                {
                    Decorate("doorpost", new Vector2(b.xMax - 1, b.y + 4), tiles);
                    Decorate("doorpost", new Vector2(b.xMax, b.y + 9), tiles);
                }
                if (Rooms.Exists(r => r.Bounds.y == b.yMax && r.Bounds.x == b.x))
                {
                    Decorate("doorpost", new Vector2(b.x + 6, b.yMax - 1), tiles);
                    Decorate("doorpost", new Vector2(b.x + 11, b.yMax - 1), tiles);
                }
            }
        }

        private static void Decorate(string kind, Vector2 position, Transform parent) =>
            PixelArt.Create("Dressing_" + kind, kind, position, GameArt.FootOrder(position.y) + 1, parent);

        // Art review only: no population, geometry, collision, AI or floor progression changes.
        public void ApplyArtTheme(int theme)
        {
            GameArt.SetTheme(theme);
            foreach (var binding in FindObjectsOfType<ArtBinding>()) binding.Refresh();
        }

        private void Update()
        {
            if (!run.Player) return;
            var p = Vector2Int.RoundToInt(run.Player.transform.position);
            for (int i = 0; i < Rooms.Count; i++)
                if (Rooms[i].Bounds.Contains(p)) { CurrentRoom = i; Rooms[i].Visited = true; break; }
        }

        public bool IsWalkable(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height && walkable[p.x, p.y];
        // Traverse every crossed cell, including both sides of an exact corner.
        // Keep this traversal in sync with DungeonDarkness.shader.
        public bool HasLineOfSight(Vector2 from, Vector2 to, bool revealBlockingWall = false)
        {
            var cell = Vector2Int.FloorToInt(from + Vector2.one * .5f);
            var goal = Vector2Int.FloorToInt(to + Vector2.one * .5f);
            if (!IsWalkable(cell)) return false;
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
                        (!IsWalkable(cell + new Vector2Int(stepX, 0)) || !IsWalkable(cell + new Vector2Int(0, stepY)))) return false;
                    return IsWalkable(goal) || (revealBlockingWall && goal.x >= 0 && goal.y >= 0 && goal.x < Width && goal.y < Height);
                }
                if (Mathf.Abs(nextX - nextY) <= .000001f)
                {
                    if (!IsWalkable(cell + new Vector2Int(stepX, 0)) ||
                        !IsWalkable(cell + new Vector2Int(0, stepY))) return false;
                    cell += new Vector2Int(stepX, stepY);
                    nextX += deltaX; nextY += deltaY;
                }
                else if (nextX < nextY) { cell.x += stepX; nextX += deltaX; }
                else { cell.y += stepY; nextY += deltaY; }
                if (cell == goal && revealBlockingWall)
                    return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
                if (!IsWalkable(cell)) return false;
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
            return Rooms[0].Center;
        }

        public List<Vector2> SpawnCells(int roomIndex)
        {
            var b = Rooms[roomIndex].Bounds;
            var result = new List<Vector2>();
            for (int x = b.x + 3; x < b.xMax - 3; x += 3)
                for (int y = b.y + 3; y < b.yMax - 2; y += 3) result.Add(new Vector2(x, y));
            for (int i = result.Count - 1; i > 0; i--)
            { int j = run.Random.Next(i + 1); var t = result[i]; result[i] = result[j]; result[j] = t; }
            return result;
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
                foreach (var step in steps)
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
