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
            new UpgradeDefinition("精密弹头", "基础伤害 +20%", p => p.DamageScale += .2f),
            new UpgradeDefinition("快装枪机", "射击间隔缩短 12%\n下限 0.05 秒", p => p.ShotIntervalScale *= .88f),
            new UpgradeDefinition("轻装行进", "移动速度 +12%", p => p.MoveScale += .12f),
            new UpgradeDefinition("应急修复", "生命上限 +20\n恢复 35 点生命", p => { p.MaxHealth += 20; p.Heal(35); }),
            new UpgradeDefinition("共振扩音器", "枪声半径 +5（上限 12）\n每只围攻怪物增伤 +1 个百分点",
                p => { p.NoiseRadiusBonus += 5; p.PressureBonusPerEnemy += .01f; }, true),
            new UpgradeDefinition("诱饵调校", "响铃诱饵冷却缩短 2 秒\n下限 4 秒", p => p.DecoyCooldownReduction += 2),
            new UpgradeDefinition("致命打击", "暴击伤害 +8 个百分点", p => p.CriticalDamageBonus += .08f),
            new UpgradeDefinition("幸运四叶草", "幸运 +8%\n金币掉落翻倍概率增加 8 个百分点",
                p => p.DoubleLootChance = UnityEngine.Mathf.Clamp01(p.DoubleLootChance + .08f))
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
