using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    // Room P1 sprites are individual native-resolution files, not props-sheet slices.
    public static class RoomArt
    {
        public sealed class Entry
        {
            public readonly string Kind, Path;
            public readonly Vector2Int Canvas, Footprint;
            public readonly bool Ground;
            public Entry(string kind, string path, int w, int h, int fw = 0, int fh = 0, bool ground = false)
            {
                Kind = "room_" + kind; Path = "Art/Room/" + path;
                Canvas = new Vector2Int(w, h); Footprint = new Vector2Int(fw, fh); Ground = ground;
            }
        }

        public static readonly Entry[] Entries =
        {
            new Entry("lintel", "Common/f0_door_lintel_6_96x32", 96, 32, ground:true),
            new Entry("threshold", "Common/f0_door_threshold_6_96x16", 96, 16, ground:true),
            new Entry("stall", "Common/f0_start_stall_96x64", 96, 64, 5, 3),
            new Entry("bell", "Floor1/f1_hero_bell_frame_128", 128, 128, 6, 6),
            new Entry("rubble", "Floor1/f1_med_rubble_64x48", 64, 48, 4, 3),
            new Entry("crate", "Floor1/f1_med_crate_stack_64x64", 64, 64, 4, 4),
            new Entry("pew", "Floor1/f1_long_stone_pew_96x32", 96, 32, 6, 1),
            new Entry("pew_ns", "Floor1/f1_long_stone_pew_ns_32x96", 32, 96, 1, 6),
            new Entry("column", "Floor1/f1_tall_broken_column_32x96", 32, 96, 2, 2),
            new Entry("crack", "Floor1/f1_decal_crack_32x32", 32, 32, ground:true),
            new Entry("moss", "Floor1/f1_decal_moss_32x32", 32, 32, ground:true),
            new Entry("coins", "Floor1/f1_decal_coins_32x32", 32, 32, ground:true),
            new Entry("sconce", "Floor1/f1_wall_sconce_16x32", 16, 32),
            new Entry("boss_pillar", "Boss/f1_boss_pillar_48x48", 48, 48, 3, 3),
            new Entry("f2_boiler", "Floor2/f2_hero_boiler_128", 128, 128, 6, 6),
            new Entry("f2_tank", "Floor2/f2_med_pressure_tank_64x64", 64, 64, 4, 4),
            new Entry("f2_gears", "Floor2/f2_med_gear_pile_64x48", 64, 48, 4, 3),
            new Entry("f2_conveyor", "Floor2/f2_long_conveyor_96x32", 96, 32, 6, 1),
            new Entry("f2_conveyor_ns", "Floor2/f2_long_conveyor_ns_32x96", 32, 96, 1, 6),
            new Entry("f2_pipes", "Floor2/f2_long_pipe_manifold_96x32", 96, 32, 5, 1),
            new Entry("f2_pipes_ns", "Floor2/f2_long_pipe_manifold_ns_32x96", 32, 96, 1, 5),
            new Entry("f2_oil", "Floor2/f2_decal_oil_32x32", 32, 32, ground:true),
            new Entry("f2_water", "Floor2/f2_decal_water_32x32", 32, 32, ground:true),
            new Entry("f2_scrap", "Floor2/f2_decal_scrap_32x32", 32, 32, ground:true),
            new Entry("f2_wall_pipes", "Floor2/f2_wall_pipes_16x32", 16, 32),
            new Entry("f2_boss_pillar", "Boss/f2_boss_pillar_48x48", 48, 48, 3, 3),
            // The selected B shrine is tall and narrow; match its visible foot, not its canopy.
            new Entry("f3_shrine", "Floor3/f3_hero_tuning_fork_shrine_128", 128, 128, 3, 2),
            new Entry("f3_crystal", "Floor3/f3_med_crystal_cluster_64x80", 64, 80, 2, 2),
            new Entry("f3_shell", "Floor3/f3_med_ceramic_shell_64x64", 64, 64, 4, 3),
            new Entry("f3_organ", "Floor3/f3_tall_organ_pipes_64x96", 64, 96, 3, 1),
            new Entry("f3_reliquary", "Floor3/f3_med_reliquary_64x48", 64, 48, 4, 2),
            new Entry("f3_shards", "Floor3/f3_decal_crystal_shard_32x32", 32, 32, ground:true),
            new Entry("f3_gold", "Floor3/f3_decal_gold_filings_32x32", 32, 32, ground:true),
            new Entry("f3_inlay", "Floor3/f3_decal_geometric_inlay_32x32", 32, 32, ground:true),
            new Entry("f3_wall_shelf", "Floor3/f3_wall_reliquary_shelf_16x32", 16, 32),
            new Entry("f3_boss_pillar", "Boss/f3_boss_pillar_48x48", 48, 48, 1, 1)
        };
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static Entry[] ForFloor(int floor, bool includeCommon = true) =>
            Array.FindAll(Entries, e => e.Path.StartsWith("Art/Room/Floor" + floor + "/") ||
                e.Path.StartsWith("Art/Room/Boss/f" + floor + "_") ||
                (includeCommon && e.Path.StartsWith("Art/Room/Common/")));

        public static Entry Definition(string kind)
        {
            foreach (var entry in Entries) if (entry.Kind == kind) return entry;
            throw new ArgumentException("Unknown room art: " + kind);
        }

        public static Sprite Sprite(string kind)
        {
            if (sprites.TryGetValue(kind, out var sprite)) return sprite;
            var entry = Definition(kind);
            sprite = Resources.Load<Sprite>(entry.Path);
            if (!sprite) throw new InvalidOperationException("Missing room art: " + entry.Path);
            sprites.Add(kind, sprite);
            return sprite;
        }
    }
}
