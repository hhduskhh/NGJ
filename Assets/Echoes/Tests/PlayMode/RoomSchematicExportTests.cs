using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    // Dev-only exporter: produces the raw room data used by the schematic renderer.
    public sealed class RoomSchematicExportTests
    {
        [Serializable] private sealed class FloorsData { public List<SchematicData> floors = new List<SchematicData>(); }

        [Serializable] private sealed class SchematicData
        {
            public int gridSize, cellWidth, cellHeight, mapWidth, mapHeight, floor;
            public string floorTitle;
            public List<RoomData> rooms = new List<RoomData>();
        }

        [Serializable] private sealed class RoomData
        {
            public int index, x, y, width, height, doors, solids;
            public float coverage;
            public string role, shape, name;
            public List<CellData> cells = new List<CellData>();
            public List<RectData> openings = new List<RectData>();
            public List<PropData> solidsList = new List<PropData>();
            public List<PropData> decor = new List<PropData>();
        }

        [Serializable] private sealed class CellData { public int x, y; }
        [Serializable] private sealed class RectData { public int x, y, width, height; }
        [Serializable] private sealed class PropData
        {
            public string kind, layer;
            public int x, y, width, height;
            public float scale;
        }

        [UnityTest, Explicit]
        public IEnumerator ExportCurrentFloorSchematics()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = UnityEngine.Object.FindObjectOfType<RunController>();
            var root = new FloorsData();
            for (int floor = 1; floor <= FloorTheme.Count; floor++)
            {
                if (floor > 1)
                {
                    run.ActivateBoss();
                    run.Boss.TakeDamage(99999);
                    run.AdvanceFloor();
                    yield return null;
                }
                root.floors.Add(CaptureFloor(run));
            }

            Directory.CreateDirectory("TestResults");
            var path = Path.Combine("TestResults", "room-schematics.json");
            File.WriteAllText(path, JsonUtility.ToJson(root, true));
            int rooms = 0;
            foreach (var floor in root.floors) rooms += floor.rooms.Count;
            Debug.Log("ECHOES_SCHEMATIC_EXPORTED " + path + " floors=" + root.floors.Count + " rooms=" + rooms);
            Assert.AreEqual(FloorTheme.Count, root.floors.Count);
            foreach (var floor in root.floors) Assert.Greater(floor.rooms.Count, 0);
        }

        private static SchematicData CaptureFloor(RunController run)
        {
            var map = run.Map;
            var solidField = typeof(DungeonMap).GetField("obstacleSpecs",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var decorField = typeof(DungeonMap).GetField("decorSpecs",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var solids = (List<RoomPropSpec>)solidField.GetValue(map);
            var decor = (List<RoomPropSpec>)decorField.GetValue(map);

            var data = new SchematicData
            {
                gridSize = DungeonMap.GridSize,
                cellWidth = DungeonMap.CellWidth,
                cellHeight = DungeonMap.CellHeight,
                mapWidth = DungeonMap.Width,
                mapHeight = DungeonMap.Height,
                floor = map.Floor,
                floorTitle = FloorTheme.Title(map.Floor)
            };
            foreach (var room in map.Rooms)
            {
                var roomData = new RoomData
                {
                    index = room.Index,
                    role = room.Role.ToString(),
                    shape = room.Shape.ToString(),
                    name = room.Name,
                    x = room.Bounds.x, y = room.Bounds.y,
                    width = room.Bounds.width, height = room.Bounds.height,
                    doors = room.DoorOpenings.Count,
                    solids = map.SolidCount(room),
                    coverage = map.SolidCoverage(room)
                };
                foreach (var cell in room.Cells)
                    roomData.cells.Add(new CellData { x = cell.x, y = cell.y });
                foreach (var opening in room.DoorOpenings)
                    roomData.openings.Add(new RectData
                    {
                        x = opening.x, y = opening.y, width = opening.width, height = opening.height
                    });
                foreach (var spec in solids)
                    if (ReferenceEquals(spec.Owner, room))
                        roomData.solidsList.Add(ToData(spec));
                foreach (var spec in decor)
                {
                    if (!ReferenceEquals(spec.Owner, room)) continue;
                    var tile = new Vector2Int(spec.X, spec.Y);
                    bool isFloor = map.IsFloorTile(tile);
                    if (spec.Layer == RoomPropLayer.WallObject ? isFloor : !isFloor) continue;
                    if (map.RoomAtPosition(new Vector2(spec.X, spec.Y)) != room) continue;
                    roomData.decor.Add(ToData(spec));
                }
                data.rooms.Add(roomData);
            }
            return data;
        }

        private static PropData ToData(RoomPropSpec spec) => new PropData
        {
            kind = spec.Kind,
            layer = spec.Solid ? "Solid" : spec.Layer.ToString(),
            x = spec.X, y = spec.Y,
            width = spec.Width, height = spec.Height,
            scale = spec.Scale
        };
    }
}
