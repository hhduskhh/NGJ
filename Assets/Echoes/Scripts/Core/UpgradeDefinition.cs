using System;
using System.Collections.Generic;

namespace Echoes
{
    public sealed class UpgradeDefinition
    {
        public string Title { get; }
        public string Description { get; }
        public bool IncreasesNoise { get; }
        private readonly Action<PlayerController> apply;

        private UpgradeDefinition(string title, string description, Action<PlayerController> effect,
            bool increasesNoise = false)
        { Title = title; Description = description; apply = effect; IncreasesNoise = increasesNoise; }

        public void Apply(PlayerController player) => apply(player);

        public static readonly UpgradeDefinition[] Catalog =
        {
            new UpgradeDefinition("精密弹头", "基础伤害 +20%\n枪声范围不变", p => p.DamageScale += .2f),
            new UpgradeDefinition("快装枪机", "射击间隔 -12%\n下限 0.10 秒 · 单次声音范围不变",
                p => p.ShotIntervalScale = UnityEngine.Mathf.Max(.36f, p.ShotIntervalScale * .88f)),
            new UpgradeDefinition("轻装行进", "移动速度 +12%\n便于拉开距离和转移房间", p => p.MoveScale += .12f),
            new UpgradeDefinition("应急修复", "生命上限 +20\n恢复 35 点生命", p => { p.MaxHealth += 20; p.Heal(35); }),
            new UpgradeDefinition("共振扩音器", "主动风险升级\n枪声半径 +3 · 可惊醒相邻房间",
                p => p.NoiseRadiusBonus += 3, true),
            new UpgradeDefinition("诱饵调校", "响铃诱饵冷却 -18%\n下限 5 秒 · 声音范围不变",
                p => p.DecoyCooldownScale = UnityEngine.Mathf.Max(.42f, p.DecoyCooldownScale * .82f))
        };

        public static List<UpgradeDefinition> Roll(Random random)
        {
            var pool = new List<UpgradeDefinition>(Catalog);
            var result = new List<UpgradeDefinition>(3);
            for (int i = 0; i < 3; i++)
            {
                int choice = random.Next(pool.Count);
                result.Add(pool[choice]);
                pool.RemoveAt(choice);
            }
            return result;
        }
    }
}
