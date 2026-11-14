namespace Echoes
{
    // Per-floor naming derived from Docs/012. Names are deterministic so tests and the HUD agree.
    public static class FloorTheme
    {
        public const int Count = 3;

        private static readonly string[] Titles =
        {
            "静 钟 街 区", "锈 鸣 工 坊", "原 音 圣 所"
        };

        private static readonly string[] StartNames =
        {
            "入口站台", "检修入口", "净音前室"
        };

        private static readonly string[] BossNames =
        {
            "守钟大厅", "鼓压大厅", "原音核心室"
        };

        private static readonly string[][] MiddleNames =
        {
            new[]
            {
                "换乘广场", "钟匠铺", "维修库", "听音岗", "废庭", "碎石回廊",
                "遗忘兵库", "回声档案室", "共振中庭", "深层通道", "旧站台", "候车廊",
                "铜管夹层", "蜡封储室", "断钟庭院", "静默岔路", "导音沟", "护耳工坊",
                "巡检哨", "积尘货仓", "低鸣井", "回响壁龛", "拱门暗廊", "旧钟窖"
            },
            new[]
            {
                "阀门间", "压力罐间", "装配间", "货运廊", "主冲压间", "蒸汽回廊",
                "工具库", "活塞庭", "铆接廊", "锅炉侧室", "冷却管道", "零件仓",
                "传动井", "排压间", "油迹工位", "锻锤庭", "齿轮夹层", "检修支线",
                "鼓风室", "旧传送带", "铁壳岔路", "气密门廊", "废料坑", "机械耳堂"
            },
            new[]
            {
                "调律间", "棱晶库", "双列长廊", "静默庭院", "环声前厅", "音叉回廊",
                "封存库", "净音侧室", "共鸣阶梯", "晶簇庭", "陶壳夹层", "细线廊",
                "无声井", "原音前庭", "调频间", "白瓷仓", "金线回廊", "对称厅",
                "静默壁龛", "音律档案", "晶体悬廊", "封闭核心侧室", "纯音井", "回声尽头"
            }
        };

        public static int ClampFloor(int floor) =>
            floor < 1 ? 1 : floor > Count ? Count : floor;

        public static string Title(int floor) => Titles[ClampFloor(floor) - 1];
        public static string StartName(int floor) => StartNames[ClampFloor(floor) - 1];
        public static string BossName(int floor) => BossNames[ClampFloor(floor) - 1];

        public static string MiddleName(int floor, int index)
        {
            var pool = MiddleNames[ClampFloor(floor) - 1];
            if (index < pool.Length) return pool[index];
            return pool[index % pool.Length] + "·" + (index + 1).ToString("D2");
        }
    }
}
