using UnityEngine;

namespace Echoes
{
    // One shared kiosk, reserved as solid terrain before room connectivity is checked.
    public static class StartStall
    {
        public const string Kind = "startStall";
        public const string ArtPath = "Room/Common/f0_start_stall_96x64";

        public static RoomPropSpec Layout(DungeonMap.Room room, Vector2Int tile)
        {
            return new RoomPropSpec
            {
                Kind = Kind,
                X = tile.x,
                Y = tile.y,
                Width = 5, Height = 3,
                Solid = true, Scale = 1,
                Layer = RoomPropLayer.FloorObject,
                Owner = room
            };
        }

        public static Vector2 CounterPosition(RoomPropSpec spec) =>
            new Vector2(spec.X + (spec.Width - 1) * .5f, spec.Y - 1);
    }
}
